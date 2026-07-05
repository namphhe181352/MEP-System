using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    public OwnerStaffController(MEPDatabaseContext context)
    {
        _context = context;
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
    /// Chuẩn hóa OwnerStaffDto từ User entity.
    /// </summary>
    private static OwnerStaffDto MapToDto(User user)
    {
        return new OwnerStaffDto
        {
            UserId = user.UserId,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            BranchId = null, // Hệ thống 1 chi nhánh, tạm thời không dùng
            BranchName = "Chi nhánh chính", // Gán cứng tên hiển thị nếu Frontend yêu cầu
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
    /// Lấy danh sách nhân viên do Owner tạo.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStaffs(
        [FromQuery] string? search,
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

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Users
            .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .Include(u => u.Roles)
            .Where(u => u.CreatedBy == ownerId.Value)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = items.Select(u => MapToDto(u)).ToList();

        return Ok(new ApiResponse<PaginatedResponse<OwnerStaffDto>>
        {
            Success = true,
            Message = "Danh sách nhân viên.",
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

        var staff = await _context.Users
            .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u =>
                u.UserId == id && u.CreatedBy == ownerId.Value);

        if (staff == null)
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Nhân viên không tồn tại hoặc không thuộc quyền quản lý của bạn."
            });

        return Ok(new ApiResponse<OwnerStaffDto>
        {
            Success = true,
            Message = "Chi tiết nhân viên.",
            Data = MapToDto(staff)
        });
    }

    #endregion

    #region POST /api/owner/staffs

    /// <summary>
    /// Tạo nhân viên mới (Không gán chi nhánh).
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

        if (await IsUsernameTakenAsync(dto.Username))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Username đã tồn tại."
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

            // ĐÃ BỎ: Logic nạp dữ liệu vào bảng StaffAssignments

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
                    Data = MapToDto(createdUser)
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

            _context.UserPermissions.RemoveRange(staff.UserPermissions);

            foreach (var permission in validPermissions)
            {
                _context.UserPermissions.Add(new UserPermission
                {
                    UserPermissionId = Guid.NewGuid(),
                    UserId = staff.UserId,
                    PermissionId = permission.PermissionId
                });
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

        var staff = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == id && u.CreatedBy == ownerId.Value);

        if (staff == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Nhân viên không tồn tại hoặc không thuộc quyền quản lý của bạn." });

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
    /// Xóa hoàn toàn nhân viên ra khỏi hệ thống (Xóa cứng trong DB).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteStaff(Guid id)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không thể xác định người dùng từ token." });

        var staff = await _context.Users
            .Include(u => u.UserPermissions)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == id && u.CreatedBy == ownerId.Value);

        if (staff == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Nhân viên không tồn tại hoặc không thuộc quyền quản lý của bạn." });

        if (staff.UserId == ownerId.Value)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Bạn không thể tự xóa tài khoản của chính mình." });

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (staff.UserPermissions.Any()) _context.UserPermissions.RemoveRange(staff.UserPermissions);
            if (staff.Roles.Any()) staff.Roles.Clear();

            var assignments = await _context.StaffAssignments.Where(sa => sa.UserId == staff.UserId).ToListAsync();
            if (assignments.Any()) _context.StaffAssignments.RemoveRange(assignments);

            _context.Users.Remove(staff);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new ApiResponse<object> { Success = true, Message = $"Đã xóa hoàn toàn nhân viên '{staff.FullName}' ra khỏi hệ thống." });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không thể xóa cứng nhân viên này vì tài khoản đã phát sinh dữ liệu nghiệp vụ (Hóa đơn, phiếu kho...). Vui lòng chọn giải pháp Tắt hoạt động.",
                Errors = new List<string> { ex.Message }
            });
        }
    }
    #endregion
}