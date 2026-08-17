using System;
using System.Security.Claims;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Services.AdminUserServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.AdminController;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/users")]
[Tags("Admin (Admin)")]
public class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;

    public AdminUsersController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var id) ? id : null;
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
        var result = await _adminUserService.GetUsersAsync(search, roleId, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/users/{id}
    /// UC-086: View User Detail
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var dto = await _adminUserService.GetUserByIdAsync(id);
        return Ok(dto);
    }

    /// <summary>
    /// POST /api/admin/users
    /// UC-087: Create User Account
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] AdminUserCreateDto dto)
    {
        var result = await _adminUserService.CreateUserAsync(dto, GetCurrentUserId());
        return CreatedAtAction(nameof(GetUserById), new { id = result.Id }, result);
    }

    /// <summary>
    /// PUT /api/admin/users/{id}
    /// UC-088: Update User Information
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] AdminUserUpdateDto dto)
    {
        await _adminUserService.UpdateUserAsync(id, dto, GetCurrentUserId());
        return Ok(new { message = "Cap nhat thong tin thanh cong." });
    }

    /// <summary>
    /// PUT /api/admin/users/{id}/roles
    /// UC-089: Assign User Role
    /// </summary>
    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> AssignUserRole(Guid id, [FromBody] AdminUserRoleUpdateDto dto)
    {
        await _adminUserService.AssignUserRoleAsync(id, dto, GetCurrentUserId());
        return Ok(new { message = "Phan vai tro thanh cong." });
    }

    /// <summary>
    /// PATCH /api/admin/users/{id}/status
    /// UC-090 & UC-091: Activate/Deactivate User Account
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] AdminUserStatusUpdateDto dto)
    {
        await _adminUserService.UpdateUserStatusAsync(id, dto, GetCurrentUserId());
        return Ok(new { message = dto.IsActive ? "Tai khoan da duoc kich hoat." : "Tai khoan da bi khoa." });
    }

    /// <summary>
    /// POST /api/admin/users/{id}/reset-password
    /// UC-092: Reset User Password
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetUserPassword(Guid id, [FromBody] AdminUserResetPasswordDto dto)
    {
        await _adminUserService.ResetUserPasswordAsync(id, dto, GetCurrentUserId());
        return Ok(new { message = "Mat khau da duoc dat lai thanh cong." });
    }

    /// <summary>
    /// GET /api/admin/users/{id}/activities
    /// UC-093: View User Activity Log
    /// </summary>
    [HttpGet("{id:guid}/activities")]
    public async Task<IActionResult> GetUserActivities(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _adminUserService.GetUserActivitiesAsync(id, page, pageSize);
        return Ok(result);
    }
}
