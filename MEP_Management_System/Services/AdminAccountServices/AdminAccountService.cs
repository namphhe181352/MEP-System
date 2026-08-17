using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.AdminAccountServices;

/// <summary>
/// Service xu ly logic nghiep vu cho Admin quan ly tai khoan (Owner + Staff).
/// </summary>
public class AdminAccountService : IAdminAccountService
{
    private readonly MEPDatabaseContext _context;

    public AdminAccountService(MEPDatabaseContext context)
    {
        _context = context;
    }

    #region Helper Methods

    private async Task LogActionWithUser(Guid adminId, Guid targetEntityId, string action, string entityType, string description, string level = "INFO")
    {
        _context.SystemLogs.Add(new SystemLog
        {
            LogId = Guid.NewGuid(),
            UserId = adminId,
            Action = action,
            TableName = "users",
            EntityType = entityType,
            EntityId = targetEntityId,
            Description = description,
            Level = level,
            Source = "AdminAccountService",
            Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
        await Task.CompletedTask;
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
        var query = _context.Users.Where(u => u.Email.ToLower() == normalized && u.IsActive != 0);
        if (excludeUserId.HasValue)
            query = query.Where(u => u.UserId != excludeUserId.Value);
        return await query.AnyAsync();
    }

    private async Task<bool> IsUsernameTakenAsync(string username, Guid? excludeUserId = null)
    {
        var normalized = username.Trim().ToLower();
        var query = _context.Users.Where(u => u.Username.ToLower() == normalized && u.IsActive != 0);
        if (excludeUserId.HasValue)
            query = query.Where(u => u.UserId != excludeUserId.Value);
        return await query.AnyAsync();
    }

    private async Task<bool> IsPhoneTakenAsync(string phone, Guid? excludeUserId = null)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        var normalized = phone.Trim();
        var query = _context.Users.Where(u => u.PhoneNumber == normalized && u.IsActive != 0);
        if (excludeUserId.HasValue)
            query = query.Where(u => u.UserId != excludeUserId.Value);
        return await query.AnyAsync();
    }

    #endregion

    #region Query Methods

    public async Task<PageResultDto<AdminAccountDetailDto>> GetAccountsAsync(
        string? role, string? status, string? search, int page, int pageSize)
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

        if (!string.IsNullOrWhiteSpace(role))
        {
            if (role.Equals("owner", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => u.Roles.Any(r => r.RoleName == "Owner"));
            else if (role.Equals("staff", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => !u.Roles.Any(r => r.RoleName == "Admin" || r.RoleName == "Owner"));
        }

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
            query = query.Where(u => u.Status.ToUpper() != "DELETED" && u.Status.ToUpper() != "PERMANENT_DELETED");
        }

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

        return new PageResultDto<AdminAccountDetailDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<AdminAccountDetailDto> GetAccountByIdAsync(Guid id)
    {
        var user = await _context.Users
            .Include(u => u.Roles)
            .Include(u => u.StaffAssignments)
            .Include(u => u.UserPermissions).ThenInclude(up => up.Permission)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            throw new KeyNotFoundException("Khong tim thay tai khoan.");

        return await MapToDtoAsync(user);
    }

    public async Task<PageResultDto<SystemLogDto>> GetAccountActivitiesAsync(Guid id, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var userExists = await _context.Users.AnyAsync(u => u.UserId == id);
        if (!userExists)
            throw new KeyNotFoundException("Khong tim thay nguoi dung.");

        var query = _context.SystemLogs
            .Include(l => l.User)
            .Where(l => l.UserId == id || (l.EntityType == "User" && l.EntityId == id));

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

    #endregion

    #region Create Methods

    public async Task<Guid> CreateOwnerAsync(AdminCreateOwnerRequest dto, Guid currentAdminId)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new BusinessException("Email la bat buoc.");

        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new BusinessException("Ho ten la bat buoc.");

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            throw new BusinessException("Mat khau phai co it nhat 6 ky tu.");

        if (!dto.BranchId.HasValue && string.IsNullOrWhiteSpace(dto.BranchName))
            throw new BusinessException("Ten cua hang la bat buoc khi tao Owner moi.");

        if (await IsEmailTakenAsync(dto.Email))
            throw new BusinessException("Email da duoc su dung.");

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber) && await IsPhoneTakenAsync(dto.PhoneNumber))
            throw new BusinessException("So dien thoai da duoc su dung.");

        var username = dto.Email.Trim();
        var ownerRole = await GetOrCreateRoleAsync("Owner", "Chu cua hang - toan quyen quan ly chi nhanh");

        Guid branchId;
        Branch branch;
        if (dto.BranchId.HasValue && dto.BranchId.Value != Guid.Empty)
        {
            var existingBranch = await _context.Branches
                .FirstOrDefaultAsync(b => b.BranchId == dto.BranchId.Value && b.IsActive == 1);
            if (existingBranch == null)
                throw new KeyNotFoundException("Chi nhanh khong ton tai hoac da bi vo hieu hoa.");

            var hasOwner = await _context.Users.AnyAsync(u =>
                u.DefaultBranchId == dto.BranchId.Value &&
                u.IsActive == 1 &&
                u.Roles.Any(r => r.RoleName == "Owner"));
            if (hasOwner)
                throw new BusinessException("Chi nhanh nay da co Owner. Moi chi nhanh chi duoc phep co 1 Owner.");

            branch = existingBranch;
            branchId = existingBranch.BranchId;
        }
        else
        {
            var branchNameTrimmed = dto.BranchName!.Trim();
            var nameExists = await _context.Branches.AnyAsync(b => b.BranchName.ToLower() == branchNameTrimmed.ToLower());
            if (nameExists)
                throw new BusinessException("Ten cua hang da ton tai.");

            var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            var newBranch = new Branch
            {
                BranchId = Guid.NewGuid(),
                BranchName = branchNameTrimmed,
                Phone = dto.PhoneNumber,
                ManagerUserId = null,
                IsActive = 1,
                CreatedAt = now,
                UpdatedAt = now
            };
            _context.Branches.Add(newBranch);
            await _context.SaveChangesAsync();
            branch = newBranch;
            branchId = newBranch.BranchId;
        }

        var nowUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var newUserId = Guid.NewGuid();
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

        if (branch.ManagerUserId == null)
            branch.ManagerUserId = newUser.UserId;

        _context.StaffAssignments.Add(new StaffAssignment
        {
            AssignmentId = Guid.NewGuid(),
            UserId = newUser.UserId,
            BranchId = branchId,
            AssignedAt = nowUtc
        });

        await LogActionWithUser(currentAdminId, newUser.UserId, "CREATE_OWNER", "User",
            $"Tao Owner: {newUser.FullName} ({newUser.Email}) — Branch: {branchId}");

        await _context.SaveChangesAsync();

        return newUser.UserId;
    }

    public async Task<Guid> CreateStaffAsync(AdminCreateStaffRequest dto, Guid currentAdminId)
    {
        if (string.IsNullOrWhiteSpace(dto.Username) ||
            string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.FullName) ||
            string.IsNullOrWhiteSpace(dto.Password) ||
            string.IsNullOrWhiteSpace(dto.RoleName))
        {
            throw new BusinessException("Thieu thong tin bat buoc.");
        }

        if (dto.Password.Length < 6)
            throw new BusinessException("Mat khau phai co it nhat 6 ky tu.");

        var validRoleNames = new[] { "SalesStaff", "InventoryStaff" };
        if (!validRoleNames.Contains(dto.RoleName.Trim(), StringComparer.OrdinalIgnoreCase))
            throw new BusinessException("Role khong hop le. Chi chap nhan: SalesStaff, InventoryStaff.");

        if (await IsEmailTakenAsync(dto.Email))
            throw new BusinessException("Email da duoc su dung.");

        if (await IsUsernameTakenAsync(dto.Username))
            throw new BusinessException("Username da ton tai.");

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber) && await IsPhoneTakenAsync(dto.PhoneNumber))
            throw new BusinessException("So dien thoai da duoc su dung.");

        var roleNameToSearch = dto.RoleName.Trim();
        if (roleNameToSearch.Equals("SalesStaff", StringComparison.OrdinalIgnoreCase))
            roleNameToSearch = "Sales Staff";
        else if (roleNameToSearch.Equals("InventoryStaff", StringComparison.OrdinalIgnoreCase))
            roleNameToSearch = "Inventory Staff";

        var staffRole = await GetOrCreateRoleAsync(roleNameToSearch, $"Role {dto.RoleName} - tu dong tao");
        await _context.Entry(staffRole).Collection(r => r.Permissions).LoadAsync();

        Guid? resolvedBranchId = dto.BranchId;
        if (resolvedBranchId == null || resolvedBranchId == Guid.Empty)
        {
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
                throw new KeyNotFoundException("Chi nhanh khong ton tai hoac da bi vo hieu hoa.");
        }

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
            permissionCodesToApply = staffRole.Permissions
                .Select(p => p.PermissionCode)
                .ToList();
        }

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
                throw new BusinessException($"Ma quyen khong ton tai: {string.Join(", ", missing)}");
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

            await LogActionWithUser(currentAdminId, newUser.UserId, "CREATE_STAFF", "User",
                $"Tao Staff: {newUser.FullName} ({newUser.Email}) — Role: {dto.RoleName}");

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return newUser.UserId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    #endregion

    #region Update Methods

    public async Task UpdateAccountAsync(Guid id, AdminUpdateAccountRequest dto, Guid currentAdminId)
    {
        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            throw new KeyNotFoundException("Khong tim thay tai khoan.");

        if (currentAdminId == user.UserId)
            throw new BusinessException("Khong the tu sua tai khoan cua chinh minh qua endpoint nay.");

        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new BusinessException("Ho ten la bat buoc.");

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            if (await IsEmailTakenAsync(dto.Email, user.UserId))
                throw new BusinessException("Email da duoc su dung.");
            user.Email = dto.Email.Trim();
        }

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            if (await IsPhoneTakenAsync(dto.PhoneNumber, user.UserId))
                throw new BusinessException("So dien thoai da duoc su dung.");
            user.PhoneNumber = dto.PhoneNumber.Trim();
        }

        user.FullName = dto.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(dto.SubscriptionPlan))
            user.SubscriptionPlan = dto.SubscriptionPlan;

        await LogActionWithUser(currentAdminId, user.UserId, "UPDATE_ACCOUNT", "User",
            $"Cap nhat tai khoan: {user.Email}");

        await _context.SaveChangesAsync();
    }

    public async Task AssignRolesAsync(Guid id, AdminAssignRolesRequest dto, Guid currentAdminId)
    {
        var user = await _context.Users.Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            throw new KeyNotFoundException("Khong tim thay tai khoan.");

        if (dto.RoleId == Guid.Empty)
            throw new BusinessException("Phai chon mot vai tro.");

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);
        if (role == null)
            throw new BusinessException("Vai tro khong ton tai.");

        if (role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("Khong the gan vai tro Admin.");

        if (role.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase) && user.DefaultBranchId.HasValue)
        {
            var hasOwner = await _context.Users.AnyAsync(u =>
                u.UserId != user.UserId &&
                u.DefaultBranchId == user.DefaultBranchId.Value &&
                u.IsActive == 1 &&
                u.Roles.Any(r => r.RoleName == "Owner"));
            if (hasOwner)
                throw new BusinessException("Chi nhanh cua nhan vien nay da co Owner. Moi chi nhanh chi duoc phep co 1 Owner.");
        }

        if (currentAdminId == user.UserId)
            throw new BusinessException("Khong the tu thay doi vai tro cua chinh minh.");

        user.Roles.Clear();
        user.Roles.Add(role);

        await LogActionWithUser(currentAdminId, user.UserId, "ASSIGN_ROLE", "User",
            $"Gan vai tro [{role.RoleName}] cho: {user.Email}");

        await _context.SaveChangesAsync();
    }

    public async Task AssignPermissionsAsync(Guid id, AdminAssignPermissionsRequest dto, Guid currentAdminId)
    {
        var user = await _context.Users
            .Include(u => u.Roles).ThenInclude(r => r.Permissions)
            .Include(u => u.UserPermissions)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            throw new KeyNotFoundException("Khong tim thay tai khoan.");

        if (dto.PermissionCodes == null)
            throw new BusinessException("Danh sach quyen khong duoc de trong.");

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
            throw new BusinessException($"Ma quyen khong ton tai: {string.Join(", ", missing)}");

        var rolePermissionCodes = user.Roles
            .SelectMany(r => r.Permissions)
            .Select(p => p.PermissionCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requestedSet = validPermissions.Select(p => p.PermissionCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _context.UserPermissions.RemoveRange(user.UserPermissions);

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

        await LogActionWithUser(currentAdminId, user.UserId, "ASSIGN_PERMISSIONS", "User",
            $"Gan {validPermissions.Count} quyen cho: {user.Email}");

        await _context.SaveChangesAsync();
    }

    public async Task AssignBranchAsync(Guid id, AdminAssignBranchRequest dto, Guid currentAdminId)
    {
        var branch = await _context.Branches
            .FirstOrDefaultAsync(b => b.BranchId == dto.BranchId && b.IsActive == 1);
        if (branch == null)
            throw new KeyNotFoundException("Chi nhanh khong ton tai hoac da bi vo hieu hoa.");

        var user = await _context.Users
            .Include(u => u.StaffAssignments)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            throw new KeyNotFoundException("Khong tim thay tai khoan.");

        var isOwner = user.Roles.Any(r => r.RoleName.Equals("Owner", StringComparison.OrdinalIgnoreCase));
        if (isOwner)
        {
            var hasOwner = await _context.Users.AnyAsync(u =>
                u.UserId != user.UserId &&
                u.DefaultBranchId == branch.BranchId &&
                u.IsActive == 1 &&
                u.Roles.Any(r => r.RoleName == "Owner"));
            if (hasOwner)
                throw new BusinessException("Chi nhanh moi da co Owner. Moi chi nhanh chi duoc phep co 1 Owner.");
        }

        _context.StaffAssignments.RemoveRange(user.StaffAssignments);
        _context.StaffAssignments.Add(new StaffAssignment
        {
            AssignmentId = Guid.NewGuid(),
            UserId = user.UserId,
            BranchId = branch.BranchId,
            AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });

        user.DefaultBranchId = branch.BranchId;

        await LogActionWithUser(currentAdminId, user.UserId, "ASSIGN_BRANCH", "User",
            $"Gan chi nhanh {branch.BranchName} cho: {user.Email}");

        await _context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(Guid id, AdminToggleStatusRequest dto, Guid currentAdminId)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            throw new KeyNotFoundException("Khong tim thay tai khoan.");

        if (currentAdminId == user.UserId)
            throw new BusinessException("Khong the tu thay doi trang thai cua chinh minh.");

        var oldStatus = user.Status;
        user.IsActive = (short)(dto.IsActive ? 1 : 0);
        user.Status = dto.IsActive ? "ACTIVE" : "SUSPENDED";

        var actionCode = dto.IsActive ? "ACTIVATE_USER" : "DEACTIVATE_USER";
        await LogActionWithUser(currentAdminId, user.UserId, actionCode, "User",
            $"Doi trang thai {oldStatus} → {user.Status}: {user.Email}");

        await _context.SaveChangesAsync();
    }

    public async Task ResetPasswordAsync(Guid id, AdminResetPasswordRequest dto, Guid currentAdminId)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
            throw new BusinessException("Mat khau phai co it nhat 6 ky tu.");

        var user = await _context.Users.FindAsync(id);
        if (user == null)
            throw new KeyNotFoundException("Khong tim thay tai khoan.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

        await LogActionWithUser(currentAdminId, user.UserId, "RESET_PASSWORD", "User",
            $"Reset mat khau cho: {user.Email}");

        await _context.SaveChangesAsync();
    }

    #endregion

    #region Relations & Delete Methods

    public async Task<AdminAccountRelationsDto> CheckRelationsAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            throw new KeyNotFoundException("Khong tim thay tai khoan.");

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
        if (invoiceCount > 0) messages.Add($"{invoiceCount} hoa don");
        if (orderCount > 0) messages.Add($"{orderCount} don hang");
        if (returnOrderCount > 0) messages.Add($"{returnOrderCount} phieu tra hang");
        if (purchaseOrderCount > 0) messages.Add($"{purchaseOrderCount} don mua hang");
        if (shiftCount > 0) messages.Add($"{shiftCount} ca lam viec");
        if (stockCreated > 0) messages.Add($"{stockCreated} phieu kho (tao)");
        if (stockConfirmed > 0) messages.Add($"{stockConfirmed} phieu kho (xac nhan)");
        if (stockCancelled > 0) messages.Add($"{stockCancelled} phieu kho (huy)");

        return new AdminAccountRelationsDto
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
            Message = hasRelations ? $"Tai khoan co: {string.Join(", ", messages)}" : "Tai khoan chua co du lieu nghiep vu."
        };
    }

    public async Task SoftDeleteAccountAsync(Guid id, Guid currentAdminId)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            throw new KeyNotFoundException("Khong tim thay tai khoan.");

        if (currentAdminId == user.UserId)
            throw new BusinessException("Khong the tu xoa tai khoan cua chinh minh.");

        if (user.Status == "DELETED")
            throw new BusinessException("Tai khoan da bi xoa mem truoc do.");

        user.Status = "DELETED";
        user.IsActive = 0;
        user.DeletedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var shortId = user.UserId.ToString().Substring(0, 6);

        var newEmail = $"del_{shortId}_{user.Email}";
        if (newEmail.Length > 150) newEmail = newEmail.Substring(0, 150);
        user.Email = newEmail;

        var newUsername = $"del_{shortId}_{user.Username}";
        if (newUsername.Length > 50) newUsername = newUsername.Substring(0, 50);
        user.Username = newUsername;

        user.PhoneNumber = null;

        await LogActionWithUser(currentAdminId, user.UserId, "DELETE_USER", "User",
            $"Xoa mem tai khoan: {user.Email} ({user.FullName})", level: "WARN");

        await _context.SaveChangesAsync();
    }

    public async Task RestoreAccountAsync(Guid id, Guid currentAdminId)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            throw new KeyNotFoundException("Khong tim thay tai khoan.");

        if (user.Status != "DELETED")
            throw new BusinessException("Tai khoan khong o trang thai DELETED.");

        user.Status = "ACTIVE";
        user.IsActive = 1;
        user.DeletedAt = null;

        await LogActionWithUser(currentAdminId, user.UserId, "RESTORE_USER", "User",
            $"Khoi phuc tai khoan: {user.Email}");

        await _context.SaveChangesAsync();
    }

    public async Task PermanentDeleteAccountAsync(Guid id, Guid currentAdminId)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            throw new KeyNotFoundException("Khong tim thay tai khoan.");

        if (currentAdminId == user.UserId)
            throw new BusinessException("Khong the tu xoa vinh vien tai khoan cua chinh minh.");

        if (user.Status != "DELETED")
            throw new BusinessException("Chi xoa vinh vien tai khoan da o trang thai DELETED.");

        var originalName = user.FullName;
        user.FullName = $"{originalName} (Da xoa)";
        user.Email = $"deleted_{Guid.NewGuid()}@mep.deleted";
        user.PhoneNumber = null;
        user.PasswordHash = "PERMANENTLY_DELETED";
        user.Status = "PERMANENT_DELETED";
        user.IsActive = 0;

        await LogActionWithUser(currentAdminId, user.UserId, "PERMANENT_DELETE_USER", "User",
            $"Xoa vinh vien tai khoan: {originalName}", level: "WARN");

        await _context.SaveChangesAsync();
    }

    public async Task<List<object>> GetDeletedAccountsAsync()
    {
        var deleted = await _context.Users
            .Include(u => u.Roles)
            .Where(u => u.Status == "DELETED")
            .OrderByDescending(u => u.DeletedAt)
            .Select(u => (object)new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.PhoneNumber,
                u.DeletedAt,
                Roles = u.Roles.Select(r => r.RoleName).ToList()
            })
            .ToListAsync();

        return deleted;
    }

    #endregion
}
