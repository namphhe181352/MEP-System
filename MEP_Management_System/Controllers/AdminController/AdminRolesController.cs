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
[Route("api/admin/roles")]
[Tags("Admin (Admin)")]
public class AdminRolesController : ControllerBase
{
    private readonly MEPDatabaseContext _context;

    public AdminRolesController(MEPDatabaseContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /api/admin/roles
    /// UC-094: Lấy danh sách Roles
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRoles()
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

        return Ok(roles);
    }

    /// <summary>
    /// GET /api/admin/roles/permissions
    /// Lấy danh sách Permissions (dành cho frontend filter)
    /// </summary>
    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions()
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

        return Ok(permissions);
    }

    /// <summary>
    /// GET /api/admin/roles/permission-matrix
    /// UC-095: View Role Permission Matrix
    /// </summary>
    [HttpGet("permission-matrix")]
    public async Task<IActionResult> GetRolePermissionMatrix()
    {
        // Dam bao tat ca permission can thiet deu ton tai trong DB
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
            .Select(r => new
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

        return Ok(matrix);
    }

    /// <summary>
    /// PUT /api/admin/roles/{id}/permissions
    /// UC-094: Configure Role Permission
    /// </summary>
    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> ConfigureRolePermissions(Guid id, [FromBody] AdminRolePermissionUpdateDto dto)
    {
        var role = await _context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.RoleId == id);

        if (role == null)
            return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Không tìm thấy vai trò (Role)." } });

        // Validate PermissionIds
        if (dto.PermissionIds == null)
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Danh sách quyền không được để trống." } });

        // Check if all permission IDs exist
        var existingPermIdsSet = (await _context.Permissions
            .Where(p => dto.PermissionIds.Contains(p.PermissionId))
            .Select(p => p.PermissionId)
            .ToListAsync())
            .ToHashSet();

        var missingPermIds = dto.PermissionIds.Where(pid => !existingPermIdsSet.Contains(pid)).ToList();

        if (missingPermIds.Any())
        {
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = $"Quyền không tồn tại: {string.Join(", ", missingPermIds)}" } });
        }

        // Chặn cấu hình quyền cho Admin
        if (role.RoleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Không thể chỉnh sửa quyền của Admin." } });
        }

        var permissionsToAssign = await _context.Permissions
            .Where(p => dto.PermissionIds.Contains(p.PermissionId))
            .ToListAsync();

        // Auto-include VIEW permissions khi co quyen thao tac lien quan
        var assignedCodes = permissionsToAssign.Select(p => p.PermissionCode).ToHashSet();
        var actionToViewPrefixes = new Dictionary<string, string[]>
        {
            { "STOCK_INWARD_CREATE", new[] { "STOCK_INWARD_" } },
            { "STOCK_OUTWARD_CREATE", new[] { "STOCK_OUTWARD_" } },
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

        await LogAction(role.RoleId, "UPDATE_ROLE_PERMISSIONS", "role_permissions", "Role", $"Cập nhật quyền cho Role: {role.RoleName}");
        await _context.SaveChangesAsync();

        // Dong bo voi sibling role (vd: "SalesStaff" <-> "Sales Staff")
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

        return Ok(new { message = $"Đã cập nhật quyền cho {role.RoleName} thành công." });
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
            Source = "AdminRoleService",
            Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context.SystemLogs.Add(log);
        return Task.CompletedTask;
    }
}
