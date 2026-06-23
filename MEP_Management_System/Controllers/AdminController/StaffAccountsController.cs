using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/staff")]
[Tags("Admin (Admin)")]
public class StaffAccountsController : ControllerBase
{
    private readonly MEPDatabaseContext _context;

    public StaffAccountsController(MEPDatabaseContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /api/admin/staff?search=&status=&page=&pageSize=
    /// Returns paginated list of Staff accounts (Owner, SalesStaff, InventoryStaff)
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStaff(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var staffRoles = new[] { "Owner", "SalesStaff", "InventoryStaff" };

        var query = _context.Users
            .Include(u => u.Roles)
            .Include(u => u.StaffAssignments)
                .ThenInclude(sa => sa.Branch)
            .Where(u => u.Roles.Any(r => staffRoles.Contains(r.RoleName)))
            .AsQueryable();

        // Filter by role name OR by "active" (IsActive=1)
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => u.IsActive == 1);
            else
                query = query.Where(u => u.Roles.Any(r => r.RoleName == status));
        }

        // Search by FullName or Email
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.UserId,
                u.Username,
                u.Email,
                u.FullName,
                u.PhoneNumber,
                u.IsActive,
                u.IsVerified,
                u.Status,
                u.SuspendedUntil,
                u.SubscriptionPlan,
                Roles = u.Roles.Select(r => r.RoleName).ToList(),
                Assignment = u.StaffAssignments.FirstOrDefault(),
                u.CreatedAt
            })
            .ToListAsync();

        var result = items.Select(u => new StaffAccountDto
        {
            UserId = u.UserId,
            Username = u.Username,
            Email = u.Email,
            FullName = u.FullName,
            PhoneNumber = u.PhoneNumber,
            IsActive = u.IsActive ?? 0,
            IsVerified = u.IsVerified,
            Status = u.Status,
            SuspendedUntil = u.SuspendedUntil,
            SubscriptionPlan = u.SubscriptionPlan,
            Roles = u.Roles,
            BranchId = u.Assignment?.BranchId,
            BranchName = u.Assignment?.Branch?.BranchName,
            CreatedAt = u.CreatedAt
        }).ToList();

        return Ok(new PageResultDto<StaffAccountDto>
        {
            Items = result,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// GET /api/admin/staff/{id}
    /// Returns full details of a staff account
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStaffById(Guid id)
    {
        var staff = await _context.Users
            .Include(u => u.Roles)
            .Include(u => u.StaffAssignments)
                .ThenInclude(sa => sa.Branch)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (staff == null)
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-404", Message = "Tài khoản không tồn tại." }
            });

        var staffAssignment = staff.StaffAssignments.FirstOrDefault();
        return Ok(new StaffAccountDto
        {
            UserId = staff.UserId,
            Username = staff.Username,
            Email = staff.Email,
            FullName = staff.FullName,
            PhoneNumber = staff.PhoneNumber,
            IsActive = staff.IsActive ?? 0,
            IsVerified = staff.IsVerified,
            Status = staff.Status,
            SuspendedUntil = staff.SuspendedUntil,
            SubscriptionPlan = staff.SubscriptionPlan,
            Roles = staff.Roles.Select(r => r.RoleName).ToList(),
            BranchId = staffAssignment?.BranchId,
            BranchName = staffAssignment?.Branch?.BranchName,
            CreatedAt = staff.CreatedAt
        });
    }

    public class CreateStaffRequest
    {
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string Password { get; set; } = null!;
        public string Role { get; set; } = null!;  // Owner | SalesStaff | InventoryStaff
        public Guid? BranchId { get; set; }
        public string? SubscriptionPlan { get; set; }
    }

    /// <summary>
    /// POST /api/admin/staff
    /// Creates a new Staff account. BR-44: email must have @system.local domain.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest dto)
    {
        // BR-44: Staff email must have @system.local domain
        if (string.IsNullOrWhiteSpace(dto.Email) ||
            !dto.Email.Trim().EndsWith("@system.local", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail
                {
                    Code = "MSG-BR44",
                    Message = "Email nhân viên phải có domain @system.local."
                }
            });
        }

        var validRoles = new[] { "Owner", "SalesStaff", "InventoryStaff" };
        if (!validRoles.Contains(dto.Role))
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail
                {
                    Code = "MSG-400",
                    Message = $"Vai trò phải là một trong: {string.Join(", ", validRoles)}."
                }
            });
        }

        // Check username/email uniqueness
        if (await _context.Users.AnyAsync(u => u.Username == dto.Username.Trim()))
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-409", Message = "Username đã tồn tại." }
            });
        }

        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.Trim().ToLower()))
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-409", Message = "Email đã được sử dụng." }
            });
        }

        // Get role
        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == dto.Role);
        if (role == null)
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-404", Message = $"Vai trò '{dto.Role}' không tồn tại." }
            });
        }

        // BCrypt password hashing
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = dto.Username.Trim(),
            Email = dto.Email.Trim(),
            FullName = dto.FullName.Trim(),
            PhoneNumber = dto.PhoneNumber?.Trim(),
            PasswordHash = passwordHash,
            IsActive = 1,
            IsVerified = dto.Role == "Owner",
            Status = "ACTIVE",
            SubscriptionPlan = dto.SubscriptionPlan,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        user.Roles.Add(role);

        // If Owner with no BranchId, create a default branch
        if (dto.Role == "Owner" && !dto.BranchId.HasValue)
        {
            var branch = new Branch
            {
                BranchId = Guid.NewGuid(),
                BranchName = dto.FullName.Trim() + " Store"
            };
            _context.Branches.Add(branch);
            await _context.SaveChangesAsync(); // get branch ID

            _context.StaffAssignments.Add(new StaffAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = user.UserId,
                BranchId = branch.BranchId,
                AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            });
        }
        else if (dto.BranchId.HasValue)
        {
            _context.StaffAssignments.Add(new StaffAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = user.UserId,
                BranchId = dto.BranchId.Value,
                AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            });
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetStaffById), new { id = user.UserId }, new IdResponseDto { Id = user.UserId });
    }

    public class UpdateStaffRequest
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Role { get; set; }
        public string? SubscriptionPlan { get; set; }
    }

    /// <summary>
    /// PUT /api/admin/staff/{id}
    /// Updates staff account info
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateStaff(Guid id, [FromBody] UpdateStaffRequest dto)
    {
        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-404", Message = "Tài khoản không tồn tại." }
            });

        if (!string.IsNullOrWhiteSpace(dto.FullName))
            user.FullName = dto.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            if (!dto.Email.Trim().EndsWith("@system.local", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = new ErrorDetail
                    {
                        Code = "MSG-BR44",
                        Message = "Email nhân viên phải có domain @system.local."
                    }
                });
            }
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.Trim().ToLower() && u.UserId != id))
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = new ErrorDetail { Code = "MSG-409", Message = "Email đã được sử dụng." }
                });
            }
            user.Email = dto.Email.Trim();
        }

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
            user.PhoneNumber = dto.PhoneNumber.Trim();

        if (!string.IsNullOrWhiteSpace(dto.SubscriptionPlan))
            user.SubscriptionPlan = dto.SubscriptionPlan;

        if (!string.IsNullOrWhiteSpace(dto.Role))
        {
            var newRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == dto.Role);
            if (newRole == null)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = new ErrorDetail { Code = "MSG-404", Message = $"Vai trò '{dto.Role}' không tồn tại." }
                });
            }
            // Replace primary role
            user.Roles.Clear();
            user.Roles.Add(newRole);
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "Cập nhật thông tin nhân viên thành công." });
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = null!;  // "active" | "suspended"
    }

    /// <summary>
    /// PUT /api/admin/staff/{id}/status
    /// Sets staff IsActive flag (active = 1, suspended = 0)
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-404", Message = "Tài khoản không tồn tại." }
            });

        if (dto.Status.ToLower() == "active")
        {
            user.IsActive = 1;
            user.Status = "ACTIVE";
        }
        else if (dto.Status.ToLower() == "suspended")
        {
            user.IsActive = 0;
            user.Status = "SUSPENDED";
        }
        else
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-400", Message = "Status phải là 'active' hoặc 'suspended'." }
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = $"Trạng thái tài khoản được đặt thành '{dto.Status}'." });
    }

    public class BanRequest
    {
        public string Reason { get; set; } = null!;
    }

    /// <summary>
    /// PUT /api/admin/staff/{id}/ban
    /// Permanently bans a staff account
    /// </summary>
    [HttpPut("{id:guid}/ban")]
    public async Task<IActionResult> BanStaff(Guid id, [FromBody] BanRequest dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-404", Message = "Tài khoản không tồn tại." }
            });

        user.IsActive = 0;
        user.Status = "BANNED";

        var adminId = GetAdminUserId();
        _context.SystemLogs.Add(new SystemLog
        {
            LogId = Guid.NewGuid(),
            UserId = adminId ?? Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Action = "BAN_STAFF",
            TableName = "users",
            EntityType = "User",
            Description = $"Ban tài khoản: {user.FullName} ({user.Email}). Lý do: {dto.Reason}",
            Level = "WARN",
            Source = "AdminService",
            Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });

        await _context.SaveChangesAsync();
        return Ok(new { message = "Tài khoản đã bị banned vĩnh viễn." });
    }

    private Guid? GetAdminUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var id) ? id : null;
    }
}
