using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/community-users")]
[Tags("Admin (Admin)")]
public class CommunityUsersController : ControllerBase
{
    private readonly MEPDatabaseContext _context;

    public CommunityUsersController(MEPDatabaseContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /api/admin/community-users?search=&status=&page=&pageSize=
    /// Returns paginated list of CommunityUser accounts
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCommunityUsers(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Users
            .Include(u => u.Roles)
            .Where(u => u.Roles.Any(r => r.RoleName == "CommunityUser"))
            .AsQueryable();

        // Filter by IsActive status
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => u.IsActive == 1);
            else if (status.Equals("suspended", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => u.IsActive == 0 && u.Status == "SUSPENDED");
            else if (status.Equals("banned", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => u.IsActive == 0 && u.Status == "BANNED");
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
            .Select(u => new CommunityUserDto
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
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(new PageResultDto<CommunityUserDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// GET /api/admin/community-users/{id}
    /// Returns full details of a community user
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCommunityUserById(Guid id)
    {
        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-404", Message = "Tài khoản không tồn tại." }
            });

        if (!user.Roles.Any(r => r.RoleName == "CommunityUser"))
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-404", Message = "Người dùng không thuộc nhóm CommunityUser." }
            });

        return Ok(new CommunityUserDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive ?? 0,
            IsVerified = user.IsVerified,
            Status = user.Status,
            SuspendedUntil = user.SuspendedUntil,
            CreatedAt = user.CreatedAt
        });
    }

    public class CreateCommunityUserRequest
    {
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string Password { get; set; } = null!;
    }

    /// <summary>
    /// POST /api/admin/community-users
    /// Creates a new CommunityUser account. BR-44: email must NOT have @system.local domain.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateCommunityUser([FromBody] CreateCommunityUserRequest dto)
    {
        // BR-44 inverse: Owner email MUST NOT have @system.local domain
        if (string.IsNullOrWhiteSpace(dto.Email) ||
            dto.Email.Trim().EndsWith("@system.local", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail
                {
                    Code = "MSG-BR44",
                    Message = "Email không được sử dụng domain @system.local."
                }
            });
        }

        // Check uniqueness
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

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "CommunityUser");
        if (role == null)
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-404", Message = "Vai trò 'CommunityUser' không tồn tại." }
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
            IsVerified = false,
            Status = "ACTIVE",
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        user.Roles.Add(role);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCommunityUserById), new { id = user.UserId }, new IdResponseDto { Id = user.UserId });
    }

    public class UpdateCommunityUserRequest
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
    }

    /// <summary>
    /// PUT /api/admin/community-users/{id}
    /// Updates community user info
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCommunityUser(Guid id, [FromBody] UpdateCommunityUserRequest dto)
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
            if (dto.Email.Trim().EndsWith("@system.local", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = new ErrorDetail
                    {
                        Code = "MSG-BR44",
                        Message = "Email không được sử dụng domain @system.local."
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

        await _context.SaveChangesAsync();

        return Ok(new { message = "Cập nhật thông tin người dùng thành công." });
    }

    /// <summary>
    /// PUT /api/admin/community-users/{id}/status
    /// Sets user status (active/suspended)
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] StaffAccountsController.UpdateStatusRequest dto)
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

    /// <summary>
    /// PUT /api/admin/community-users/{id}/ban
    /// Permanently bans a community user
    /// </summary>
    [HttpPut("{id:guid}/ban")]
    public async Task<IActionResult> BanUser(Guid id, [FromBody] StaffAccountsController.BanRequest dto)
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
            Action = "BAN_COMMUNITY_USER",
            TableName = "users",
            EntityType = "User",
            Description = $"Ban tài khoản community: {user.FullName} ({user.Email}). Lý do: {dto.Reason}",
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
        if (Guid.TryParse(userIdClaim, out var id))
            return id;
        return null;
    }
}
