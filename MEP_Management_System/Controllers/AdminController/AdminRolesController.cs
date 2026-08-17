using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Services.AdminRoleServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.AdminController;

/// <summary>
/// Thin controller for Admin role management.
/// All business logic moved to AdminRoleService.
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/roles")]
[Tags("Admin (Admin)")]
public class AdminRolesController : ControllerBase
{
    private readonly IAdminRoleService _service;

    public AdminRolesController(IAdminRoleService service)
    {
        _service = service;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var id) ? id : null;
    }

    private Guid GetRequiredAdminId()
    {
        var adminId = GetCurrentUserId();
        if (adminId == null)
            throw new UnauthorizedAccessException("Khong xac dinh duoc nguoi dung hien tai.");
        return adminId.Value;
    }

    /// <summary>
    /// GET /api/admin/roles
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRoles()
    {
        var result = await _service.GetRolesAsync();
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/roles/permissions
    /// </summary>
    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions()
    {
        var result = await _service.GetPermissionsAsync();
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/roles/permission-matrix
    /// </summary>
    [HttpGet("permission-matrix")]
    public async Task<IActionResult> GetRolePermissionMatrix()
    {
        var result = await _service.GetRolePermissionMatrixAsync();
        return Ok(result);
    }

    /// <summary>
    /// PUT /api/admin/roles/{id}/permissions
    /// </summary>
    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> ConfigureRolePermissions(Guid id, [FromBody] AdminRolePermissionUpdateDto dto)
    {
        var adminId = GetRequiredAdminId();
        var message = await _service.ConfigureRolePermissionsAsync(id, dto, adminId);
        return Ok(new { message });
    }
}
