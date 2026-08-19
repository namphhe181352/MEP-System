using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.AdminBranchServices;

/// <summary>
/// Service xu ly logic nghiep vu cho Admin quan ly chi nhanh.
/// </summary>
public class AdminBranchService : IAdminBranchService
{
    private readonly MEPDatabaseContext _context;
    private readonly MepNotificationService _notify;

    public AdminBranchService(MEPDatabaseContext context, MepNotificationService notify)
    {
        _context = context;
        _notify = notify;
    }

    private async Task LogActionWithUser(Guid adminId, Guid targetEntityId, string action, string tableName, string entityType, string description)
    {
        _context.SystemLogs.Add(new SystemLog
        {
            LogId = Guid.NewGuid(),
            UserId = adminId,
            Action = action,
            TableName = tableName,
            EntityType = entityType,
            EntityId = targetEntityId,
            Description = description,
            Level = "INFO",
            Source = "AdminBranchService",
            Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
        await Task.CompletedTask;
    }

    private async Task SafeNotifyBranchChangeAsync(Guid? ownerId, Guid branchId, string branchName, string action, string message)
    {
        try
        {
            await _notify.NotifyBranchChange(ownerId, branchId, action, branchName, message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdminBranchService] NotifyBranchChange failed: {ex.Message}");
        }
    }

    private async Task<(bool HasRelations, string Message)> CheckBranchHasBusinessDataAsync(Guid branchId)
    {
        var ordersCount = await _context.Orders.CountAsync(o => o.BranchId == branchId);
        if (ordersCount > 0)
            return (true, $"Khong the xoa chi nhanh vi con {ordersCount} don hang lien quan. Vui long xu ly truoc.");

        var stockTicketsCount = await _context.StockTickets.CountAsync(st => st.BranchId == branchId);
        if (stockTicketsCount > 0)
            return (true, $"Khong the xoa chi nhanh vi con {stockTicketsCount} phieu kho lien quan. Vui long xu ly truoc.");

        var inventoryCheckTicketsCount = await _context.InventoryCheckTickets.CountAsync(t => t.BranchId == branchId);
        if (inventoryCheckTicketsCount > 0)
            return (true, $"Khong the xoa chi nhanh vi con {inventoryCheckTicketsCount} phieu kiem ke lien quan. Vui long xu ly truoc.");

        var notificationsCount = await _context.Notifications.CountAsync(n => n.BranchId == branchId);
        if (notificationsCount > 0)
            return (true, $"Khong the xoa chi nhanh vi con {notificationsCount} thong bao lien quan. Vui long xu ly truoc.");

        var branchProductsWithStock = await _context.BranchProducts
            .CountAsync(bp => bp.BranchId == branchId && (bp.ActualStock > 0 || bp.ReservedStock > 0));
        if (branchProductsWithStock > 0)
            return (true, $"Khong the xoa chi nhanh vi con ton kho thuc te o {branchProductsWithStock} san pham. Vui long xu ly truoc.");

        return (false, string.Empty);
    }

    /// <summary>
    /// Khi xóa (nháp hoặc hẳn) một chi nhánh: khóa các user mà chi nhánh này là chi nhánh đang
    /// hoạt động DUY NHẤT của họ (sau khi loại chi nhánh này ra, họ không còn chi nhánh nào
    /// đang hoạt động). Đặt Status = "BRANCH_DELETED" + IsActive = 0 để login chặn kèm thông báo rõ.
    /// User còn chi nhánh đang hoạt động khác → KHÔNG bị khóa (giữ quyền truy cập chi nhánh kia).
    /// </summary>
    private async Task LockUsersWithoutActiveBranchAsync(Guid branchId, Guid adminId)
    {
        // Tập ứng viên bị ảnh hưởng: ManagerUserId + user được phân công (StaffAssignment)
        // + user có DefaultBranchId trỏ tới chi nhánh này.
        var candidate = new HashSet<Guid>();

        var managerId = await _context.Branches
            .Where(b => b.BranchId == branchId)
            .Select(b => (Guid?)b.ManagerUserId)
            .FirstOrDefaultAsync();
        if (managerId.HasValue && managerId.Value != Guid.Empty)
            candidate.Add(managerId.Value);

        var assignedUserIds = await _context.StaffAssignments
            .Where(sa => sa.BranchId == branchId)
            .Select(sa => sa.UserId)
            .ToListAsync();
        foreach (var uid in assignedUserIds)
            candidate.Add(uid);

        var defaultBranchUserIds = await _context.Users
            .Where(u => u.DefaultBranchId == branchId)
            .Select(u => u.UserId)
            .ToListAsync();
        foreach (var uid in defaultBranchUserIds)
            candidate.Add(uid);

        if (candidate.Count == 0)
            return;

        var candidateList = candidate.ToList();
        // Lấy danh sách users kèm theo Role để kiểm tra Admin
        var users = await _context.Users
            .Include(u => u.Roles)
            .Where(u => candidateList.Contains(u.UserId))
            .ToListAsync();

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var lockedIds = new List<Guid>();

        foreach (var u in users)
        {
            // Bỏ qua nếu là Admin vì Admin không thuộc chi nhánh nào
            if (u.Roles.Any(r => r.RoleName == "Admin"))
                continue;

            // Bỏ qua user còn đang hoạt động ở chi nhánh KHÁC.
            var hasOtherActiveBranch = await _context.StaffAssignments
                .AnyAsync(sa => sa.UserId == u.UserId
                             && sa.BranchId != branchId
                             && sa.Branch.IsActive == 1);
            if (hasOtherActiveBranch)
                continue;

            u.IsActive = 0;
            u.Status = "BRANCH_DELETED";
            u.DeletedAt = now;
            lockedIds.Add(u.UserId);
        }

        if (lockedIds.Count > 0)
        {
            await LogActionWithUser(adminId, branchId, "Khóa tài khoản nhân viên", "users", "User",
                $"Khoa {lockedIds.Count} user do chi nhanh bi xoa (khong con chi nhanh hoat dong).");
            await _context.SaveChangesAsync();
        }
    }

    private static AdminBranchDto MapToDto(Branch branch)
    {
        return new AdminBranchDto
        {
            BranchId = branch.BranchId,
            BranchName = branch.BranchName,
            BranchCode = branch.BranchCode,
            Phone = branch.Phone,
            City = branch.City,
            Address = branch.Address,
            Type = branch.Type,
            ManagerUserId = branch.ManagerUserId,
            ManagerFullName = branch.ManagerUser?.FullName,
            ManagerEmail = branch.ManagerUser?.Email,
            IsActive = branch.IsActive,
            CreatedAt = branch.CreatedAt,
            UpdatedAt = branch.UpdatedAt
        };
    }

    public async Task<PageResultDto<AdminBranchDto>> GetBranchesAsync(string? search, short? isActive, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = _context.Branches.Include(b => b.ManagerUser).AsQueryable();

        if (isActive.HasValue)
            query = query.Where(b => b.IsActive == isActive.Value);
        else
            query = query.Where(b => b.IsActive >= 0);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLower();
            query = query.Where(b => b.BranchName.ToLower().Contains(t) ||
                                     (b.BranchCode != null && b.BranchCode.ToLower().Contains(t)));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PageResultDto<AdminBranchDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminBranchDto> GetBranchByIdAsync(Guid id)
    {
        var branch = await _context.Branches
            .Include(b => b.ManagerUser)
            .FirstOrDefaultAsync(b => b.BranchId == id);

        if (branch == null)
            throw new KeyNotFoundException("Chi nhanh khong ton tai.");

        return MapToDto(branch);
    }

    public async Task<AdminBranchDto> CreateBranchAsync(CreateAdminBranchRequest dto, Guid currentAdminId)
    {
        var branchCode = dto.BranchCode?.Trim();
        var phone = dto.Phone?.Trim();
        var address = dto.Address?.Trim();

        if (!string.IsNullOrWhiteSpace(branchCode))
        {
            var codeExists = await _context.Branches.AnyAsync(b => b.BranchCode != null && b.BranchCode.ToLower() == branchCode.ToLower());
            if (codeExists)
                throw new BusinessException("Ma chi nhanh da ton tai.");
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            var phoneExists = await _context.Branches.AnyAsync(b => b.Phone != null && b.Phone.ToLower() == phone.ToLower());
            if (phoneExists)
                throw new BusinessException("So dien thoai da ton tai trong he thong.");
        }

        if (!string.IsNullOrWhiteSpace(address))
        {
            var addressExists = await _context.Branches.AnyAsync(b => b.Address != null && b.Address.ToLower() == address.ToLower());
            if (addressExists)
                throw new BusinessException("Dia chi da ton tai trong he thong.");
        }

        var branchNameTrimmed = dto.BranchName?.Trim();
        if (!string.IsNullOrWhiteSpace(branchNameTrimmed))
        {
            var nameExists = await _context.Branches.AnyAsync(b => b.BranchName.ToLower() == branchNameTrimmed.ToLower());
            if (nameExists)
                throw new BusinessException("Ten cua hang da ton tai.");
        }

        if (dto.ManagerUserId.HasValue)
        {
            var ownerBranchExists = await _context.Branches.AnyAsync(b => b.ManagerUserId == dto.ManagerUserId.Value);
            if (ownerBranchExists)
                throw new BusinessException("Chu cua hang nay da duoc gan cho mot cua hang khac.");
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var branch = new Branch
        {
            BranchId = Guid.NewGuid(),
            BranchName = dto.BranchName.Trim(),
            BranchCode = dto.BranchCode?.Trim(),
            Phone = dto.Phone?.Trim(),
            City = dto.City?.Trim(),
            Address = dto.Address?.Trim(),
            Type = dto.Type?.Trim(),
            ManagerUserId = dto.ManagerUserId,
            IsActive = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var transaction = await _context.Database.BeginTransactionAsync();

        _context.Branches.Add(branch);

        if (dto.ManagerUserId.HasValue)
        {
            _context.StaffAssignments.Add(new StaffAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = dto.ManagerUserId.Value,
                BranchId = branch.BranchId,
                AssignedAt = now
            });
        }

        await LogActionWithUser(currentAdminId, branch.BranchId, "Tạo chi nhánh mới", "branches", "Branch",
            $"Tao chi nhanh: {branch.BranchName}");

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return MapToDto(branch);
    }

    public async Task<AdminBranchDto> UpdateBranchAsync(Guid id, UpdateAdminBranchRequest dto, Guid currentAdminId)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
        if (branch == null)
            throw new KeyNotFoundException("Chi nhanh khong ton tai.");

        if (!string.IsNullOrWhiteSpace(dto.BranchName)) branch.BranchName = dto.BranchName.Trim();

        if (!string.IsNullOrWhiteSpace(dto.BranchCode))
        {
            var trimmedCode = dto.BranchCode.Trim();
            if (trimmedCode != branch.BranchCode)
            {
                var exists = await _context.Branches.AnyAsync(b => b.BranchId != id && b.BranchCode != null && b.BranchCode.ToLower() == trimmedCode.ToLower());
                if (exists) throw new BusinessException("Ma chi nhanh da ton tai.");
                branch.BranchCode = trimmedCode;
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.Phone))
        {
            var normalizedPhone = dto.Phone.Trim();
            var phoneExists = await _context.Branches.AnyAsync(b => b.BranchId != id && b.Phone != null && b.Phone.ToLower() == normalizedPhone.ToLower());
            if (phoneExists)
                throw new BusinessException("So dien thoai da ton tai trong he thong.");
            branch.Phone = normalizedPhone;
        }

        if (!string.IsNullOrWhiteSpace(dto.City))
            branch.City = dto.City.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Address))
        {
            var normalizedAddress = dto.Address.Trim();
            var addressExists = await _context.Branches.AnyAsync(b => b.BranchId != id && b.Address != null && b.Address.ToLower() == normalizedAddress.ToLower());
            if (addressExists)
                throw new BusinessException("Dia chi da ton tai trong he thong.");
            branch.Address = normalizedAddress;
        }

        if (!string.IsNullOrWhiteSpace(dto.Type))
            branch.Type = dto.Type.Trim();

        if (dto.ManagerUserId.HasValue)
        {
            var ownerBranchExists = await _context.Branches.AnyAsync(b => b.BranchId != id && b.ManagerUserId == dto.ManagerUserId.Value);
            if (ownerBranchExists)
                throw new BusinessException("Chu cua hang nay da duoc gan cho mot cua hang khac.");
            branch.ManagerUserId = dto.ManagerUserId.Value;
        }

        if (dto.IsActive.HasValue) branch.IsActive = dto.IsActive.Value;

        branch.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await LogActionWithUser(currentAdminId, branch.BranchId, "Cập nhật chi nhánh", "branches", "Branch",
            $"Cap nhat chi nhanh: {branch.BranchName}");

        await _context.SaveChangesAsync();

        await SafeNotifyBranchChangeAsync(branch.ManagerUserId, branch.BranchId, branch.BranchName, "UPDATE_BRANCH",
            $"Cua hang \"{branch.BranchName}\" cua ban vua duoc cap nhat boi quan tri vien.");

        return MapToDto(branch);
    }

    public async Task<(string message, short isActive)> DeleteBranchAsync(Guid id, Guid currentAdminId, bool force = false)
    {
        var branch = await _context.Branches
            .FirstOrDefaultAsync(b => b.BranchId == id);

        if (branch == null)
            throw new KeyNotFoundException("Chi nhanh khong ton tai.");

        if (branch.IsActive == 0)
            return ("Chi nhanh da o trang thai xoa nho.", (short)0);

        if (!force)
        {
            var relatedData = await CheckBranchHasBusinessDataAsync(id);
            if (relatedData.HasRelations)
                throw new BusinessException(relatedData.Message);
        }

        var managerId = branch.ManagerUserId;
        var branchName = branch.BranchName;

        branch.IsActive = 0;
        branch.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await LogActionWithUser(currentAdminId, branch.BranchId, "Xóa tạm thời chi nhánh", "branches", "Branch",
            $"Xoa nho chi nhanh: {branchName}");

        await _context.SaveChangesAsync();

        // Khóa các user mà chi nhánh này là chi nhánh đang hoạt động duy nhất (sau khi đã lưu IsActive=0).
        await LockUsersWithoutActiveBranchAsync(id, currentAdminId);

        await SafeNotifyBranchChangeAsync(managerId, branch.BranchId, branchName, "SOFT_DELETE_BRANCH",
            $"Cua hang \"{branchName}\" cua ban vua bi tam ngung hoat dong boi quan tri vien.");

        return ("Da xoa nho chi nhanh. Ban co the khoi phuc hoac xoa han.", (short)0);
    }

    public async Task<(string message, short isActive)> RestoreBranchAsync(Guid id, Guid currentAdminId)
    {
        var branch = await _context.Branches
            .FirstOrDefaultAsync(b => b.BranchId == id);

        if (branch == null)
            throw new KeyNotFoundException("Chi nhanh khong ton tai.");

        if (branch.IsActive == 1)
            return ("Chi nhanh dang hoat dong.", (short)1);

        branch.IsActive = 1;
        branch.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // Mở khóa lại các user đã bị khóa do chi nhánh này bị xóa (giờ chi nhánh đã hoạt động lại).
        var restoreUsers = await _context.Users
            .Where(u => u.Status == "BRANCH_DELETED"
                     && (_context.StaffAssignments.Any(sa => sa.UserId == u.UserId && sa.BranchId == id)
                         || u.DefaultBranchId == id))
            .ToListAsync();
        foreach (var u in restoreUsers)
        {
            u.IsActive = 1;
            u.Status = "ACTIVE";
            u.DeletedAt = null;
        }

        await LogActionWithUser(currentAdminId, branch.BranchId, "Khôi phục chi nhánh", "branches", "Branch",
            $"Khoi phuc chi nhanh: {branch.BranchName}");

        await _context.SaveChangesAsync();

        await SafeNotifyBranchChangeAsync(branch.ManagerUserId, branch.BranchId, branch.BranchName, "RESTORE_BRANCH",
            $"Cua hang \"{branch.BranchName}\" cua ban vua duoc khoi phuc hoat dong.");

        return ("Khoi phuc chi nhanh thanh cong.", (short)1);
    }

    public async Task<string> HardDeleteBranchAsync(Guid id, Guid currentAdminId, bool force = false)
    {
        var branch = await _context.Branches
            .FirstOrDefaultAsync(b => b.BranchId == id);

        if (branch == null)
            throw new KeyNotFoundException("Chi nhanh khong ton tai.");

        if (branch.IsActive == 1)
            throw new BusinessException("Vui long xoa nho truoc khi xoa han.");

        if (!force)
        {
            var relatedData = await CheckBranchHasBusinessDataAsync(id);
            if (relatedData.HasRelations)
                throw new BusinessException(relatedData.Message);
        }

        var managerId = branch.ManagerUserId;
        var branchName = branch.BranchName;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        // Khóa các user mà chi nhánh này là chi nhánh hoạt động duy nhất TRƯỚC khi xóa hẳn
        // (sau khi ExecuteDelete xóa StaffAssignments thì không còn truy vết được nữa).
        await LockUsersWithoutActiveBranchAsync(id, currentAdminId);

        try
        {
            await _context.StaffAssignments.Where(sa => sa.BranchId == id).ExecuteDeleteAsync();
            await _context.Shifts.Where(s => s.BranchId == id).ExecuteDeleteAsync();
            await _context.SystemConfigs.Where(c => c.BranchId == id).ExecuteDeleteAsync();
            await _context.InvoicePrintTemplates.Where(t => t.BranchId == id).ExecuteDeleteAsync();
            await _context.StockPrintTemplates.Where(t => t.BranchId == id).ExecuteDeleteAsync();
            await _context.ProductTaxonomies.Where(t => t.BranchId == id).ExecuteDeleteAsync();
            await _context.OrderDiscountTiers.Where(t => t.BranchId == id).ExecuteDeleteAsync();
            await _context.BranchProducts.Where(bp => bp.BranchId == id).ExecuteDeleteAsync();

            var deletedRows = await _context.Branches
                .Where(b => b.BranchId == id)
                .ExecuteDeleteAsync();

            if (deletedRows == 0)
            {
                await transaction.RollbackAsync();
                throw new BusinessException("Chi nhanh dang duoc xu ly boi mot yeu cau khac. Vui long thu lai.");
            }

            await LogActionWithUser(currentAdminId, id, "Xóa vĩnh viễn chi nhánh", "branches", "Branch",
                $"Xoa han chi nhanh: {branchName}");

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await SafeNotifyBranchChangeAsync(managerId, id, branchName, "HARD_DELETE_BRANCH",
                $"Cua hang \"{branchName}\" cua ban da bi xoa vinh vien boi quan tri vien.");

            return "Xóa hẳn chi nhánh thành công.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            
            if (force)
            {
                // Fallback: Pseudo Hard Delete (Ẩn hoàn toàn khỏi hệ thống, nhưng giữ lại database)
                await using var fallbackTx = await _context.Database.BeginTransactionAsync();
                
                var branchToHide = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
                if (branchToHide != null)
                {
                    branchToHide.IsActive = -1;
                    branchToHide.BranchName = $"[ĐÃ XÓA] {branchToHide.BranchName}";
                    branchToHide.BranchCode = $"DEL_{Guid.NewGuid().ToString("N").Substring(0, 8)}_{branchToHide.BranchCode}";
                    branchToHide.Phone = $"del_{Guid.NewGuid().ToString("N").Substring(0, 5)}_{branchToHide.Phone}";
                    branchToHide.ManagerUserId = null;
                    branchToHide.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                    
                    if (managerId != Guid.Empty)
                    {
                        var manager = await _context.Users.FirstOrDefaultAsync(u => u.UserId == managerId);
                        if (manager != null)
                        {
                            var otherBranches = await _context.Branches.AnyAsync(b => b.ManagerUserId == managerId && b.BranchId != id && b.IsActive >= 0);
                            if (!otherBranches)
                            {
                                manager.Status = "0"; 
                                manager.Email = $"del_{Guid.NewGuid().ToString("N").Substring(0, 6)}_{manager.Email}";
                                manager.Username = $"del_{Guid.NewGuid().ToString("N").Substring(0, 6)}_{manager.Username}";
                                manager.PhoneNumber = $"del_{Guid.NewGuid().ToString("N").Substring(0, 6)}_{manager.PhoneNumber}";
                            }
                        }
                    }

                    await _context.StaffAssignments.Where(sa => sa.BranchId == id).ExecuteDeleteAsync();
                    
                    await LogActionWithUser(currentAdminId, id, "Xóa ẩn chi nhánh", "branches", "Branch",
                        $"Xóa ẩn chi nhánh (do vướng dữ liệu): {branchName}");

                    await _context.SaveChangesAsync();
                    await fallbackTx.CommitAsync();

                    await SafeNotifyBranchChangeAsync(managerId, id, branchName, "HARD_DELETE_BRANCH",
                        $"Cua hang \"{branchName}\" cua ban da bi xoa boi quan tri vien.");

                    return "Đã xóa ẩn cửa hàng và giải phóng thông tin do còn dữ liệu lịch sử liên kết.";
                }
                
                throw new BusinessException("Lỗi trong quá trình xóa ẩn cửa hàng.");
            }
            else
            {
                throw new BusinessException("Không thể xóa hẳn cửa hàng này vì vẫn còn dữ liệu giao dịch liên kết chặt chẽ trong hệ thống. Vui lòng chỉ sử dụng chức năng Xóa Nháp.");
            }
        }
    }

    public async Task<List<BranchUserDto>> GetBranchUsersAsync(Guid id)
    {
        var exists = await _context.Branches.AnyAsync(b => b.BranchId == id);
        if (!exists)
            throw new KeyNotFoundException("Chi nhanh khong ton tai.");

        var users = await _context.StaffAssignments
            .Include(sa => sa.User)
            .ThenInclude(u => u.Roles)
            .Where(sa => sa.BranchId == id)
            .Select(sa => new BranchUserDto
            {
                StaffAssignmentId = sa.AssignmentId,
                UserId = sa.UserId,
                FullName = sa.User.FullName,
                Email = sa.User.Email,
                Role = sa.User.Roles.FirstOrDefault().RoleName,
                IsManager = sa.User.UserId == sa.Branch.ManagerUserId ? (short)1 : (short)0,
                AssignedAt = sa.AssignedAt
            })
            .ToListAsync();

        return users;
    }

    public async Task AssignUserToBranchAsync(Guid id, AssignUserToBranchRequest dto, Guid currentAdminId)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
        if (branch == null)
            throw new KeyNotFoundException("Chi nhanh khong ton tai.");

        var userExists = await _context.Users.AnyAsync(u => u.UserId == dto.UserId);
        if (!userExists)
            throw new KeyNotFoundException("User khong ton tai.");

        var assignment = await _context.StaffAssignments
            .FirstOrDefaultAsync(sa => sa.BranchId == id && sa.UserId == dto.UserId);

        if (assignment == null)
        {
            assignment = new StaffAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = dto.UserId,
                BranchId = id,
                AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };
            _context.StaffAssignments.Add(assignment);
        }

        if (dto.IsManager)
            branch.ManagerUserId = dto.UserId;

        await LogActionWithUser(currentAdminId, branch.BranchId, "Gán nhân viên vào chi nhánh", "staff_assignments", "Branch",
            $"Gan user {dto.UserId} vao chi nhanh {branch.BranchName}");

        await _context.SaveChangesAsync();
    }

    public async Task RemoveUserFromBranchAsync(Guid id, Guid userId, Guid currentAdminId)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
        if (branch == null)
            throw new KeyNotFoundException("Chi nhanh khong ton tai.");

        var assignment = await _context.StaffAssignments
            .FirstOrDefaultAsync(sa => sa.BranchId == id && sa.UserId == userId);

        if (assignment == null)
            throw new BusinessException("Nguoi dung khong thuoc chi nhanh nay.");

        _context.StaffAssignments.Remove(assignment);

        if (branch.ManagerUserId == userId)
            branch.ManagerUserId = null;

        await LogActionWithUser(currentAdminId, branch.BranchId, "Xóa nhân viên khỏi chi nhánh", "staff_assignments", "Branch",
            $"Xoa user {userId} khoi chi nhanh {branch.BranchName}");

        await _context.SaveChangesAsync();
    }
}
