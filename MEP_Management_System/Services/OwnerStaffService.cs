using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MEP_Management_System.Services;

public interface IOwnerStaffService
{
    Task<(List<OwnerStaffDto> Items, int TotalCount, int PageNumber, int PageSize, int TotalPages, bool HasNextPage, bool HasPreviousPage)> GetStaffsAsync(
        Guid branchId, Guid ownerId, string? search, bool? isActive, bool? isDeleted, string? view, int page, int pageSize);
    Task<OwnerStaffDto?> GetStaffByIdAsync(Guid id, Guid branchId);
    Task<object> CheckStaffRelationsAsync(Guid id, Guid branchId);
    Task<object> CheckStaffUsageAsync(Guid id, Guid branchId);
    Task<Dictionary<string, List<string>>> GetRolePermissionsAsync();
    Task<List<object>> GetAvailablePermissionsAsync();
    Task<OwnerStaffDto> CreateStaffAsync(CreateStaffWithPermissionRequest dto, Guid ownerId, Guid branchId);
    Task<bool> UpdateStaffAsync(Guid id, UpdateStaffPermissionRequest dto, Guid branchId);
    Task<bool> ToggleStaffStatusAsync(Guid id, Guid ownerId, Guid branchId);
    Task<(bool Success, string Message, object? Data)> DeleteStaffAsync(Guid id, Guid ownerId, Guid branchId);
    Task<List<DeletedStaffDto>> GetDeletedStaffsAsync(Guid branchId, Guid ownerId);
    Task<bool> RestoreStaffAsync(Guid id, Guid branchId);
    Task<bool> PermanentDeleteStaffAsync(Guid id, Guid branchId);
}

public class OwnerStaffService : IOwnerStaffService
{
    private readonly MEPDatabaseContext _context;
    private readonly IConfiguration _configuration;

    public OwnerStaffService(MEPDatabaseContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    private async Task<OwnerStaffDto> MapToDto(User user)
    {
        var rolePermissionCodes = user.Roles
            .SelectMany(r => r.Permissions)
            .Select(p => p.PermissionCode)
            .ToHashSet();

        var deniedCodes = user.UserPermissions
            .Where(up => up.IsGranted == false)
            .Select(up => up.Permission?.PermissionCode ?? "")
            .Where(c => !string.IsNullOrEmpty(c))
            .ToHashSet();

        var allowedCodes = user.UserPermissions
            .Where(up => up.IsGranted != false)
            .Select(up => up.Permission?.PermissionCode ?? "")
            .Where(c => !string.IsNullOrEmpty(c))
            .ToHashSet();

        var effectivePermissions = rolePermissionCodes
            .Union(allowedCodes)
            .Except(deniedCodes)
            .Distinct()
            .ToList();

        return new OwnerStaffDto
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            PermissionCodes = effectivePermissions,
            HasCustomPermissions = user.UserPermissions.Any(),
            Roles = user.Roles.Select(r => r.RoleName).ToList()
        };
    }

    private async Task<bool> IsEmailTakenAsync(string email, Guid? excludeUserId = null)
    {
        var normalized = email.Trim().ToLower();
        var query = _context.Users.Where(u => u.Email.ToLower() == normalized && u.Status != "DELETED");
        if (excludeUserId.HasValue)
            query = query.Where(u => u.UserId != excludeUserId.Value);
        return await query.AnyAsync();
    }

    private async Task<bool> IsPhoneTakenAsync(string phone, Guid? excludeUserId = null)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        var normalized = phone.Trim();
        var query = _context.Users.Where(u => u.PhoneNumber == normalized && u.Status != "DELETED");
        if (excludeUserId.HasValue)
            query = query.Where(u => u.UserId != excludeUserId.Value);
        return await query.AnyAsync();
    }

    private async Task<bool> IsUsernameTakenInBranchAsync(string username, Guid branchId)
    {
        return await _context.StaffAssignments
            .Include(sa => sa.User)
            .AnyAsync(sa => sa.BranchId == branchId &&
                sa.User.Username.ToLower() == username.Trim().ToLower() &&
                sa.User.Status != "DELETED" && sa.User.Status != "PERMANENT_DELETED");
    }

    private async Task<(List<Permission> Permissions, List<string> MissingCodes)> ValidatePermissionsAsync(
        IEnumerable<string> codes)
    {
        var codeList = codes?.Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (!codeList.Any())
            return (new List<Permission>(), new List<string>());

        var permissions = await _context.Permissions
            .Where(p => codeList.Contains(p.PermissionCode))
            .ToListAsync();

        var foundCodes = permissions.Select(p => p.PermissionCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = codeList.Where(c => !foundCodes.Contains(c)).ToList();

        try
        {
            foreach (var code in missing)
            {
                var newPerm = new Permission
                {
                    PermissionId = Guid.NewGuid(),
                    PermissionCode = code,
                    PermissionName = code
                };
                _context.Permissions.Add(newPerm);
                permissions.Add(newPerm);
            }
            if (missing.Any())
            {
                await _context.SaveChangesAsync();
                missing.Clear();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WARN] Failed to auto-create permissions: {ex.Message}");
        }

        return (permissions, missing);
    }

    private async Task<Role> GetOrCreateRoleAsync(string roleName)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r =>
            r.RoleName.ToLower() == roleName.Trim().ToLower());

        if (role == null)
        {
            role = new Role
            {
                RoleId = Guid.NewGuid(),
                RoleName = roleName.Trim(),
                Description = $"Role {roleName} - tự động tạo",
                IsSystem = 1
            };
            _context.Roles.Add(role);
        }

        return role;
    }

    private async Task<bool> IsUserInBranchAsync(Guid userId, Guid branchId)
    {
        return await _context.StaffAssignments
            .AnyAsync(sa => sa.UserId == userId && sa.BranchId == branchId);
    }

    public async Task<(List<OwnerStaffDto> Items, int TotalCount, int PageNumber, int PageSize, int TotalPages, bool HasNextPage, bool HasPreviousPage)> GetStaffsAsync(
        Guid branchId, Guid ownerId, string? search, bool? isActive, bool? isDeleted, string? view, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var staffUserIds = await _context.StaffAssignments
            .Where(sa => sa.BranchId == branchId)
            .Select(sa => sa.UserId)
            .ToListAsync();

        string? statusFilter = null;
        if (string.Equals(view, "all", StringComparison.OrdinalIgnoreCase))
            statusFilter = null;
        else if (string.Equals(view, "hidden", StringComparison.OrdinalIgnoreCase))
            statusFilter = "INACTIVE";
        else if (string.Equals(view, "deleted", StringComparison.OrdinalIgnoreCase))
            statusFilter = "DELETED";
        else
            statusFilter = "ACTIVE";

        IQueryable<User> query;

        if (isDeleted == true || string.Equals(view, "deleted", StringComparison.OrdinalIgnoreCase))
        {
            query = _context.Users
                .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
                .Include(u => u.Roles).ThenInclude(r => r.Permissions)
                .Where(u => staffUserIds.Contains(u.UserId)
                    && (u.Status == "DELETED" || u.Status == "PERMANENT_DELETED" || u.FullName.EndsWith("(Đã xóa)") || u.Email.StartsWith("deleted_") || u.Email.StartsWith("del_") || u.Email.EndsWith("@mep.deleted"))
                    && u.UserId != ownerId
                    && !u.Roles.Any(r => r.RoleName == "Admin" || r.RoleName == "Owner"));
        }
        else
        {
            query = _context.Users
                .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
                .Include(u => u.Roles).ThenInclude(r => r.Permissions)
                .Where(u => staffUserIds.Contains(u.UserId)
                    && u.UserId != ownerId
                    && u.Status != "DELETED"
                    && u.Status != "PERMANENT_DELETED"
                    && !u.FullName.EndsWith("(Đã xóa)")
                    && !u.Email.StartsWith("deleted_")
                    && !u.Email.StartsWith("del_")
                    && !u.Email.EndsWith("@mep.deleted")
                    && !u.Roles.Any(r => r.RoleName == "Admin" || r.RoleName == "Owner"));
        }

        if (statusFilter != null)
            query = query.Where(u => u.Status == statusFilter);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
        }

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == (isActive.Value ? (short)1 : (short)0));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = new List<OwnerStaffDto>();
        foreach (var u in items)
            dtos.Add(await MapToDto(u));

        return (dtos, totalCount, page, pageSize,
            (int)Math.Ceiling(totalCount / (double)pageSize),
            page * pageSize < totalCount,
            page > 1);
    }

    public async Task<OwnerStaffDto?> GetStaffByIdAsync(Guid id, Guid branchId)
    {
        var isInBranch = await IsUserInBranchAsync(id, branchId);
        if (!isInBranch) return null;

        var staff = await _context.Users
            .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .Include(u => u.Roles).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.UserId == id);

        return staff == null ? null : await MapToDto(staff);
    }

    public async Task<object> CheckStaffRelationsAsync(Guid id, Guid branchId)
    {
        var isInBranch = await IsUserInBranchAsync(id, branchId);
        if (!isInBranch) throw new KeyNotFoundException("Nhân viên không tồn tại hoặc không thuộc chi nhánh này.");

        var invoiceCount = await _context.Invoices.CountAsync(i => i.UserId == id);
        var orderCount = await _context.Orders.CountAsync(o => o.UserId == id);
        var returnOrderCount = await _context.ReturnOrders.CountAsync(r => r.UserId == id);
        var purchaseOrderCount = await _context.PurchaseOrders.CountAsync(p => p.UserId == id);
        var shiftCount = await _context.Shifts.CountAsync(s => s.UserId == id);
        var stockTicketCount = await _context.StockTickets.CountAsync(s => s.UserId == id);

        var hasRelations = invoiceCount > 0 || orderCount > 0 || returnOrderCount > 0
            || purchaseOrderCount > 0 || shiftCount > 0 || stockTicketCount > 0;

        var messages = new List<string>();
        if (invoiceCount > 0) messages.Add($"{invoiceCount} hóa đơn");
        if (orderCount > 0) messages.Add($"{orderCount} đơn hàng");
        if (returnOrderCount > 0) messages.Add($"{returnOrderCount} phiếu trả hàng");
        if (purchaseOrderCount > 0) messages.Add($"{purchaseOrderCount} đơn mua hàng");
        if (shiftCount > 0) messages.Add($"{shiftCount} ca làm việc");
        if (stockTicketCount > 0) messages.Add($"{stockTicketCount} phiếu kho");

        var message = hasRelations
            ? $"Nhân viên có: {string.Join(", ", messages)}"
            : "Nhân viên chưa có dữ liệu nghiệp vụ.";

        return new
        {
            hasRelations,
            message,
            invoiceCount,
            orderCount,
            returnOrderCount,
            purchaseOrderCount,
            shiftCount,
            stockTicketCount
        };
    }

    public async Task<object> CheckStaffUsageAsync(Guid id, Guid branchId)
    {
        var isInBranch = await IsUserInBranchAsync(id, branchId);
        if (!isInBranch) throw new KeyNotFoundException("Nhân viên không tồn tại hoặc không thuộc chi nhánh này.");

        var staff = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
        if (staff == null) throw new KeyNotFoundException("Nhân viên không tồn tại.");

        var orderCount = await _context.Orders.CountAsync(o => o.UserId == id);
        var invoiceCount = await _context.Invoices.CountAsync(i => i.UserId == id);
        var shiftCount = await _context.Shifts.CountAsync(s => s.UserId == id);
        var stockTicketCreatedCount = await _context.StockTickets.CountAsync(s => s.UserId == id);
        var stockTicketConfirmedCount = await _context.StockTickets.CountAsync(s => s.ConfirmedBy == id);
        var stockTicketCancelledCount = await _context.StockTickets.CountAsync(s => s.CancelledBy == id);
        var stockTicketTotalCount = stockTicketCreatedCount + stockTicketConfirmedCount + stockTicketCancelledCount;
        var violationReportCount = 0;

        var hasUsage = orderCount > 0 || invoiceCount > 0 || shiftCount > 0
            || stockTicketTotalCount > 0 || violationReportCount > 0;

        var usageDetails = new List<string>();
        if (orderCount > 0) usageDetails.Add($"{orderCount} đơn hàng");
        if (invoiceCount > 0) usageDetails.Add($"{invoiceCount} hóa đơn");
        if (shiftCount > 0) usageDetails.Add($"{shiftCount} ca làm việc");
        if (stockTicketCreatedCount > 0) usageDetails.Add($"{stockTicketCreatedCount} phiếu kho (người tạo)");
        if (stockTicketConfirmedCount > 0) usageDetails.Add($"{stockTicketConfirmedCount} phiếu kho (người xác nhận)");
        if (stockTicketCancelledCount > 0) usageDetails.Add($"{stockTicketCancelledCount} phiếu kho (người hủy)");
        if (violationReportCount > 0) usageDetails.Add($"{violationReportCount} báo cáo vi phạm");

        var warningMessage = hasUsage
            ? $"Nhân viên đang có {string.Join(", ", usageDetails)}. Nếu xóa sẽ mất thông tin này."
            : "Nhân viên chưa có dữ liệu nghiệp vụ.";

        return new
        {
            hasUsage,
            message = warningMessage,
            orderCount,
            invoiceCount,
            shiftCount,
            stockTicket = new
            {
                createdCount = stockTicketCreatedCount,
                confirmedCount = stockTicketConfirmedCount,
                cancelledCount = stockTicketCancelledCount,
                totalCount = stockTicketTotalCount
            },
            violationReportCount
        };
    }

    public async Task<Dictionary<string, List<string>>> GetRolePermissionsAsync()
    {
        var roles = await _context.Roles
            .Include(r => r.Permissions)
            .Where(r => r.RoleName == "SalesStaff" || r.RoleName == "InventoryStaff"
                || r.RoleName == "Sales Staff" || r.RoleName == "Inventory Staff")
            .ToListAsync();

        var result = new Dictionary<string, List<string>>();
        foreach (var role in roles)
        {
            var key = role.RoleName switch
            {
                "Sales Staff" => "SalesStaff",
                "Inventory Staff" => "InventoryStaff",
                _ => role.RoleName
            };
            if (!result.ContainsKey(key))
                result[key] = role.Permissions.Select(p => p.PermissionCode).ToList();
            else
            {
                foreach (var code in role.Permissions.Select(p => p.PermissionCode))
                    if (!result[key].Contains(code)) result[key].Add(code);
            }
        }
        return result;
    }

    public async Task<List<object>> GetAvailablePermissionsAsync()
    {
        return await _context.Permissions
            .OrderBy(p => p.PermissionCode)
            .Select(p => (object)new
            {
                p.PermissionId,
                p.PermissionCode,
                p.PermissionName
            })
            .ToListAsync();
    }

    public async Task<OwnerStaffDto> CreateStaffAsync(CreateStaffWithPermissionRequest dto, Guid ownerId, Guid branchId)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.FullName) ||
            string.IsNullOrWhiteSpace(dto.Password))
        {
            throw new BusinessException("Thông tin bắt buộc bị thiếu: Username, Email, FullName, Password.");
        }

        if (await IsEmailTakenAsync(dto.Email))
            throw new BusinessException("Email đã được sử dụng.");

        if (await IsUsernameTakenInBranchAsync(dto.Username, branchId))
            throw new BusinessException($"Username '{dto.Username}' đã tồn tại trong chi nhánh này.");

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber) && await IsPhoneTakenAsync(dto.PhoneNumber))
            throw new BusinessException("Số điện thoại đã được sử dụng.");

        List<string> permissionCodes;
        Role? staffRole = null;

        if (!string.IsNullOrWhiteSpace(dto.DefaultRoleType))
        {
            var validRoles = new[] { "SalesStaff", "InventoryStaff" };
            if (!validRoles.Contains(dto.DefaultRoleType.Trim(), StringComparer.OrdinalIgnoreCase))
                throw new BusinessException("DefaultRoleType không hợp lệ. Chỉ được phép: SalesStaff, InventoryStaff.");

            staffRole = await GetOrCreateRoleAsync(dto.DefaultRoleType);
            await _context.Entry(staffRole).Collection(r => r.Permissions).LoadAsync();

            var rolePermissionCodes = staffRole.Permissions
                .Select(p => p.PermissionCode)
                .ToList();

            permissionCodes = (dto.CustomPermissionCodes != null && dto.CustomPermissionCodes.Any())
                ? dto.CustomPermissionCodes
                : rolePermissionCodes;
        }
        else
        {
            permissionCodes = dto.CustomPermissionCodes ?? new List<string>();
        }

        var (validPermissions, missingCodes) = await ValidatePermissionsAsync(permissionCodes);
        if (missingCodes.Any())
            throw new BusinessException($"Một số mã quyền không tồn tại: {string.Join(", ", missingCodes)}");

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Username = dto.Username.Trim(),
                Email = dto.Email.Trim().ToLower(),
                FullName = dto.FullName.Trim(),
                PhoneNumber = dto.PhoneNumber?.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password.Trim()),
                IsActive = 1,
                IsVerified = true,
                Status = "ACTIVE",
                CreatedAt = now,
                CreatedBy = ownerId
            };

            _context.Users.Add(user);

            if (staffRole != null)
                user.Roles.Add(staffRole);

            _context.StaffAssignments.Add(new StaffAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = user.UserId,
                BranchId = branchId,
                AssignedAt = now
            });

            foreach (var permission in validPermissions)
            {
                _context.UserPermissions.Add(new UserPermission
                {
                    UserPermissionId = Guid.NewGuid(),
                    UserId = user.UserId,
                    PermissionId = permission.PermissionId
                });
            }

            await _context.SaveChangesAsync();

            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE users SET \"DefaultBranchId\" = {0} WHERE user_id = {1}",
                branchId, user.UserId);

            await transaction.CommitAsync();

            var createdUser = await _context.Users
                .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
                .Include(u => u.Roles).ThenInclude(r => r.Permissions)
                .FirstAsync(u => u.UserId == user.UserId);

            return await MapToDto(createdUser);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> UpdateStaffAsync(Guid id, UpdateStaffPermissionRequest dto, Guid branchId)
    {
        var isInBranch = await IsUserInBranchAsync(id, branchId);
        if (!isInBranch) throw new KeyNotFoundException("Nhân viên không tồn tại hoặc không thuộc quyền quản lý.");

        var staff = await _context.Users
            .Include(u => u.UserPermissions)
            .Include(u => u.Roles).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (staff == null) throw new KeyNotFoundException("Nhân viên không tồn tại.");

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            if (await IsEmailTakenAsync(dto.Email, staff.UserId))
                throw new BusinessException("Email đã được sử dụng bởi tài khoản khác.");
            staff.Email = dto.Email.Trim().ToLower();
        }

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            if (await IsPhoneTakenAsync(dto.PhoneNumber, staff.UserId))
                throw new BusinessException("Số điện thoại đã được sử dụng bởi tài khoản khác.");
            staff.PhoneNumber = dto.PhoneNumber.Trim();
        }

        if (!string.IsNullOrWhiteSpace(dto.FullName))
            staff.FullName = dto.FullName.Trim();

        if (dto.IsActive.HasValue)
            staff.IsActive = dto.IsActive.Value;

        if (!string.IsNullOrWhiteSpace(dto.Password))
            staff.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password.Trim());

        if (dto.PermissionCodes != null)
        {
            var (validPermissions, missingCodes) = await ValidatePermissionsAsync(dto.PermissionCodes);

            if (missingCodes.Any())
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] Permission codes not found: {string.Join(", ", missingCodes)}");
                validPermissions = validPermissions.Where(p => !missingCodes.Contains(p.PermissionCode, StringComparer.OrdinalIgnoreCase)).ToList();
            }

            var rolePermissionCodes = staff.Roles
                .SelectMany(r => r.Permissions)
                .Select(p => p.PermissionCode)
                .ToHashSet();

            var requestedCodes = validPermissions.Select(p => p.PermissionCode).ToHashSet();

            var actionToViewPrefixes = new Dictionary<string, string[]>
            {
                { "STOCK_CHECK_VIEW", new[] { "STOCK_CHECK_" } },
                { "PRODUCT_VIEW", new[] { "PRODUCT_" } },
                { "SALE_VIEW", new[] { "SALE_", "CUSTOMER_", "PAYMENT_" } },
                { "SUPPLIER_VIEW", new[] { "SUPPLIER_" } },
                { "EXPENSE_VIEW", new[] { "EXPENSE_" } },
                { "STAFF_VIEW", new[] { "STAFF_" } },
            };

            foreach (var kvp in actionToViewPrefixes)
            {
                var viewCode = kvp.Key;
                var actionPrefixes = kvp.Value;
                if (!requestedCodes.Contains(viewCode) &&
                    requestedCodes.Any(rc => actionPrefixes.Any(ap => rc.StartsWith(ap))))
                {
                    var viewPerm = await _context.Permissions.FirstOrDefaultAsync(p => p.PermissionCode == viewCode);
                    if (viewPerm != null)
                    {
                        validPermissions.Add(viewPerm);
                        requestedCodes.Add(viewCode);
                    }
                }
            }

            _context.UserPermissions.RemoveRange(staff.UserPermissions);

            foreach (var permission in validPermissions)
            {
                _context.UserPermissions.Add(new UserPermission
                {
                    UserPermissionId = Guid.NewGuid(),
                    UserId = staff.UserId,
                    PermissionId = permission.PermissionId,
                    IsGranted = true
                });
            }

            var viewToActionPrefixes = new Dictionary<string, string[]>
            {
                { "STOCK_CHECK_VIEW", new[] { "STOCK_CHECK_" } },
                { "PRODUCT_VIEW", new[] { "PRODUCT_" } },
                { "SALE_VIEW", new[] { "SALE_", "CUSTOMER_", "PAYMENT_" } },
                { "SUPPLIER_VIEW", new[] { "SUPPLIER_" } },
                { "EXPENSE_VIEW", new[] { "EXPENSE_" } },
                { "STAFF_VIEW", new[] { "STAFF_" } },
            };

            foreach (var rolePermCode in rolePermissionCodes.Where(c => !requestedCodes.Contains(c)))
            {
                var isViewBeingDenied = viewToActionPrefixes.TryGetValue(rolePermCode, out var actionPrefixes);
                var hasRelatedAction = isViewBeingDenied && actionPrefixes != null &&
                    requestedCodes.Any(rc => actionPrefixes.Any(ap => rc.StartsWith(ap)));

                if (hasRelatedAction)
                    continue;

                var perm = await _context.Permissions.FirstOrDefaultAsync(p => p.PermissionCode == rolePermCode);
                if (perm != null)
                {
                    _context.UserPermissions.Add(new UserPermission
                    {
                        UserPermissionId = Guid.NewGuid(),
                        UserId = staff.UserId,
                        PermissionId = perm.PermissionId,
                        IsGranted = false
                    });
                }
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> ToggleStaffStatusAsync(Guid id, Guid ownerId, Guid branchId)
    {
        var isInBranch = await IsUserInBranchAsync(id, branchId);
        if (!isInBranch) throw new KeyNotFoundException("Nhân viên không tồn tại hoặc không thuộc chi nhánh này.");

        var staff = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id && u.Status != "DELETED");

        if (staff == null) throw new KeyNotFoundException("Nhân viên đã bị xóa hoặc không tồn tại.");

        if (staff.UserId == ownerId)
            throw new BusinessException("Bạn không thể tự khóa/mở khóa tài khoản của chính mình.");

        staff.IsActive = staff.IsActive == 1 ? (short)0 : (short)1;
        staff.Status = staff.IsActive == 1 ? "ACTIVE" : "INACTIVE";

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string Message, object? Data)> DeleteStaffAsync(Guid id, Guid ownerId, Guid branchId)
    {
        var isInBranch = await IsUserInBranchAsync(id, branchId);
        if (!isInBranch) throw new KeyNotFoundException("Nhân viên không tồn tại hoặc không thuộc chi nhánh này.");

        var staff = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id && u.Status != "DELETED");

        if (staff == null) throw new KeyNotFoundException("Nhân viên không tồn tại.");

        if (staff.UserId == ownerId)
            throw new BusinessException("Bạn không thể tự xóa tài khoản của chính mình.");

        var orderCount = await _context.Orders.CountAsync(o => o.UserId == id);
        var invoiceCount = await _context.Invoices.CountAsync(i => i.UserId == id);
        var shiftCount = await _context.Shifts.CountAsync(s => s.UserId == id);
        var stockTicketCreatedCount = await _context.StockTickets.CountAsync(s => s.UserId == id);
        var stockTicketConfirmedCount = await _context.StockTickets.CountAsync(s => s.ConfirmedBy == id);
        var stockTicketCancelledCount = await _context.StockTickets.CountAsync(s => s.CancelledBy == id);
        var violationReportCount = 0;

        var hasUsage = orderCount > 0 || invoiceCount > 0 || shiftCount > 0
            || stockTicketCreatedCount > 0 || stockTicketConfirmedCount > 0 || stockTicketCancelledCount > 0
            || violationReportCount > 0;

        string? warningMessage = null;
        if (hasUsage)
        {
            var usageDetails = new List<string>();
            if (orderCount > 0) usageDetails.Add($"{orderCount} đơn hàng");
            if (invoiceCount > 0) usageDetails.Add($"{invoiceCount} hóa đơn");
            if (shiftCount > 0) usageDetails.Add($"{shiftCount} ca làm việc");
            if (stockTicketCreatedCount > 0) usageDetails.Add($"{stockTicketCreatedCount} phiếu kho (người tạo)");
            if (stockTicketConfirmedCount > 0) usageDetails.Add($"{stockTicketConfirmedCount} phiếu kho (người xác nhận)");
            if (stockTicketCancelledCount > 0) usageDetails.Add($"{stockTicketCancelledCount} phiếu kho (người hủy)");
            if (violationReportCount > 0) usageDetails.Add($"{violationReportCount} báo cáo vi phạm");

            warningMessage = $"Cảnh báo: Nhân viên đang có {string.Join(", ", usageDetails)}. Thông tin này sẽ được giữ lại nhưng không thể khôi phục.";
        }

        staff.Status = "DELETED";
        staff.IsActive = 0;
        staff.IsVerified = false;
        staff.DeletedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var shortId = staff.UserId.ToString().Substring(0, 6);
        var newEmail = $"del_{shortId}_{staff.Email}";
        if (newEmail.Length > 150) newEmail = newEmail.Substring(0, 150);
        staff.Email = newEmail;

        var newUsername = $"del_{shortId}_{staff.Username}";
        if (newUsername.Length > 50) newUsername = newUsername.Substring(0, 50);
        staff.Username = newUsername;

        staff.PhoneNumber = null;
        await _context.SaveChangesAsync();

        var data = hasUsage ? (object)new
        {
            warning = warningMessage,
            deletedStaffId = staff.UserId,
            deletedStaffName = staff.FullName
        } : null;

        return (true, $"Đã xóa mềm nhân viên '{staff.FullName}'.", data);
    }

    public async Task<List<DeletedStaffDto>> GetDeletedStaffsAsync(Guid branchId, Guid ownerId)
    {
        var retentionDays = _configuration.GetValue<int>("AccountSettings:RetentionDays", 15);

        var staffUserIds = await _context.StaffAssignments
            .Where(sa => sa.BranchId == branchId)
            .Select(sa => sa.UserId)
            .ToListAsync();

        var deletedUsers = await _context.Users
            .Where(u => (staffUserIds.Contains(u.UserId) || u.CreatedBy == ownerId)
                && u.Status == "DELETED"
                && u.DeletedAt != null
                && u.DeletedAt > DateTime.UtcNow.AddDays(-retentionDays))
            .ToListAsync();

        return deletedUsers.Select(u => new DeletedStaffDto
        {
            UserId = u.UserId,
            FullName = u.FullName,
            Email = u.Email,
            PhoneNumber = u.PhoneNumber,
            DeletedAt = u.DeletedAt,
            DaysUntilPermanentDelete = u.DeletedAt.HasValue
                ? Math.Max(0, retentionDays - (int)(DateTime.UtcNow - u.DeletedAt.Value).TotalDays)
                : retentionDays
        }).ToList();
    }

    public async Task<bool> RestoreStaffAsync(Guid id, Guid branchId)
    {
        var isInBranch = await IsUserInBranchAsync(id, branchId);
        if (!isInBranch) throw new KeyNotFoundException("Nhân viên không tồn tại hoặc không thuộc chi nhánh này.");

        var staff = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id && u.Status == "DELETED");

        if (staff == null) throw new KeyNotFoundException("Nhân viên không tồn tại hoặc chưa bị xóa.");

        staff.Status = "ACTIVE";
        staff.IsActive = 1;
        staff.IsVerified = true;
        staff.DeletedAt = null;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> PermanentDeleteStaffAsync(Guid id, Guid branchId)
    {
        var isInBranch = await IsUserInBranchAsync(id, branchId);
        if (!isInBranch) throw new KeyNotFoundException("Nhân viên không tồn tại hoặc không thuộc chi nhánh này.");

        var staff = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id
                && (u.Status == "DELETED" || u.Status == "INACTIVE" || u.Status == "PERMANENT_DELETED"));

        if (staff == null) throw new KeyNotFoundException("Nhân viên không tồn tại hoặc chưa bị xóa.");

        var originalName = staff.FullName;

        staff.FullName = $"{originalName} (Đã xóa)";
        staff.Email = $"deleted_{Guid.NewGuid()}@mep.deleted";
        var shortId = staff.UserId.ToString().Substring(0, 6);
        var newUsername = $"del_{shortId}_{staff.Username}";
        if (newUsername.Length > 50) newUsername = newUsername.Substring(0, 50);
        staff.Username = newUsername;
        staff.PhoneNumber = null;
        staff.PasswordHash = "PERMANENTLY_DELETED";
        staff.Status = "PERMANENT_DELETED";
        staff.IsActive = 0;
        staff.DeletedAt = null;

        await _context.SaveChangesAsync();
        return true;
    }
}
