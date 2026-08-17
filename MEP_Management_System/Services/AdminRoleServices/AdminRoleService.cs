using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.AdminRoleServices;

/// <summary>
/// Service xu ly logic nghiep vu cho Admin quan ly vai tro va quyen.
/// </summary>
public class AdminRoleService : IAdminRoleService
{
    private readonly MEPDatabaseContext _context;

    public AdminRoleService(MEPDatabaseContext context)
    {
        _context = context;
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
            Source = "AdminRoleService",
            Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
        await Task.CompletedTask;
    }

    public async Task<List<AdminRoleDto>> GetRolesAsync()
    {
        var roles = await _context.Roles
            .OrderBy(r => r.RoleName)
            .Select(r => new AdminRoleDto
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                Description = r.Description
            })
            .ToListAsync();

        return roles;
    }

    public async Task<List<AdminPermissionDto>> GetPermissionsAsync()
    {
        var permissions = await _context.Permissions
            .OrderBy(p => p.PermissionCode)
            .Select(p => new AdminPermissionDto
            {
                PermissionId = p.PermissionId,
                PermissionCode = p.PermissionCode,
                PermissionName = p.PermissionName
            })
            .ToListAsync();

        return permissions;
    }

    public async Task<List<object>> GetRolePermissionMatrixAsync()
    {
        var requiredCodes = new[] {
            "EXPENSE_VIEW", "EXPENSE_CREATE", "EXPENSE_CONFIRM", "EXPENSE_CANCEL", "EXPENSE_CATEGORY_MANAGE",
            "STAFF_VIEW", "STAFF_CREATE", "STAFF_UPDATE", "STAFF_DELETE", "STAFF_ASSIGN_BRANCH",
            "SHIFT_VIEW", "SHIFT_CREATE", "SHIFT_UPDATE", "SHIFT_FORCE_CLOSE",
            "PRINT_VIEW", "PROMOTION_VIEW",
            "PRODUCT_CATEGORY_MANAGE", "STOCK_CHECK_COUNT"
        };

        var existingCodes = await _context.Permissions
            .Where(p => requiredCodes.Contains(p.PermissionCode))
            .Select(p => p.PermissionCode)
            .ToListAsync();

        var missingCodes = requiredCodes.Where(c => !existingCodes.Contains(c)).ToList();
        foreach (var code in missingCodes)
        {
            _context.Permissions.Add(new Permission
            {
                PermissionId = Guid.NewGuid(),
                PermissionCode = code,
                PermissionName = code
            });
        }
        if (missingCodes.Any()) await _context.SaveChangesAsync();

        var matrix = await _context.Roles
            .Include(r => r.Permissions)
            .OrderBy(r => r.RoleName)
            .Select(r => (object)new
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                Permissions = r.Permissions.Select(p => new AdminPermissionDto
                {
                    PermissionId = p.PermissionId,
                    PermissionCode = p.PermissionCode,
                    PermissionName = p.PermissionName
                }).ToList()
            })
            .ToListAsync();

        return matrix;
    }

    public async Task<string> ConfigureRolePermissionsAsync(Guid id, AdminRolePermissionUpdateDto dto, Guid currentAdminId)
    {
        var role = await _context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.RoleId == id);

        if (role == null)
            throw new KeyNotFoundException("Khong tim thay vai tro (Role).");

        if (dto.PermissionIds == null)
            throw new BusinessException("Danh sach quyen khong duoc de trong.");

        var existingPermIdsSet = (await _context.Permissions
            .Where(p => dto.PermissionIds.Contains(p.PermissionId))
            .Select(p => p.PermissionId)
            .ToListAsync())
            .ToHashSet();

        var missingPermIds = dto.PermissionIds.Where(pid => !existingPermIdsSet.Contains(pid)).ToList();

        if (missingPermIds.Any())
            throw new BusinessException($"Quyen khong ton tai: {string.Join(", ", missingPermIds)}");

        if (role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("Khong the chinh sua quyen cua Admin.");

        var permissionsToAssign = await _context.Permissions
            .Where(p => dto.PermissionIds.Contains(p.PermissionId))
            .ToListAsync();

        var assignedCodes = permissionsToAssign.Select(p => p.PermissionCode).ToHashSet();
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
            if (!assignedCodes.Contains(viewCode) &&
                assignedCodes.Any(rc => actionPrefixes.Any(ap => rc.StartsWith(ap))))
            {
                var viewPerm = await _context.Permissions.FirstOrDefaultAsync(p => p.PermissionCode == viewCode);
                if (viewPerm != null && !permissionsToAssign.Any(p => p.PermissionId == viewPerm.PermissionId))
                {
                    permissionsToAssign.Add(viewPerm);
                }
            }
        }

        role.Permissions.Clear();
        foreach (var permission in permissionsToAssign)
        {
            role.Permissions.Add(permission);
        }

        await LogActionWithUser(currentAdminId, role.RoleId, "UPDATE_ROLE_PERMISSIONS", "role_permissions", "Role",
            $"Cap nhat quyen cho Role: {role.RoleName}");

        await _context.SaveChangesAsync();

        var normalizedName = role.RoleName.Trim();
        string? siblingName = null;
        if (normalizedName.Equals("SalesStaff", StringComparison.OrdinalIgnoreCase))
            siblingName = "Sales Staff";
        else if (normalizedName.Equals("Sales Staff", StringComparison.OrdinalIgnoreCase))
            siblingName = "SalesStaff";
        else if (normalizedName.Equals("InventoryStaff", StringComparison.OrdinalIgnoreCase))
            siblingName = "Inventory Staff";
        else if (normalizedName.Equals("Inventory Staff", StringComparison.OrdinalIgnoreCase))
            siblingName = "InventoryStaff";

        if (siblingName != null)
        {
            var siblingRole = await _context.Roles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.RoleName == siblingName);
            if (siblingRole != null)
            {
                var siblingPermissions = await _context.Permissions
                    .Where(p => dto.PermissionIds.Contains(p.PermissionId))
                    .ToListAsync();
                siblingRole.Permissions.Clear();
                foreach (var p in siblingPermissions)
                    siblingRole.Permissions.Add(p);
                await _context.SaveChangesAsync();
            }
        }

        return $"Da cap nhat quyen cho {role.RoleName} thanh cong.";
    }
}
