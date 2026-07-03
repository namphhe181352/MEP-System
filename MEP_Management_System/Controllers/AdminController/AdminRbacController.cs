using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.AdminController;

public class PermissionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class RoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public short IsSystem { get; set; }
}

public class RolePermissionUpdateDto
{
    public List<Guid> PermissionIds { get; set; } = new List<Guid>();
}

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/rbac")]
[Tags("Admin (Admin)")]
public class AdminRbacController : ControllerBase
{
    private readonly MEPDatabaseContext _context;

    public AdminRbacController(MEPDatabaseContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /api/admin/rbac/permissions
    /// Gets all available permissions in the system.
    /// </summary>
    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions()
    {
        var permissions = await _context.Permissions
            .OrderBy(p => p.PermissionCode)
            .Select(p => new PermissionDto
            {
                Id = p.PermissionId,
                Code = p.PermissionCode,
                Name = p.PermissionName
            })
            .ToListAsync();

        return Ok(permissions);
    }

    /// <summary>
    /// GET /api/admin/rbac/roles
    /// Gets all roles in the system.
    /// </summary>
    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _context.Roles
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.RoleName)
            .Select(r => new RoleDto
            {
                Id = r.RoleId,
                Name = r.RoleName,
                Description = r.Description ?? "",
                IsSystem = r.IsSystem ?? 0
            })
            .ToListAsync();

        return Ok(roles);
    }

    /// <summary>
    /// GET /api/admin/rbac/roles/{roleId}/permissions
    /// Gets the permissions assigned to a specific role.
    /// </summary>
    [HttpGet("roles/{roleId:guid}/permissions")]
    public async Task<IActionResult> GetRolePermissions(Guid roleId)
    {
        var role = await _context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.RoleId == roleId);

        if (role == null)
            return NotFound(new { message = "Role not found." });

        var permissions = role.Permissions.Select(p => new PermissionDto
        {
            Id = p.PermissionId,
            Code = p.PermissionCode,
            Name = p.PermissionName
        }).ToList();

        return Ok(permissions);
    }

    /// <summary>
    /// PUT /api/admin/rbac/roles/{roleId}/permissions
    /// Updates the permissions for a specific role.
    /// </summary>
    [HttpPut("roles/{roleId:guid}/permissions")]
    public async Task<IActionResult> UpdateRolePermissions(Guid roleId, [FromBody] RolePermissionUpdateDto request)
    {
        var role = await _context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.RoleId == roleId);

        if (role == null)
            return NotFound(new { message = "Role not found." });

        // Retrieve the full permission objects based on the provided IDs
        var permissionsToAdd = await _context.Permissions
            .Where(p => request.PermissionIds.Contains(p.PermissionId))
            .ToListAsync();

        // Validate that all requested permissions exist
        if (permissionsToAdd.Count != request.PermissionIds.Count)
        {
            var foundIds = permissionsToAdd.Select(p => p.PermissionId).ToList();
            var missingIds = request.PermissionIds.Except(foundIds).ToList();
            return BadRequest(new { message = "One or more permissions do not exist.", missingIds });
        }

        // Clear existing permissions and add the new ones
        role.Permissions.Clear();
        foreach (var p in permissionsToAdd)
        {
            role.Permissions.Add(p);
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "Role permissions updated successfully." });
    }
}
