using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.AdminController;

/// <summary>
/// Controller thống nhất cho Admin quản lý tài khoản (Owner + Staff).
/// Thay thế các endpoint cũ ở OwnerAccountsController.
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/accounts")]
[Tags("Admin (Admin)")]
public class AdminAccountsController : ControllerBase
{
    private readonly MEPDatabaseContext _context;

    public AdminAccountsController(MEPDatabaseContext context)
    {
        _context = context;
    }

    #region Helper Methods

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var id) ? id : null;
    }

    private async Task<Role> GetOrCreateRoleAsync(string roleName, string description)
    {
        var role = await _context.Roles.FirstOrDefaultAsync(r =>
            r.RoleName.ToLower() == roleName.Trim().ToLower());

        if (role == null)
        {
            role = new Role
            {
                RoleId = Guid.NewGuid(),
                RoleName = roleName.Trim(),
                Description = description,
                IsSystem = 1
            };
            _context.Roles.Add(role);
        }

        return role;
    }

    private async Task<AdminAccountDetailDto> MapToDtoAsync(User user)
    {
        string? branchName = null;
        if (user.Roles.Any(r => r.RoleName == "Owner"))
        {
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.ManagerUserId == user.UserId);
            branchName = branch?.BranchName;
        }
        else
        {
            branchName = user.StaffAssignments.FirstOrDefault()?.Branch?.BranchName;
        }

        return new AdminAccountDetailDto
        {
            UserId = user.UserId,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            DeletedAt = user.DeletedAt,
            SuspendedUntil = user.SuspendedUntil,
            IsVerified = user.IsVerified,
            SubscriptionPlan = user.SubscriptionPlan,
            CreatedBy = user.CreatedBy,
            DefaultBranchId = user.DefaultBranchId,
            DefaultBranchName = branchName,
            Roles = user.Roles.Select(r => new AdminRoleDto
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                Description = r.Description
            }).ToList(),
            PermissionCodes = user.UserPermissions
                .Where(up => up.IsGranted != false)
                .Select(up => up.Permission?.PermissionCode ?? "")
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
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

    private async Task<bool> IsUsernameTakenAsync(string username, Guid? excludeUserId = null)
    {
        var normalized = username.Trim().ToLower();
        var query = _context.Users.Where(u => u.Username.ToLower() == normalized);
        if (excludeUserId.HasValue)
            query = query.Where(u => u.UserId != excludeUserId.Value);
        return await query.AnyAsync();
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

    private Task LogAction(Guid targetEntityId, string action, string entityType, string description, string level = "INFO")
    {
        var adminId = GetCurrentUserId();
        if (adminId == null) return Task.CompletedTask;

        _context.SystemLogs.Add(new SystemLog
        {
            LogId = Guid.NewGuid(),
            UserId = adminId.Value,
            Action = action,
            TableName = "users",
            EntityType = entityType,
            EntityId = targetEntityId,
            Description = description,
            Level = level,
            Source = "AdminAccountService",
            Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
        return Task.CompletedTask;
    }

    #endregion

    #region GET /api/admin/accounts

    /// <summary>
    /// GET /api/admin/accounts
    /// Lấy danh sách tài khoản Owner + Staff.
    /// Query: role (owner|staff|all), status (active|suspended|deleted), search, page, pageSize.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.Users
            .Include(u => u.Roles)
            .Include(u => u.StaffAssignments).ThenInclude(sa => sa.Branch)
            .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .Where(u => !u.Roles.Any(r => r.RoleName == "Admin"))
            .AsQueryable();

        // Filter by role
        if (!string.IsNullOrWhiteSpace(role))
        {
            if (role.Equals("owner", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => u.Roles.Any(r => r.RoleName == "Owner"));
            else if (role.Equals("staff", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => !u.Roles.Any(r => r.RoleName == "Admin" || r.RoleName == "Owner"));
            // "all" or anything else: no additional filter
        }

        // Filter by status
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => (u.Status.ToUpper() == "ACTIVE" || u.Status == null) && u.IsActive == 1);
            else if (status.Equals("suspended", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => u.Status.ToUpper() == "SUSPENDED" || (u.IsActive == 0 && u.Status.ToUpper() != "DELETED" && u.Status.ToUpper() != "PERMANENT_DELETED"));
            else if (status.Equals("deleted", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => u.Status.ToUpper() == "DELETED" || u.Status.ToUpper() == "PERMANENT_DELETED");
        }
        else
        {
            // Mặc định không hiện user đã xóa mềm
            query = query.Where(u => u.Status.ToUpper() != "DELETED" && u.Status.ToUpper() != "PERMANENT_DELETED");
        }

        // Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                u.Username.ToLower().Contains(term) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
        }

        var totalCount = await query.CountAsync();

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = new List<AdminAccountDetailDto>();
        foreach (var u in users)
        {
            items.Add(await MapToDtoAsync(u));
        }

        return Ok(new PageResultDto<AdminAccountDetailDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    #endregion

    #region GET /api/admin/accounts/{id}

    /// <summary>
    /// GET /api/admin/accounts/{id} - Chi tiết một tài khoản.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAccountById(Guid id)
    {
        var user = await _context.Users
            .Include(u => u.Roles)
            .Include(u => u.StaffAssignments)
            .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            return NotFound(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy tài khoản." }
            });

        return Ok(await MapToDtoAsync(user));
    }

    /// <summary>
    /// GET /api/admin/accounts/{id}/activities - Lịch sử hoạt động của tài khoản
    /// </summary>
    [HttpGet("{id:guid}/activities")]
    public async Task<IActionResult> GetAccountActivities(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
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

    #endregion

    #region POST /api/admin/accounts/owners

    /// <summary>
    /// POST /api/admin/accounts/owners - Admin tạo tài khoản Owner mới.
    /// Nếu BranchId null, tự tạo Branch mặc định.
    /// </summary>
    [HttpPost("owners")]
    public async Task<IActionResult> CreateOwner([FromBody] AdminCreateOwnerRequest dto)
    {
        var currentAdminId = GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Email là bắt buộc." } });

        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Họ tên là bắt buộc." } });

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Mật khẩu phải có ít nhất 6 ký tự." } });

        if (await IsEmailTakenAsync(dto.Email))
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-409", Message = "Email đã được sử dụng." } });

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber) && await IsPhoneTakenAsync(dto.PhoneNumber))
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-409", Message = "Số điện thoại đã được sử dụng." } });

        // Username = email (giống AdminUsersController pattern)
        var username = dto.Email.Trim();

        // Lấy role Owner
        var ownerRole = await GetOrCreateRoleAsync("Owner", "Chủ cửa hàng - toàn quyền quản lý chi nhánh");

        var newUserId = Guid.NewGuid();

        // Xử lý branch
        Guid branchId;
        Branch branch;
        if (dto.BranchId.HasValue && dto.BranchId.Value != Guid.Empty)
        {
            var existingBranch = await _context.Branches
                .FirstOrDefaultAsync(b => b.BranchId == dto.BranchId.Value && b.IsActive == 1);
            if (existingBranch == null)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = new ErrorDetail { Code = "MSG-404", Message = "Chi nhánh không tồn tại hoặc đã bị vô hiệu hóa." }
                });
            }

            // Kiểm tra xem chi nhánh đã có Owner nào chưa (chỉ được phép có 1 Owner)
            var hasOwner = await _context.Users.AnyAsync(u =>
                u.DefaultBranchId == dto.BranchId.Value &&
                u.IsActive == 1 &&
                u.Roles.Any(r => r.RoleName == "Owner"));

            if (hasOwner)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = new ErrorDetail { Code = "MSG-409", Message = "Chi nhánh này đã có Owner. Mỗi chi nhánh chỉ được phép có 1 Owner." }
                });
            }

            branch = existingBranch;
            branchId = existingBranch.BranchId;
        }
        else
        {
            // For a single-store system, we should assign the owner to the existing active branch
            // Prefer the branch with the most products to ensure they see the main data
            var existingMainBranch = await _context.Branches
                .Where(b => b.IsActive == 1)
                .OrderByDescending(b => b.BranchProducts.Count)
                .FirstOrDefaultAsync();

            if (existingMainBranch != null)
            {
                branch = existingMainBranch;
                branchId = existingMainBranch.BranchId;
            }
            else
            {
                // Tự tạo branch chỉ khi hệ thống chưa có bất kỳ chi nhánh nào
                var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                var newBranch = new Branch
                {
                    BranchId = Guid.NewGuid(),
                    BranchName = !string.IsNullOrWhiteSpace(dto.BranchName)
                        ? dto.BranchName.Trim()
                        : $"{dto.FullName.Trim()} Store",
                    Phone = dto.PhoneNumber,
                    ManagerUserId = null,
                    IsActive = 1,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _context.Branches.Add(newBranch);
                await _context.SaveChangesAsync(); // get BranchId
                branch = newBranch;
                branchId = newBranch.BranchId;
            }
        }

        var nowUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var newUser = new User
        {
            UserId = newUserId,
            Username = username,
            Email = dto.Email.Trim(),
            FullName = dto.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            IsActive = 1,
            IsVerified = true,
            Status = "ACTIVE",
            SubscriptionPlan = dto.SubscriptionPlan,
            DefaultBranchId = branchId,
            CreatedAt = nowUtc,
            CreatedBy = currentAdminId
        };
        newUser.Roles.Add(ownerRole);

        _context.Users.Add(newUser);

        // Gán Manager cho branch nếu vừa tạo
        if (branch.ManagerUserId == null)
        {
            branch.ManagerUserId = newUser.UserId;
        }

        // Tạo StaffAssignment
        _context.StaffAssignments.Add(new StaffAssignment
        {
            AssignmentId = Guid.NewGuid(),
            UserId = newUser.UserId,
            BranchId = branchId,
            AssignedAt = nowUtc
        });

        await LogAction(newUser.UserId, "CREATE_OWNER", "User",
            $"Tạo Owner: {newUser.FullName} ({newUser.Email}) — Branch: {branchId}");

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAccountById), new { id = newUser.UserId },
            new IdResponseDto { Id = newUser.UserId });
    }

    #endregion

    #region POST /api/admin/accounts/staff

    /// <summary>
    /// POST /api/admin/accounts/staff - Admin tạo tài khoản Staff.
    /// </summary>
    [HttpPost("staff")]
    public async Task<IActionResult> CreateStaff([FromBody] AdminCreateStaffRequest dto)
    {
        var currentAdminId = GetCurrentUserId();

        // Required fields
        if (string.IsNullOrWhiteSpace(dto.Username) ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.FullName) ||
            string.IsNullOrWhiteSpace(dto.Password) ||
            string.IsNullOrWhiteSpace(dto.RoleName))
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-400", Message = "Thiếu thông tin bắt buộc." }
            });
        }

        if (dto.Password.Length < 6)
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-400", Message = "Mật khẩu phải có ít nhất 6 ký tự." }
            });

        // Validate role
        var validRoleNames = new[] { "SalesStaff", "InventoryStaff", "Staff" };
        if (!validRoleNames.Contains(dto.RoleName.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail
                {
                    Code = "MSG-400",
                    Message = "Role không hợp lệ. Chỉ chấp nhận: SalesStaff, InventoryStaff, Staff."
                }
            });
        }

        // Uniqueness
        if (await IsEmailTakenAsync(dto.Email))
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-409", Message = "Email đã được sử dụng." } });

        if (await IsUsernameTakenAsync(dto.Username))
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-409", Message = "Username đã tồn tại." } });

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber) && await IsPhoneTakenAsync(dto.PhoneNumber))
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-409", Message = "Số điện thoại đã được sử dụng." } });

        // Resolve role name (SalesStaff -> "Sales Staff" trong DB)
        var roleNameToSearch = dto.RoleName.Trim();
        if (roleNameToSearch.Equals("SalesStaff", StringComparison.OrdinalIgnoreCase))
            roleNameToSearch = "Sales Staff";
        else if (roleNameToSearch.Equals("InventoryStaff", StringComparison.OrdinalIgnoreCase))
            roleNameToSearch = "Inventory Staff";

        var staffRole = await GetOrCreateRoleAsync(roleNameToSearch, $"Role {dto.RoleName} - tự động tạo");
        await _context.Entry(staffRole).Collection(r => r.Permissions).LoadAsync();

        // Resolve branch
        Guid? resolvedBranchId = dto.BranchId;
        if (resolvedBranchId == null || resolvedBranchId == Guid.Empty)
        {
            // Chọn chi nhánh có nhiều hàng hóa nhất để đảm bảo Staff vào là thấy dữ liệu
            var bestBranch = await _context.Branches
                .Where(b => b.IsActive == 1)
                .OrderByDescending(b => b.BranchProducts.Count)
                .FirstOrDefaultAsync();
            resolvedBranchId = bestBranch?.BranchId;
        }
        else
        {
            var branchExists = await _context.Branches.AnyAsync(b => b.BranchId == resolvedBranchId.Value && b.IsActive == 1);
            if (!branchExists)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = new ErrorDetail { Code = "MSG-404", Message = "Chi nhánh không tồn tại hoặc đã bị vô hiệu hóa." }
                });
            }
        }

        // Resolve permission codes
        List<string> permissionCodesToApply = new();
        if (dto.PermissionCodes != null && dto.PermissionCodes.Any())
        {
            permissionCodesToApply = dto.PermissionCodes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            // Mặc định lấy permission của role
            permissionCodesToApply = staffRole.Permissions
                .Select(p => p.PermissionCode)
                .ToList();
        }

        // Validate permission codes tồn tại
        List<Permission> validPermissions = new();
        if (permissionCodesToApply.Any())
        {
            validPermissions = await _context.Permissions
                .Where(p => permissionCodesToApply.Contains(p.PermissionCode))
                .ToListAsync();
            var foundCodes = validPermissions.Select(p => p.PermissionCode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missing = permissionCodesToApply.Where(c => !foundCodes.Contains(c)).ToList();
            if (missing.Any())
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = new ErrorDetail
                    {
                        Code = "MSG-400",
                        Message = $"Mã quyền không tồn tại: {string.Join(", ", missing)}"
                    }
                });
            }
        }

        var nowUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var newUser = new User
            {
                UserId = Guid.NewGuid(),
                Username = dto.Username.Trim(),
                Email = dto.Email.Trim().ToLower(),
                FullName = dto.FullName.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                IsActive = 1,
                IsVerified = true,
                Status = "ACTIVE",
                DefaultBranchId = resolvedBranchId,
                CreatedAt = nowUtc,
                CreatedBy = currentAdminId
            };
            newUser.Roles.Add(staffRole);

            _context.Users.Add(newUser);

            if (resolvedBranchId.HasValue)
            {
                _context.StaffAssignments.Add(new StaffAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    UserId = newUser.UserId,
                    BranchId = resolvedBranchId.Value,
                    AssignedAt = nowUtc
                });
            }

            foreach (var permission in validPermissions)
            {
                _context.UserPermissions.Add(new UserPermission
                {
                    UserPermissionId = Guid.NewGuid(),
                    UserId = newUser.UserId,
                    PermissionId = permission.PermissionId,
                    IsGranted = true
                });
            }

            await LogAction(newUser.UserId, "CREATE_STAFF", "User",
                $"Tạo Staff: {newUser.FullName} ({newUser.Email}) — Role: {dto.RoleName}");

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(nameof(GetAccountById), new { id = newUser.UserId },
                new IdResponseDto { Id = newUser.UserId });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    #endregion

    #region PUT /api/admin/accounts/{id}

    /// <summary>
    /// PUT /api/admin/accounts/{id} - Cập nhật thông tin cơ bản.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] AdminUpdateAccountRequest dto)
    {
        var currentAdminId = GetCurrentUserId();

        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy tài khoản." } });

        // Chặn admin tự sửa chính mình (an toàn)
        if (currentAdminId == user.UserId)
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-403", Message = "Không thể tự sửa tài khoản của chính mình qua endpoint này." }
            });
        }

        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Họ tên là bắt buộc." } });

        // Nếu user là Staff (có role staff), không ép domain email
        var isStaff = user.Roles.Any(r =>
            r.RoleName == "Sales Staff" ||
            r.RoleName == "Inventory Staff" ||
            r.RoleName == "Staff");

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            if (await IsEmailTakenAsync(dto.Email, user.UserId))
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = new ErrorDetail { Code = "MSG-409", Message = "Email đã được sử dụng." }
                });
            }
            user.Email = dto.Email.Trim();
        }

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            if (await IsPhoneTakenAsync(dto.PhoneNumber, user.UserId))
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = new ErrorDetail { Code = "MSG-409", Message = "Số điện thoại đã được sử dụng." }
                });
            }
            user.PhoneNumber = dto.PhoneNumber.Trim();
        }

        user.FullName = dto.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(dto.SubscriptionPlan))
            user.SubscriptionPlan = dto.SubscriptionPlan;

        await LogAction(user.UserId, "UPDATE_ACCOUNT", "User",
            $"Cập nhật tài khoản: {user.Email}");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Cập nhật thành công." });
    }

    #endregion

    #region PUT /api/admin/accounts/{id}/roles

    /// <summary>
    /// PUT /api/admin/accounts/{id}/roles - Gán roles cho user. Chặn role Admin.
    /// </summary>
    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AdminAssignRolesRequest dto)
    {
        var currentAdminId = GetCurrentUserId();

        var user = await _context.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy tài khoản." } });

        if (dto.RoleIds == null || !dto.RoleIds.Any())
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Phải chọn ít nhất một vai trò." } });

        var roles = await _context.Roles.Where(r => dto.RoleIds.Contains(r.RoleId)).ToListAsync();

        var existingIds = roles.Select(r => r.RoleId).ToHashSet();
        var missing = dto.RoleIds.Where(rid => !existingIds.Contains(rid)).ToList();
        if (missing.Any())
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-400", Message = $"Vai trò không tồn tại: {string.Join(", ", missing)}" }
            });
        }

        // Chặn role Admin
        var adminRole = roles.FirstOrDefault(r => r.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase));
        if (adminRole != null)
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-403", Message = "Không thể gán vai trò Admin." }
            });
        }

        // Kiểm tra xem nếu gán quyền Owner thì chi nhánh hiện tại của user đã có Owner chưa
        var isAssigningOwner = roles.Any(r => r.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase));
        if (isAssigningOwner && user.DefaultBranchId.HasValue)
        {
            var hasOwner = await _context.Users.AnyAsync(u =>
                u.UserId != user.UserId &&
                u.DefaultBranchId == user.DefaultBranchId.Value &&
                u.IsActive == 1 &&
                u.Roles.Any(r => r.RoleName == "Owner"));

            if (hasOwner)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = new ErrorDetail { Code = "MSG-409", Message = "Chi nhánh của nhân viên này đã có Owner. Mỗi chi nhánh chỉ được phép có 1 Owner." }
                });
            }
        }

        // Admin tự gỡ role của mình
        if (currentAdminId == user.UserId)
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-403", Message = "Không thể tự thay đổi vai trò của chính mình." }
            });
        }

        user.Roles.Clear();
        foreach (var role in roles)
        {
            user.Roles.Add(role);
        }

        var roleNames = string.Join(", ", roles.Select(r => r.RoleName));
        await LogAction(user.UserId, "ASSIGN_ROLE", "User", $"Gán vai trò [{roleNames}] cho: {user.Email}");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Cập nhật vai trò thành công." });
    }

    #endregion

    #region PUT /api/admin/accounts/{id}/permissions

    /// <summary>
    /// PUT /api/admin/accounts/{id}/permissions - Gán permission trực tiếp cho user.
    /// </summary>
    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> AssignPermissions(Guid id, [FromBody] AdminAssignPermissionsRequest dto)
    {
        var currentAdminId = GetCurrentUserId();

        var user = await _context.Users
            .Include(u => u.Roles).ThenInclude(r => r.Permissions)
            .Include(u => u.UserPermissions)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy tài khoản." } });

        if (dto.PermissionCodes == null)
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Danh sách quyền không được để trống." } });

        var requestedCodes = dto.PermissionCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var validPermissions = await _context.Permissions
            .Where(p => requestedCodes.Contains(p.PermissionCode))
            .ToListAsync();

        var foundCodes = validPermissions.Select(p => p.PermissionCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = requestedCodes.Where(c => !foundCodes.Contains(c)).ToList();
        if (missing.Any())
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-400", Message = $"Mã quyền không tồn tại: {string.Join(", ", missing)}" }
            });
        }

        // Lấy role permission codes
        var rolePermissionCodes = user.Roles
            .SelectMany(r => r.Permissions)
            .Select(p => p.PermissionCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requestedSet = validPermissions.Select(p => p.PermissionCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Xóa permission cũ của user
        _context.UserPermissions.RemoveRange(user.UserPermissions);

        // Add explicit allows
        foreach (var permission in validPermissions)
        {
            _context.UserPermissions.Add(new UserPermission
            {
                UserPermissionId = Guid.NewGuid(),
                UserId = user.UserId,
                PermissionId = permission.PermissionId,
                IsGranted = true
            });
        }

        // Add explicit denies (role permission không có trong request)
        foreach (var roleCode in rolePermissionCodes.Where(c => !requestedSet.Contains(c)))
        {
            var perm = validPermissions.FirstOrDefault(p => p.PermissionCode.Equals(roleCode, StringComparison.OrdinalIgnoreCase))
                ?? await _context.Permissions.FirstOrDefaultAsync(p => p.PermissionCode == roleCode);
            if (perm != null)
            {
                _context.UserPermissions.Add(new UserPermission
                {
                    UserPermissionId = Guid.NewGuid(),
                    UserId = user.UserId,
                    PermissionId = perm.PermissionId,
                    IsGranted = false
                });
            }
        }

        await LogAction(user.UserId, "ASSIGN_PERMISSIONS", "User",
            $"Gán {validPermissions.Count} quyền cho: {user.Email}");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Cập nhật quyền thành công." });
    }

    #endregion

    #region PUT /api/admin/accounts/{id}/branch

    /// <summary>
    /// PUT /api/admin/accounts/{id}/branch - Gán/đổi chi nhánh cho user.
    /// </summary>
    [HttpPut("{id:guid}/branch")]
    public async Task<IActionResult> AssignBranch(Guid id, [FromBody] AdminAssignBranchRequest dto)
    {
        var branch = await _context.Branches
            .FirstOrDefaultAsync(b => b.BranchId == dto.BranchId && b.IsActive == 1);
        if (branch == null)
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-404", Message = "Chi nhánh không tồn tại hoặc đã bị vô hiệu hóa." }
            });
        }

        var user = await _context.Users
            .Include(u => u.StaffAssignments)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy tài khoản." } });

        // Kiểm tra xem nếu user là Owner thì branch mới đã có Owner chưa
        var isOwner = user.Roles.Any(r => r.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase));
        if (isOwner)
        {
            var hasOwner = await _context.Users.AnyAsync(u =>
                u.UserId != user.UserId &&
                u.DefaultBranchId == branch.BranchId &&
                u.IsActive == 1 &&
                u.Roles.Any(r => r.RoleName == "Owner"));

            if (hasOwner)
            {
                return BadRequest(new ErrorResponseDto
                {
                    Error = new ErrorDetail { Code = "MSG-409", Message = "Chi nhánh mới đã có Owner. Mỗi chi nhánh chỉ được phép có 1 Owner." }
                });
            }
        }

        var existing = user.StaffAssignments.FirstOrDefault();
        if (existing != null)
        {
            existing.BranchId = branch.BranchId;
            existing.AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        }
        else
        {
            _context.StaffAssignments.Add(new StaffAssignment
            {
                AssignmentId = Guid.NewGuid(),
                UserId = user.UserId,
                BranchId = branch.BranchId,
                AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            });
        }

        user.DefaultBranchId = branch.BranchId;

        await LogAction(user.UserId, "ASSIGN_BRANCH", "User",
            $"Gán chi nhánh {branch.BranchName} cho: {user.Email}");

        await _context.SaveChangesAsync();

        return Ok(new { message = $"Đã gán chi nhánh {branch.BranchName} cho {user.FullName}." });
    }

    #endregion

    #region PATCH /api/admin/accounts/{id}/status

    /// <summary>
    /// PATCH /api/admin/accounts/{id}/status - Đổi trạng thái ACTIVE/SUSPENDED.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] AdminToggleStatusRequest dto)
    {
        var currentAdminId = GetCurrentUserId();

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy tài khoản." } });

        if (currentAdminId == user.UserId)
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-403", Message = "Không thể tự thay đổi trạng thái của chính mình." }
            });
        }

        var oldStatus = user.Status;
        user.IsActive = (short)(dto.IsActive ? 1 : 0);
        user.Status = dto.IsActive ? "ACTIVE" : "SUSPENDED";

        var actionCode = dto.IsActive ? "ACTIVATE_USER" : "DEACTIVATE_USER";
        await LogAction(user.UserId, actionCode, "User",
            $"Đổi trạng thái {oldStatus} → {user.Status}: {user.Email}");

        await _context.SaveChangesAsync();

        return Ok(new { message = dto.IsActive ? "Tài khoản đã được kích hoạt." : "Tài khoản đã bị tạm khóa." });
    }

    #endregion

    #region POST /api/admin/accounts/{id}/reset-password

    /// <summary>
    /// POST /api/admin/accounts/{id}/reset-password - Reset mật khẩu.
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] AdminResetPasswordRequest dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-400", Message = "Mật khẩu phải có ít nhất 6 ký tự." }
            });

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy tài khoản." } });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

        await LogAction(user.UserId, "RESET_PASSWORD", "User",
            $"Reset mật khẩu cho: {user.Email}");

        await _context.SaveChangesAsync();

        return Ok(new { message = "Mật khẩu đã được đặt lại." });
    }

    #endregion

    #region GET /api/admin/accounts/{id}/check-relations

    /// <summary>
    /// GET /api/admin/accounts/{id}/check-relations - Kiểm tra ràng buộc FK trước khi xóa.
    /// </summary>
    [HttpGet("{id:guid}/check-relations")]
    public async Task<IActionResult> CheckRelations(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy tài khoản." } });

        var invoiceCount = await _context.Invoices.CountAsync(i => i.UserId == id);
        var orderCount = await _context.Orders.CountAsync(o => o.UserId == id);
        var returnOrderCount = await _context.ReturnOrders.CountAsync(r => r.UserId == id);
        var purchaseOrderCount = await _context.PurchaseOrders.CountAsync(p => p.UserId == id);
        var shiftCount = await _context.Shifts.CountAsync(s => s.UserId == id);
        var stockCreated = await _context.StockTickets.CountAsync(s => s.UserId == id);
        var stockConfirmed = await _context.StockTickets.CountAsync(s => s.ConfirmedBy == id);
        var stockCancelled = await _context.StockTickets.CountAsync(s => s.CancelledBy == id);

        var hasRelations = invoiceCount > 0 || orderCount > 0 || returnOrderCount > 0
            || purchaseOrderCount > 0 || shiftCount > 0
            || stockCreated > 0 || stockConfirmed > 0 || stockCancelled > 0;

        var messages = new List<string>();
        if (invoiceCount > 0) messages.Add($"{invoiceCount} hóa đơn");
        if (orderCount > 0) messages.Add($"{orderCount} đơn hàng");
        if (returnOrderCount > 0) messages.Add($"{returnOrderCount} phiếu trả hàng");
        if (purchaseOrderCount > 0) messages.Add($"{purchaseOrderCount} đơn mua hàng");
        if (shiftCount > 0) messages.Add($"{shiftCount} ca làm việc");
        if (stockCreated > 0) messages.Add($"{stockCreated} phiếu kho (tạo)");
        if (stockConfirmed > 0) messages.Add($"{stockConfirmed} phiếu kho (xác nhận)");
        if (stockCancelled > 0) messages.Add($"{stockCancelled} phiếu kho (hủy)");

        var dto = new AdminAccountRelationsDto
        {
            HasRelations = hasRelations,
            InvoiceCount = invoiceCount,
            OrderCount = orderCount,
            ReturnOrderCount = returnOrderCount,
            PurchaseOrderCount = purchaseOrderCount,
            ShiftCount = shiftCount,
            StockTicketCreatedCount = stockCreated,
            StockTicketConfirmedCount = stockConfirmed,
            StockTicketCancelledCount = stockCancelled,
            Message = hasRelations ? $"Tài khoản có: {string.Join(", ", messages)}" : "Tài khoản chưa có dữ liệu nghiệp vụ."
        };

        return Ok(dto);
    }

    #endregion

    #region DELETE /api/admin/accounts/{id}

    /// <summary>
    /// DELETE /api/admin/accounts/{id} - Soft delete (chuyển Status = DELETED, IsActive = 0).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> SoftDeleteAccount(Guid id)
    {
        var currentAdminId = GetCurrentUserId();

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy tài khoản." } });

        if (currentAdminId == user.UserId)
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-403", Message = "Không thể tự xóa tài khoản của chính mình." }
            });
        }

        if (user.Status == "DELETED")
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-400", Message = "Tài khoản đã bị xóa mềm trước đó." }
            });
        }

        user.Status = "DELETED";
        user.IsActive = 0;
        user.DeletedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await LogAction(user.UserId, "DELETE_USER", "User",
            $"Xóa mềm tài khoản: {user.Email} ({user.FullName})", level: "WARN");

        await _context.SaveChangesAsync();

        return Ok(new { message = $"Đã xóa mềm tài khoản '{user.FullName}'." });
    }

    #endregion

    #region POST /api/admin/accounts/{id}/restore

    /// <summary>
    /// POST /api/admin/accounts/{id}/restore - Khôi phục từ DELETED.
    /// </summary>
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreAccount(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy tài khoản." } });

        if (user.Status != "DELETED")
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-400", Message = "Tài khoản không ở trạng thái DELETED." }
            });
        }

        user.Status = "ACTIVE";
        user.IsActive = 1;
        user.DeletedAt = null;

        await LogAction(user.UserId, "RESTORE_USER", "User",
            $"Khôi phục tài khoản: {user.Email}");

        await _context.SaveChangesAsync();

        return Ok(new { message = $"Đã khôi phục tài khoản '{user.FullName}'." });
    }

    #endregion

    #region DELETE /api/admin/accounts/{id}/permanent

    /// <summary>
    /// DELETE /api/admin/accounts/{id}/permanent - Xóa vĩnh viễn (anonymize, giữ row cho FK).
    /// </summary>
    [HttpDelete("{id:guid}/permanent")]
    public async Task<IActionResult> PermanentDeleteAccount(Guid id)
    {
        var currentAdminId = GetCurrentUserId();

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy tài khoản." } });

        if (currentAdminId == user.UserId)
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-403", Message = "Không thể tự xóa vĩnh viễn tài khoản của chính mình." }
            });
        }

        if (user.Status != "DELETED")
        {
            return BadRequest(new ErrorResponseDto
            {
                Error = new ErrorDetail { Code = "MSG-400", Message = "Chỉ xóa vĩnh viễn tài khoản đã ở trạng thái DELETED." }
            });
        }

        var originalName = user.FullName;
        user.FullName = $"{originalName} (Đã xóa)";
        user.Email = $"deleted_{Guid.NewGuid()}@mep.deleted";
        user.PhoneNumber = null;
        user.PasswordHash = "PERMANENTLY_DELETED";
        user.Status = "PERMANENT_DELETED";
        user.IsActive = 0;

        await LogAction(user.UserId, "PERMANENT_DELETE_USER", "User",
            $"Xóa vĩnh viễn tài khoản: {originalName}", level: "WARN");

        await _context.SaveChangesAsync();

        return Ok(new { message = $"Đã xóa vĩnh viễn tài khoản '{originalName}'. Dữ liệu cá nhân đã được ẩn danh." });
    }

    #endregion

    #region GET /api/admin/accounts/deleted

    /// <summary>
    /// GET /api/admin/accounts/deleted - Danh sách tài khoản đã xóa mềm.
    /// </summary>
    [HttpGet("deleted")]
    public async Task<IActionResult> GetDeletedAccounts()
    {
        var deleted = await _context.Users
            .Include(u => u.Roles)
            .Where(u => u.Status == "DELETED")
            .OrderByDescending(u => u.DeletedAt)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.PhoneNumber,
                u.DeletedAt,
                Roles = u.Roles.Select(r => r.RoleName).ToList()
            })
            .ToListAsync();

        return Ok(deleted);
    }

    #endregion
}
