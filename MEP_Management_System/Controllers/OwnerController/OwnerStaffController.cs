using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MEP_Management_System.Controllers.OwnerController;

/// <summary>
/// Controller quản lý nhân viên dành riêng cho Owner (Hệ thống đơn chi nhánh).
/// </summary>
[ApiController]
[Route("api/owner/staffs")]
[Tags("Quản lý Nhân viên (Owner)")]
[Authorize(Roles = PermissionConstants.OWNER_ROLE)]
public class OwnerStaffController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly ICurrentBranchAccessor _currentBranch;
    private readonly IConfiguration _configuration;

    public OwnerStaffController(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch, IConfiguration configuration)
    {
        _context = context;
        _currentBranch = currentBranch;
        _configuration = configuration;
    }

    #region Private Helper Methods

    /// <summary>
    /// Lấy UserId của Owner hiện tại từ JWT token.
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("userId")?.Value;

        if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var userId))
            return null;

        return userId;
    }

    /// <summary>
    /// Lấy BranchId từ JWT token.
    /// </summary>
    private Guid? GetCurrentBranchId()
    {
        return _currentBranch.GetBranchId();
    }

    /// <summary>
    /// Chuẩn hóa OwnerStaffDto từ User entity.
    /// </summary>
    private async Task<OwnerStaffDto> MapToDto(User user)
    {
        return new OwnerStaffDto
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            PermissionCodes = user.UserPermissions
                .Select(up => up.Permission?.PermissionCode ?? "")
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList(),
            Roles = user.Roles
                .Select(r => r.RoleName)
                .ToList()
        };
    }

    private async Task<bool> IsEmailTakenAsync(string email, Guid? excludeUserId = null)
    {
        var normalized = email.Trim().ToLower();
        var query = _context.Users.Where(u => u.Email.ToLower() == normalized);

        if (excludeUserId.HasValue)
            query = query.Where(u => u.UserId != excludeUserId.Value);

        return await query.AnyAsync();
    }

    private async Task<bool> IsUsernameTakenAsync(string username)
    {
        return await _context.Users.AnyAsync(u =>
            u.Username.ToLower() == username.Trim().ToLower());
    }

    private async Task<bool> IsUsernameTakenInBranchAsync(string username, Guid branchId)
    {
        // Check if any user with this username is assigned to the same branch
        return await _context.StaffAssignments
            .Include(sa => sa.User)
            .AnyAsync(sa => sa.BranchId == branchId &&
                sa.User.Username.ToLower() == username.Trim().ToLower());
    }

    private async Task<bool> IsPhoneTakenAsync(string phone, Guid? excludeUserId = null)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;

        var normalized = phone.Trim();
        var query = _context.Users.Where(u => u.PhoneNumber == normalized);

        if (excludeUserId.HasValue)
            query = query.Where(u => u.UserId != excludeUserId.Value);

        return await query.AnyAsync();
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

    #endregion

    #region GET /api/owner/staffs

    /// <summary>
    /// Lấy danh sách nhân viên của chi nhánh hiện tại.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStaffs(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] bool? isDeleted = false,
        [FromQuery] string? view = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không thể xác định người dùng từ token."
            });

        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh."
            });

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Lấy danh sách user thuộc chi nhánh hiện tại
        var staffUserIds = await _context.StaffAssignments
            .Where(sa => sa.BranchId == branchId.Value)
            .Select(sa => sa.UserId)
            .ToListAsync();

        // view=all | active (default) | hidden | deleted
        // - all: Không lọc theo status
        // - active: Status = ACTIVE
        // - hidden: Status = INACTIVE (đã khóa)
        // - deleted: Status = DELETED
        string? statusFilter = null;
        string viewMessage;

        if (string.Equals(view, "all", StringComparison.OrdinalIgnoreCase))
        {
            statusFilter = null;
            viewMessage = "Danh sách tất cả nhân viên.";
        }
        else if (string.Equals(view, "hidden", StringComparison.OrdinalIgnoreCase))
        {
            statusFilter = "INACTIVE";
            viewMessage = "Danh sách nhân viên đã khóa.";
        }
        else if (string.Equals(view, "deleted", StringComparison.OrdinalIgnoreCase))
        {
            statusFilter = "DELETED";
            viewMessage = "Danh sách nhân viên đã xóa.";
        }
        else
        {
            statusFilter = "ACTIVE";
            viewMessage = "Danh sách nhân viên đang hoạt động.";
        }

        var query = _context.Users
            .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .Include(u => u.Roles)
            .Where(u => staffUserIds.Contains(u.UserId)
                && u.UserId != ownerId.Value
                // Loại trừ Admin/Owner - chỉ hiển thị staff (Sale, Kho, etc.)
                && !u.Roles.Any(r => r.RoleName == "Admin" || r.RoleName == "Owner"))
            .AsQueryable();

        if (statusFilter != null)
        {
            query = query.Where(u => u.Status == statusFilter);
        }

        // isDeleted override chỉ áp dụng nếu muốn xem tất cả (backward compatible)
        if (isDeleted == true)
        {
            query = _context.Users
                .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
                .Include(u => u.Roles)
                .Where(u => staffUserIds.Contains(u.UserId)
                    && u.Status == "DELETED"
                    && u.UserId != ownerId.Value
                    && !u.Roles.Any(r => r.RoleName == "Admin" || r.RoleName == "Owner"))
                .AsQueryable();
            viewMessage = "Danh sách nhân viên đã xóa.";
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == (isActive.Value ? (short)1 : (short)0));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = new List<OwnerStaffDto>();
        foreach (var u in items)
        {
            dtos.Add(await MapToDto(u));
        }

        return Ok(new ApiResponse<PaginatedResponse<OwnerStaffDto>>
        {
            Success = true,
            Message = viewMessage,
            Data = new PaginatedResponse<OwnerStaffDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                HasNextPage = page * pageSize < totalCount,
                HasPreviousPage = page > 1
            }
        });
    }

    #endregion

    #region GET /api/owner/staffs/{id}/check-relations
    /// <summary>
    /// Kiểm tra quan hệ dữ liệu của nhân viên trước khi xóa.
    /// </summary>
    /// <remarks>
    /// NOTE: Owner luôn pass permission check (xem PermissionAuthorizationHandler).
    /// HasPermission đặt ở đây vì:
    /// 1. Audit trail - ghi log ai check relations
    /// 2. Intent documentation - thể hiện rõ đây là bước trước khi xóa
    /// 3. Security-in-depth - phòng trường hợp user không phải Owner gọi trực tiếp qua URL
    /// </remarks>
    [HttpGet("{id:guid}/check-relations")]
    [HasPermission(PermissionConstants.STAFF_DELETE)]
    public async Task<IActionResult> CheckStaffRelations(Guid id)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không thể xác định người dùng từ token."
            });

        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh."
            });

        // Kiểm tra nhân viên có thuộc chi nhánh hiện tại không
        var isInBranch = await _context.StaffAssignments
            .AnyAsync(sa => sa.UserId == id && sa.BranchId == branchId.Value);

        if (!isInBranch)
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Nhân viên không tồn tại hoặc không thuộc chi nhánh này."
            });

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

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = message,
            Data = new
            {
                hasRelations,
                invoiceCount,
                orderCount,
                returnOrderCount,
                purchaseOrderCount,
                shiftCount,
                stockTicketCount
            }
        });
    }
    #endregion

    #region GET /api/owner/staffs/{id}/check-usage
    /// <summary>
    /// Kiểm tra xem nhân viên có đang liên kết với các bảng dữ liệu nào không.
    /// </summary>
    [HttpGet("{id:guid}/check-usage")]
    [HasPermission(PermissionConstants.STAFF_DELETE)]
    public async Task<IActionResult> CheckStaffUsage(Guid id)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không thể xác định người dùng từ token."
            });

        var staff = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id && u.CreatedBy == ownerId.Value);

        if (staff == null)
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Nhân viên không tồn tại hoặc không thuộc quyền quản lý của bạn."
            });

        // Đếm số lượng liên kết trong các bảng
        var orderCount = await _context.Orders.CountAsync(o => o.UserId == id);
        var invoiceCount = await _context.Invoices.CountAsync(i => i.UserId == id);
        var shiftCount = await _context.Shifts.CountAsync(s => s.UserId == id);

        // StockTickets: UserId (người tạo), ConfirmedBy (người xác nhận), CancelledBy (người hủy)
        var stockTicketCreatedCount = await _context.StockTickets.CountAsync(s => s.UserId == id);
        var stockTicketConfirmedCount = await _context.StockTickets.CountAsync(s => s.ConfirmedBy == id);
        var stockTicketCancelledCount = await _context.StockTickets.CountAsync(s => s.CancelledBy == id);
        var stockTicketTotalCount = stockTicketCreatedCount + stockTicketConfirmedCount + stockTicketCancelledCount;

        var violationReportCount = 0;

        var hasUsage = orderCount > 0 || invoiceCount > 0 || shiftCount > 0
            || stockTicketTotalCount > 0 || violationReportCount > 0;

        // Tạo thông báo chi tiết
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

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = warningMessage,
            Data = new
            {
                hasUsage,
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
            }
        });
    }
    #endregion

    #region GET /api/owner/staffs/available-permissions

    /// <summary>
    /// Lấy danh sách quyền hạn khả dụng từ DB.
    /// </summary>
    [HttpGet("available-permissions")]
    public async Task<IActionResult> GetAvailablePermissions()
    {
        var permissions = await _context.Permissions
            .OrderBy(p => p.PermissionCode)
            .Select(p => new
            {
                p.PermissionId,
                p.PermissionCode,
                p.PermissionName
            })
            .ToListAsync();

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Danh sách quyền hạn khả dụng trong hệ thống.",
            Data = permissions
        });
    }

    #endregion

    #region GET /api/owner/staffs/{id}

    /// <summary>
    /// Lấy chi tiết một nhân viên.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStaffById(Guid id)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không thể xác định người dùng từ token."
            });

        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh."
            });

        // Kiểm tra nhân viên có thuộc chi nhánh hiện tại không
        var isInBranch = await _context.StaffAssignments
            .AnyAsync(sa => sa.UserId == id && sa.BranchId == branchId.Value);

        if (!isInBranch)
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Nhân viên không tồn tại hoặc không thuộc chi nhánh này."
            });

        var staff = await _context.Users
            .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (staff == null)
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Nhân viên không tồn tại."
            });

        return Ok(new ApiResponse<OwnerStaffDto>
        {
            Success = true,
            Message = "Chi tiết nhân viên.",
            Data = await MapToDto(staff)
        });
    }

    #endregion

    #region POST /api/owner/staffs

    /// <summary>
    /// Tạo nhân viên mới - Tự động gán chi nhánh của Owner.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffWithPermissionRequest dto)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không thể xác định người dùng từ token."
            });

        // Lấy branchId hiện tại (từ Header/Token) để gán cho nhân viên mới
        var currentBranchId = GetCurrentBranchId();
        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không xác định được chi nhánh hiện tại. Vui lòng chọn chi nhánh."
            });
        }
        
        var ownerBranchId = currentBranchId.Value;

        if (string.IsNullOrWhiteSpace(dto.Username) ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.FullName) ||
            string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Thông tin bắt buộc bị thiếu.",
                Errors = new List<string> { "Username, Email, FullName, Password là bắt buộc." }
            });
        }

        if (await IsEmailTakenAsync(dto.Email))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Email đã được sử dụng."
            });
        }

        // Check username uniqueness within the branch
        if (await IsUsernameTakenInBranchAsync(dto.Username, ownerBranchId))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = $"Username '{dto.Username}' đã tồn tại trong chi nhánh này."
            });
        }

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber) && await IsPhoneTakenAsync(dto.PhoneNumber))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Số điện thoại đã được sử dụng."
            });
        }

        List<string> permissionCodes;
        Role? staffRole = null;

        if (!string.IsNullOrWhiteSpace(dto.DefaultRoleType))
        {
            var validRoles = new[] { "SalesStaff", "InventoryStaff", "Staff" };
            if (!validRoles.Contains(dto.DefaultRoleType.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "DefaultRoleType không hợp lệ.",
                    Errors = new List<string> { "Chỉ được phép: SalesStaff, InventoryStaff, Staff." }
                });
            }

            var roleNameToSearch = dto.DefaultRoleType;
            if (roleNameToSearch.Equals("SalesStaff", StringComparison.OrdinalIgnoreCase))
                roleNameToSearch = "Sales Staff";
            if (roleNameToSearch.Equals("InventoryStaff", StringComparison.OrdinalIgnoreCase))
                roleNameToSearch = "Inventory Staff";

            staffRole = await GetOrCreateRoleAsync(roleNameToSearch);
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
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Một số mã quyền không tồn tại.",
                Errors = missingCodes.Select(c => $"PermissionCode không tồn tại: {c}").ToList()
            });
        }

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
                CreatedBy = ownerId.Value
            };

            _context.Users.Add(user);

            if (staffRole != null)
            {
                user.Roles.Add(staffRole);
            }

            // Tự động gán nhân viên vào chi nhánh của Owner
            _context.StaffAssignments.Add(new StaffAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = user.UserId,
                BranchId = ownerBranchId,
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

            // Dùng raw SQL để update DefaultBranchId vì EF không map đúng
            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE users SET \"DefaultBranchId\" = {0} WHERE user_id = {1}",
                ownerBranchId, user.UserId);

            await transaction.CommitAsync();

            var createdUser = await _context.Users
                .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
                .Include(u => u.Roles)
                .FirstAsync(u => u.UserId == user.UserId);

            return CreatedAtAction(nameof(GetStaffById), new { id = user.UserId },
                new ApiResponse<OwnerStaffDto>
                {
                    Success = true,
                    Message = "Tạo nhân viên thành công.",
                    Data = await MapToDto(createdUser)
                });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    #endregion

    #region PUT /api/owner/staffs/{id}

    /// <summary>
    /// Cập nhật thông tin nhân viên (Không đổi chi nhánh).
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateStaff(Guid id, [FromBody] UpdateStaffPermissionRequest dto)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không thể xác định người dùng từ token."
            });

        var staff = await _context.Users
            .Include(u => u.UserPermissions)
            .Include(u => u.Roles)
            .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u =>
                u.UserId == id && u.CreatedBy == ownerId.Value);

        if (staff == null)
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Nhân viên không tồn tại hoặc không thuộc quyền quản lý."
            });

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            if (await IsEmailTakenAsync(dto.Email, staff.UserId))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Email đã được sử dụng bởi tài khoản khác."
                });
            }
            staff.Email = dto.Email.Trim().ToLower();
        }

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            if (await IsPhoneTakenAsync(dto.PhoneNumber, staff.UserId))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Số điện thoại đã được sử dụng bởi tài khoản khác."
                });
            }
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
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Một số mã quyền không tồn tại.",
                    Errors = missingCodes.Select(c => $"PermissionCode không tồn tại: {c}").ToList()
                });
            }

            // Get role permission codes for this staff
            var rolePermissionCodes = staff.Roles
                .SelectMany(r => r.Permissions)
                .Select(p => p.PermissionCode)
                .ToHashSet();

            var requestedCodes = validPermissions.Select(p => p.PermissionCode).ToHashSet();

            // Remove all existing user permissions
            _context.UserPermissions.RemoveRange(staff.UserPermissions);

            // Add explicit allows (IsGranted = true) for requested permissions
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

            // Add explicit denies (IsGranted = false) for role permissions NOT in the requested list
            foreach (var rolePermCode in rolePermissionCodes.Where(c => !requestedCodes.Contains(c)))
            {
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

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Cập nhật nhân viên thành công."
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    #endregion

    #region PATCH /api/owner/staffs/{id}/toggle-status
    /// <summary>
    /// Thay đổi trạng thái hoạt động (Bật/Tắt) của tài khoản nhân viên.
    /// </summary>
    [HttpPatch("{id:guid}/toggle-status")]
    public async Task<IActionResult> ToggleStaffStatus(Guid id)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không thể xác định người dùng từ token." });

        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Không tìm thấy thông tin chi nhánh." });

        var isInBranch = await _context.StaffAssignments
            .AnyAsync(sa => sa.UserId == id && sa.BranchId == branchId.Value);

        if (!isInBranch)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Nhân viên không tồn tại hoặc không thuộc chi nhánh này." });

        var staff = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id && u.Status != "DELETED");

        if (staff == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Nhân viên đã bị xóa hoặc không tồn tại." });

        if (staff.UserId == ownerId.Value)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Bạn không thể tự khóa/mở khóa tài khoản của chính mình." });

        try
        {
            // Thực hiện đảo trạng thái IsActive (Nếu là 1 thì thành 0, nếu là 0 thì thành 1)
            staff.IsActive = staff.IsActive == 1 ? (short)0 : (short)1;

            // Đồng bộ cập nhật thêm cột Status cho đồng nhất logic nghiệp vụ
            staff.Status = staff.IsActive == 1 ? "ACTIVE" : "INACTIVE";

            await _context.SaveChangesAsync();

            string statusMessage = staff.IsActive == 1 ? "Kích hoạt (Mở khóa)" : "Vô hiệu hóa (Khóa)";
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = $"Đã {statusMessage} tài khoản nhân viên '{staff.FullName}' thành công."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Có lỗi xảy ra trong quá trình cập nhật trạng thái hoạt động.",
                Errors = new List<string> { ex.Message }
            });
        }
    }
    #endregion


    #region DELETE /api/owner/staffs/{id}
    /// <summary>
    /// Xóa mềm nhân viên (Soft Delete - chuyển Status = DELETED, IsActive = 0).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteStaff(Guid id)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không thể xác định người dùng từ token." });

        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Không tìm thấy thông tin chi nhánh." });

        // Kiểm tra nhân viên có thuộc chi nhánh hiện tại không
        var isInBranch = await _context.StaffAssignments
            .AnyAsync(sa => sa.UserId == id && sa.BranchId == branchId.Value);

        if (!isInBranch)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Nhân viên không tồn tại hoặc không thuộc chi nhánh này." });

        var staff = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id && u.Status != "DELETED");

        if (staff == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Nhân viên không tồn tại." });

        if (staff.UserId == ownerId.Value)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Bạn không thể tự xóa tài khoản của chính mình." });

        // Kiểm tra các liên kết dữ liệu trước khi xóa
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

        // Tạo cảnh báo nếu có liên kết
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

        // Thực hiện soft delete
        staff.Status = "DELETED";
        staff.IsActive = 0;
        staff.IsVerified = false; // Không thể login nữa
        staff.DeletedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync();

        var response = new ApiResponse<object>
        {
            Success = true,
            Message = $"Đã xóa mềm nhân viên '{staff.FullName}'."
        };

        // Thêm cảnh báo vào response nếu có
        if (hasUsage)
        {
            response.Data = new
            {
                warning = warningMessage,
                deletedStaffId = staff.UserId,
                deletedStaffName = staff.FullName
            };
        }

        return Ok(response);
    }
    #endregion

    #region GET /api/owner/staffs/deleted
    /// <summary>
    /// Lấy danh sách nhân viên đã xóa mềm của chi nhánh hiện tại.
    /// </summary>
    [HttpGet("deleted")]
    public async Task<IActionResult> GetDeletedStaffs()
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không thể xác định người dùng từ token." });

        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Không tìm thấy thông tin chi nhánh." });

        var retentionDays = _configuration.GetValue<int>("AccountSettings:RetentionDays", 15);

        // Lấy user đã xóa do owner này tạo hoặc thuộc chi nhánh
        var staffUserIds = await _context.StaffAssignments
            .Where(sa => sa.BranchId == branchId.Value)
            .Select(sa => sa.UserId)
            .ToListAsync();

        var deletedUsers = await _context.Users
            .Where(u => (staffUserIds.Contains(u.UserId) || u.CreatedBy == ownerId.Value)
                && u.Status == "DELETED"
                && u.DeletedAt != null
                && u.DeletedAt > DateTime.UtcNow.AddDays(-retentionDays)) // còn trong thời hạn retention
            .ToListAsync();

        var result = deletedUsers.Select(u => new DeletedStaffDto
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

        return Ok(new ApiResponse<List<DeletedStaffDto>>
        {
            Success = true,
            Message = "Danh sách nhân viên đã xóa.",
            Data = result
        });
    }
    #endregion

    #region POST /api/owner/staffs/{id}/restore
    /// <summary>
    /// Khôi phục nhân viên đã xóa mềm.
    /// </summary>
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreStaff(Guid id)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không thể xác định người dùng từ token." });

        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Không tìm thấy thông tin chi nhánh." });

        // Kiểm tra nhân viên có thuộc chi nhánh hiện tại không
        var isInBranch = await _context.StaffAssignments
            .AnyAsync(sa => sa.UserId == id && sa.BranchId == branchId.Value);

        if (!isInBranch)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Nhân viên không tồn tại hoặc không thuộc chi nhánh này." });

        var staff = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id && u.Status == "DELETED");

        if (staff == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Nhân viên không tồn tại hoặc chưa bị xóa." });

        // Khôi phục: đưa về trạng thái ACTIVE (kèm IsActive=1)
        staff.Status = "ACTIVE";
        staff.IsActive = 1;
        staff.IsVerified = true; // Cho phép login lại
        staff.DeletedAt = null;
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = $"Đã khôi phục nhân viên '{staff.FullName}'."
        });
    }
    #endregion

    #region DELETE /api/owner/staffs/{id}/permanent
    /// <summary>
    /// Xóa vĩnh viễn nhân viên (Anonymization - giữ User record để bảo toàn FK).
    /// Sau khi anonymize, UserId vẫn giữ nguyên nên các hoá đơn/đơn hàng vẫn trỏ đúng.
    /// </summary>
    [HttpDelete("{id:guid}/permanent")]
    public async Task<IActionResult> PermanentDeleteStaff(Guid id)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không thể xác định người dùng từ token." });

        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Không tìm thấy thông tin chi nhánh." });

        // Kiểm tra nhân viên có thuộc chi nhánh hiện tại không
        var isInBranch = await _context.StaffAssignments
            .AnyAsync(sa => sa.UserId == id && sa.BranchId == branchId.Value);

        if (!isInBranch)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Nhân viên không tồn tại hoặc không thuộc chi nhánh này." });

        var staff = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id
                && (u.Status == "DELETED" || u.Status == "INACTIVE" || u.Status == "PERMANENT_DELETED"));

        if (staff == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Nhân viên không tồn tại hoặc chưa bị xóa." });

        var originalName = staff.FullName;

        // Anonymize: xoá dữ liệu cá nhân nhưng GIỮ row để bảo toàn FK trong Orders/Invoices
        staff.FullName = $"{originalName} (Đã xóa)";
        staff.Email = $"deleted_{Guid.NewGuid()}@mep.deleted";
        staff.PhoneNumber = null;
        staff.PasswordHash = "PERMANENTLY_DELETED";
        staff.Status = "PERMANENT_DELETED";
        staff.IsActive = 0;
        staff.DeletedAt = null; // Đã xóa vĩnh viễn → không còn nằm trong retention list
        // UserId, CreatedBy, CreatedAt giữ nguyên → audit trail vẫn đầy đủ

        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = $"Đã xóa vĩnh viễn nhân viên '{originalName}'. Dữ liệu cá nhân đã được ẩn danh, các hoá đơn vẫn được giữ nguyên."
        });
    }
    #endregion
}