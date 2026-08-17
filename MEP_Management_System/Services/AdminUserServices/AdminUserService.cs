using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.AdminUserServices;

/// <summary>
/// Service implementing Admin user management business logic.
/// </summary>
public class AdminUserService : IAdminUserService
{
    private readonly MEPDatabaseContext _context;

    public AdminUserService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<PageResultDto<AdminUserDto>> GetUsersAsync(string? search, string? roleId, int page, int pageSize)
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

        return new PageResultDto<AdminUserDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminUserDetailDto> GetUserByIdAsync(Guid id)
    {
        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            throw new KeyNotFoundException("Khong tim thay nguoi dung.");

        var branchName = await _context.Branches
            .Where(b => b.BranchId == user.DefaultBranchId)
            .Select(b => b.BranchName)
            .FirstOrDefaultAsync();

        return new AdminUserDetailDto
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
            BranchName = branchName,
            Roles = user.Roles.Select(r => new AdminRoleDto
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                Description = r.Description
            }).ToList()
        };
    }

    public async Task<IdResponseDto> CreateUserAsync(AdminUserCreateDto dto, Guid? currentAdminId)
    {
        // Validate required fields
        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new BusinessException("MSG-400", "Email la bat buoc.");

        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new BusinessException("MSG-400", "Ho ten la bat buoc.");

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            throw new BusinessException("MSG-400", "Mat khau phai co it nhat 6 ky tu.");

        var emailTrimmed = dto.Email.Trim().ToLower();
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == emailTrimmed && u.IsActive != 0))
            throw new BusinessException("MSG-409", "Email da ton tai.");

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            var phoneTrimmed = dto.PhoneNumber.Trim();
            if (await _context.Users.AnyAsync(u => u.PhoneNumber == phoneTrimmed && u.IsActive != 0))
                throw new BusinessException("MSG-409", "So dien thoai da ton tai.");
        }

        // Validate RoleIds - check if roles exist and prevent assigning Admin role
        var validRoles = new List<Role>();
        if (dto.RoleIds != null && dto.RoleIds.Any())
        {
            var roles = await _context.Roles.Where(r => dto.RoleIds.Contains(r.RoleId)).ToListAsync();

            // Check if trying to assign Admin role
            var adminRole = roles.FirstOrDefault(r => r.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase));
            if (adminRole != null)
            {
                throw new BusinessException("MSG-403", "Khong the gan vai tro Admin.");
            }

            // Block Owner role creation via this endpoint
            if (roles.Any(r => r.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase)))
                throw new BusinessException("MSG-403", "Vui long dung endpoint POST /api/admin/accounts/owners de tao Owner.");

            validRoles = roles;
        }

        // Resolve branch: use BranchId provided or get first branch
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
                throw new BusinessException("MSG-404", "Chi nhanh khong ton tai hoac da bi vo hieu hoa.");
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

        // Create staff assignment if branch is available
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
        await LogActionAsync(currentAdminId, user.UserId, "CREATE_USER", "users", "User", $"Tao nguoi dung: {user.FullName} ({user.Email})");
        await _context.SaveChangesAsync();

        return new IdResponseDto { Id = user.UserId };
    }

    public async Task UpdateUserAsync(Guid id, AdminUserUpdateDto dto, Guid? currentAdminId)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            throw new KeyNotFoundException("Khong tim thay nguoi dung.");

        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new BusinessException("MSG-400", "Ho ten la bat buoc.");

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            var phoneTrimmed = dto.PhoneNumber.Trim();
            if (phoneTrimmed != (user.PhoneNumber ?? "") && await _context.Users.AnyAsync(u => u.PhoneNumber == phoneTrimmed && u.IsActive != 0))
                throw new BusinessException("MSG-409", "So dien thoai da ton tai.");
        }

        user.FullName = dto.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();

        await LogActionAsync(currentAdminId, user.UserId, "UPDATE_USER", "users", "User", $"Cap nhat nguoi dung: {user.Email}");
        await _context.SaveChangesAsync();
    }

    public async Task AssignUserRoleAsync(Guid id, AdminUserRoleUpdateDto dto, Guid? currentAdminId)
    {
        var user = await _context.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.UserId == id);
        if (user == null)
            throw new KeyNotFoundException("Khong tim thay nguoi dung.");

        if (dto.RoleId == Guid.Empty)
            throw new BusinessException("MSG-400", "Phai chon mot vai tro.");

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);
        if (role == null)
            throw new BusinessException("MSG-400", "Vai tro khong ton tai.");

        // Block Admin role
        if (role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("MSG-403", "Khong the gan vai tro Admin.");

        // Check Owner uniqueness per branch
        if (role.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase) && user.DefaultBranchId.HasValue)
        {
            var hasOwner = await _context.Users.AnyAsync(u =>
                u.UserId != user.UserId &&
                u.DefaultBranchId == user.DefaultBranchId.Value &&
                u.IsActive == 1 &&
                u.Roles.Any(r => r.RoleName == "Owner"));

            if (hasOwner)
                throw new BusinessException("MSG-409", "Chi nhanh nay da co Owner. Moi chi nhanh chi duoc phep co 1 Owner.");
        }

        user.Roles.Clear();
        user.Roles.Add(role);

        await LogActionAsync(currentAdminId, user.UserId, "ASSIGN_ROLE", "User", "User", $"Phan vai tro [{role.RoleName}] cho tai khoan: {user.Email}");
        await _context.SaveChangesAsync();
    }

    public async Task UpdateUserStatusAsync(Guid id, AdminUserStatusUpdateDto dto, Guid? currentAdminId)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            throw new KeyNotFoundException("Khong tim thay nguoi dung.");

        // Prevent admin from locking themselves
        if (currentAdminId == user.UserId)
        {
            throw new BusinessException("MSG-403", "Khong the thay doi trang thai cua chinh minh.");
        }

        user.IsActive = (short)(dto.IsActive ? 1 : 0);
        user.Status = dto.IsActive ? "ACTIVE" : "SUSPENDED";

        string actionType = dto.IsActive ? "ACTIVATE_USER" : "DEACTIVATE_USER";
        await LogActionAsync(currentAdminId, user.UserId, actionType, "users", "User", $"Doi trang thai: {user.Status}");
        await _context.SaveChangesAsync();
    }

    public async Task ResetUserPasswordAsync(Guid id, AdminUserResetPasswordDto dto, Guid? currentAdminId)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            throw new KeyNotFoundException("Khong tim thay nguoi dung.");

        // Validate password
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
            throw new BusinessException("MSG-400", "Mat khau phai co it nhat 6 ky tu.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

        await LogActionAsync(currentAdminId, user.UserId, "RESET_PASSWORD", "users", "User", $"Reset mat khau cho: {user.Email}");
        await _context.SaveChangesAsync();
    }

    public async Task<PageResultDto<SystemLogDto>> GetUserActivitiesAsync(Guid id, int page, int pageSize)
    {
        // Verify user exists
        var userExists = await _context.Users.AnyAsync(u => u.UserId == id);
        if (!userExists)
            throw new KeyNotFoundException("Khong tim thay nguoi dung.");

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

        return new PageResultDto<SystemLogDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    private async Task LogActionAsync(Guid? adminId, Guid targetEntityId, string action, string tableName, string entityType, string description)
    {
        if (adminId == null) return;

        _context.SystemLogs.Add(new SystemLog
        {
            LogId = Guid.NewGuid(),
            UserId = adminId.Value,
            Action = action,
            TableName = tableName,
            EntityType = entityType,
            EntityId = targetEntityId,
            Description = description,
            Level = "INFO",
            Source = "AdminUserService",
            Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
    }
}
