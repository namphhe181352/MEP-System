using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MEP_Management_System.Controllers.AdminController;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/users")]
[Tags("Admin (Admin)")]
public class AdminUsersController : ControllerBase
{
    private readonly MEPDatabaseContext _context;

    public AdminUsersController(MEPDatabaseContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /api/admin/users
    /// UC-085: View User List
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? roleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var query = _context.Users
            .Include(u => u.Roles)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(roleId) && Guid.TryParse(roleId, out var parsedRoleId))
        {
            query = query.Where(u => u.Roles.Any(r => r.RoleId == parsedRoleId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                u.Username.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserDto
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive,
                Status = u.Status,
                CreatedAt = u.CreatedAt,
                BranchId = u.DefaultBranchId,
                BranchName = _context.Branches.Where(b => b.BranchId == u.DefaultBranchId).Select(b => b.BranchName).FirstOrDefault(),
                Roles = u.Roles.Select(r => new AdminRoleDto
                {
                    RoleId = r.RoleId,
                    RoleName = r.RoleName,
                    Description = r.Description
                }).ToList()
            })
            .ToListAsync();

        return Ok(new PageResultDto<AdminUserDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// GET /api/admin/users/{id}
    /// UC-086: View User Detail
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy người dùng." } });

        var dto = new AdminUserDetailDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            IsVerified = user.IsVerified,
            SuspendedUntil = user.SuspendedUntil,
            SubscriptionPlan = user.SubscriptionPlan,
            BranchId = user.DefaultBranchId,
            BranchName = await _context.Branches.Where(b => b.BranchId == user.DefaultBranchId).Select(b => b.BranchName).FirstOrDefaultAsync(),
            Roles = user.Roles.Select(r => new AdminRoleDto
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                Description = r.Description
            }).ToList()
        };

        return Ok(dto);
    }

    /// <summary>
    /// POST /api/admin/users
    /// UC-087: Create User Account
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] AdminUserCreateDto dto)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Email là bắt buộc." } });

        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Họ tên là bắt buộc." } });

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Mật khẩu phải có ít nhất 6 ký tự." } });

        var emailTrimmed = dto.Email.Trim().ToLower();
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == emailTrimmed && u.IsActive != 0))
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-409", Message = "Email đã tồn tại." } });

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            var phoneTrimmed = dto.PhoneNumber.Trim();
            if (await _context.Users.AnyAsync(u => u.PhoneNumber == phoneTrimmed && u.IsActive != 0))
                return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-409", Message = "Số điện thoại đã tồn tại." } });
        }

        // Get current admin user ID to check for role security
        var currentAdminId = GetCurrentUserId();

        // Validate RoleIds - check if roles exist and prevent assigning Admin role
        var validRoles = new List<Role>();
        if (dto.RoleIds != null && dto.RoleIds.Any())
        {
            var roles = await _context.Roles.Where(r => dto.RoleIds.Contains(r.RoleId)).ToListAsync();

            // Check if trying to assign Admin role
            var adminRole = roles.FirstOrDefault(r => r.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase));
            if (adminRole != null)
            {
                return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-403", Message = "Không thể gán vai trò Admin." } });
            }

            // Block Owner role creation via this endpoint
            if (roles.Any(r => r.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-403", Message = "Vui lòng dùng endpoint POST /api/admin/accounts/owners để tạo Owner." } });

            validRoles = roles;
        }

        // Resolve branch: sử dụng BranchId được cung cấp hoặc lấy branch đầu tiên
        Guid? resolvedBranchId = dto.BranchId;
        if (resolvedBranchId == null || resolvedBranchId == Guid.Empty)
        {
            var firstBranch = await _context.Branches
                .Where(b => b.IsActive == 1)
                .OrderBy(b => b.CreatedAt)
                .FirstOrDefaultAsync();
            resolvedBranchId = firstBranch?.BranchId;
        }

        // Validate branch exists if provided
        if (resolvedBranchId != null)
        {
            var branchExists = await _context.Branches.AnyAsync(b => b.BranchId == resolvedBranchId && b.IsActive == 1);
            if (!branchExists)
            {
                return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Chi nhánh không tồn tại hoặc đã bị vô hiệu hóa." } });
            }
        }

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = dto.Email.Trim(),
            Email = dto.Email.Trim(),
            FullName = dto.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            IsActive = 1,
            Status = "ACTIVE",
            DefaultBranchId = resolvedBranchId,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            CreatedBy = currentAdminId
        };

        foreach (var role in validRoles)
        {
            user.Roles.Add(role);
        }

        // Tạo staff assignment nếu có branch
        if (resolvedBranchId != null)
        {
            _context.StaffAssignments.Add(new StaffAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = user.UserId,
                BranchId = resolvedBranchId.Value,
                AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            });
        }

        _context.Users.Add(user);
        await LogAction(user.UserId, "CREATE_USER", "users", "User", $"Tạo người dùng: {user.FullName} ({user.Email})");
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUserById), new { id = user.UserId }, new IdResponseDto { Id = user.UserId });
    }

    /// <summary>
    /// PUT /api/admin/users/{id}
    /// UC-088: Update User Information
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] AdminUserUpdateDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy người dùng." } });

        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Họ tên là bắt buộc." } });

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            var phoneTrimmed = dto.PhoneNumber.Trim();
            if (phoneTrimmed != (user.PhoneNumber ?? "") && await _context.Users.AnyAsync(u => u.PhoneNumber == phoneTrimmed && u.IsActive != 0))
                return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-409", Message = "Số điện thoại đã tồn tại." } });
        }

        user.FullName = dto.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();

        await LogAction(user.UserId, "UPDATE_USER", "users", "User", $"Cập nhật người dùng: {user.Email}");
        await _context.SaveChangesAsync();

        return Ok(new { message = "Cập nhật thông tin thành công." });
    }

    /// <summary>
    /// PUT /api/admin/users/{id}/roles
    /// UC-089: Assign User Role
    /// </summary>
    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> AssignUserRole(Guid id, [FromBody] AdminUserRoleUpdateDto dto)
    {
        var user = await _context.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy người dùng." } });

        if (dto.RoleIds == null || !dto.RoleIds.Any())
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Phải chọn ít nhất một vai trò." } });

        var roles = await _context.Roles.Where(r => dto.RoleIds.Contains(r.RoleId)).ToListAsync();

        // Check if all requested role IDs exist
        var existingRoleIds = roles.Select(r => r.RoleId).ToHashSet();
        var missingRoleIds = dto.RoleIds.Where(rid => !existingRoleIds.Contains(rid)).ToList();
        if (missingRoleIds.Any())
        {
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = $"Vai trò không tồn tại: {string.Join(", ", missingRoleIds)}" } });
        }

        // Check if trying to assign Admin role
        var adminRole = roles.FirstOrDefault(r => r.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase));
        if (adminRole != null)
        {
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-403", Message = "Không thể gán vai trò Admin." } });
        }

        // Check if current user is trying to remove their own Admin role
        var currentUserId = GetCurrentUserId();
        if (currentUserId == user.UserId)
        {
            var hasAdminRole = user.Roles.Any(r => r.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase));
            var willHaveAdminRole = roles.Any(r => r.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase));
            if (hasAdminRole && !willHaveAdminRole)
            {
                return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-403", Message = "Không thể tự xóa vai trò Admin của chính mình." } });
            }
        }

        user.Roles.Clear();
        foreach (var role in roles)
        {
            user.Roles.Add(role);
        }

        await LogAction(user.UserId, "ASSIGN_ROLE", "user_roles", "User", $"Gán vai trò cho: {user.Email}");
        await _context.SaveChangesAsync();

        return Ok(new { message = "Cập nhật vai trò thành công." });
    }

    /// <summary>
    /// PATCH /api/admin/users/{id}/status
    /// UC-090 & UC-091: Activate/Deactivate User Account
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] AdminUserStatusUpdateDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy người dùng." } });

        // Prevent admin from locking themselves
        var currentUserId = GetCurrentUserId();
        if (currentUserId == user.UserId)
        {
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-403", Message = "Không thể thay đổi trạng thái của chính mình." } });
        }

        user.IsActive = (short)(dto.IsActive ? 1 : 0);
        user.Status = dto.IsActive ? "ACTIVE" : "SUSPENDED";

        string actionType = dto.IsActive ? "ACTIVATE_USER" : "DEACTIVATE_USER";
        await LogAction(user.UserId, actionType, "users", "User", $"Đổi trạng thái: {user.Status}");
        await _context.SaveChangesAsync();

        return Ok(new { message = dto.IsActive ? "Tài khoản đã được kích hoạt." : "Tài khoản đã bị khóa." });
    }

    /// <summary>
    /// POST /api/admin/users/{id}/reset-password
    /// UC-092: Reset User Password
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetUserPassword(Guid id, [FromBody] AdminUserResetPasswordDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy người dùng." } });

        // Validate password
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Mật khẩu phải có ít nhất 6 ký tự." } });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

        await LogAction(user.UserId, "RESET_PASSWORD", "users", "User", $"Reset mật khẩu cho: {user.Email}");
        await _context.SaveChangesAsync();

        return Ok(new { message = "Mật khẩu đã được đặt lại thành công." });
    }

    /// <summary>
    /// GET /api/admin/users/{id}/activities
    /// UC-093: View User Activity Log
    /// </summary>
    [HttpGet("{id:guid}/activities")]
    public async Task<IActionResult> GetUserActivities(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        // Verify user exists
        var userExists = await _context.Users.AnyAsync(u => u.UserId == id);
        if (!userExists)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy người dùng." } });

        var query = _context.SystemLogs
            .Include(l => l.User)
            .Where(l => l.UserId == id);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new SystemLogDto
            {
                LogId = l.LogId,
                Action = l.Action,
                TableName = l.TableName,
                EntityType = l.EntityType,
                Description = l.Description,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                Level = l.Level,
                Source = l.Source,
                UserName = l.User != null ? l.User.FullName : "System",
                Timestamp = l.Timestamp
            })
            .ToListAsync();

        return Ok(new PageResultDto<SystemLogDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var id) ? id : null;
    }

    private Task LogAction(Guid targetEntityId, string action, string tableName, string entityType, string description)
    {
        var adminId = GetCurrentUserId();
        if (adminId == null) return Task.CompletedTask;

        var log = new SystemLog
        {
            LogId = Guid.NewGuid(),
            UserId = adminId.Value,
            Action = action,
            TableName = tableName,
            EntityType = entityType,
            EntityId = targetEntityId,
            Description = description,
            Level = "INFO",
            Source = "AdminService",
            Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context.SystemLogs.Add(log);
        return Task.CompletedTask;
    }
}
