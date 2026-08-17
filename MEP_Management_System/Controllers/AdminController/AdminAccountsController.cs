using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Services.AdminAccountServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.AdminController;

/// <summary>
/// Thin controller for Admin account management.
/// All business logic moved to AdminAccountService.
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/accounts")]
[Tags("Admin (Admin)")]
public class AdminAccountsController : ControllerBase
{
    private readonly IAdminAccountService _service;

    public AdminAccountsController(IAdminAccountService service)
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
    /// GET /api/admin/accounts
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetAccountsAsync(role, status, search, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/accounts/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAccountById(Guid id)
    {
        var result = await _service.GetAccountByIdAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/accounts/{id}/activities
    /// </summary>
    [HttpGet("{id:guid}/activities")]
    public async Task<IActionResult> GetAccountActivities(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetAccountActivitiesAsync(id, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/admin/accounts/owners
    /// </summary>
    [HttpPost("owners")]
    public async Task<IActionResult> CreateOwner([FromBody] AdminCreateOwnerRequest dto)
    {
        var adminId = GetRequiredAdminId();
        var userId = await _service.CreateOwnerAsync(dto, adminId);
        return CreatedAtAction(nameof(GetAccountById), new { id = userId },
            new IdResponseDto { Id = userId });
    }

    /// <summary>
    /// POST /api/admin/accounts/staff
    /// </summary>
    [HttpPost("staff")]
    public async Task<IActionResult> CreateStaff([FromBody] AdminCreateStaffRequest dto)
    {
        var adminId = GetRequiredAdminId();
        var userId = await _service.CreateStaffAsync(dto, adminId);
        return CreatedAtAction(nameof(GetAccountById), new { id = userId },
            new IdResponseDto { Id = userId });
    }

    /// <summary>
    /// PUT /api/admin/accounts/{id}
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] AdminUpdateAccountRequest dto)
    {
        var adminId = GetRequiredAdminId();
        await _service.UpdateAccountAsync(id, dto, adminId);
        return Ok(new { message = "Cap nhat thanh cong." });
    }

    /// <summary>
    /// PUT /api/admin/accounts/{id}/roles
    /// </summary>
    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AdminAssignRolesRequest dto)
    {
        var adminId = GetRequiredAdminId();
        await _service.AssignRolesAsync(id, dto, adminId);
        return Ok(new { message = "Cap nhat vai tro thanh cong." });
    }

    /// <summary>
    /// PUT /api/admin/accounts/{id}/permissions
    /// </summary>
    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> AssignPermissions(Guid id, [FromBody] AdminAssignPermissionsRequest dto)
    {
        var adminId = GetRequiredAdminId();
        await _service.AssignPermissionsAsync(id, dto, adminId);
        return Ok(new { message = "Cap nhat quyen thanh cong." });
    }

    /// <summary>
    /// PUT /api/admin/accounts/{id}/branch
    /// </summary>
    [HttpPut("{id:guid}/branch")]
    public async Task<IActionResult> AssignBranch(Guid id, [FromBody] AdminAssignBranchRequest dto)
    {
        var adminId = GetRequiredAdminId();
        await _service.AssignBranchAsync(id, dto, adminId);
        return Ok(new { message = $"Da gan chi nhanh thanh cong." });
    }

    /// <summary>
    /// PATCH /api/admin/accounts/{id}/status
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] AdminToggleStatusRequest dto)
    {
        var adminId = GetRequiredAdminId();
        await _service.UpdateStatusAsync(id, dto, adminId);
        return Ok(new { message = dto.IsActive ? "Tai khoan da duoc kich hoat." : "Tai khoan da bi tam khoa." });
    }

    /// <summary>
    /// POST /api/admin/accounts/{id}/reset-password
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] AdminResetPasswordRequest dto)
    {
        var adminId = GetRequiredAdminId();
        await _service.ResetPasswordAsync(id, dto, adminId);
        return Ok(new { message = "Mat khau da duoc dat lai." });
    }

    /// <summary>
    /// GET /api/admin/accounts/{id}/check-relations
    /// </summary>
    [HttpGet("{id:guid}/check-relations")]
    public async Task<IActionResult> CheckRelations(Guid id)
    {
        var result = await _service.CheckRelationsAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// DELETE /api/admin/accounts/{id}
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> SoftDeleteAccount(Guid id)
    {
        var adminId = GetRequiredAdminId();
        var user = await _service.GetAccountByIdAsync(id);
        await _service.SoftDeleteAccountAsync(id, adminId);
        return Ok(new { message = $"Da xoa mem tai khoan '{user.FullName}'." });
    }

    /// <summary>
    /// POST /api/admin/accounts/{id}/restore
    /// </summary>
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreAccount(Guid id)
    {
        var adminId = GetRequiredAdminId();
        var user = await _service.GetAccountByIdAsync(id);
        await _service.RestoreAccountAsync(id, adminId);
        return Ok(new { message = $"Da khoi phuc tai khoan '{user.FullName}'." });
    }

    /// <summary>
    /// DELETE /api/admin/accounts/{id}/permanent
    /// </summary>
    [HttpDelete("{id:guid}/permanent")]
    public async Task<IActionResult> PermanentDeleteAccount(Guid id)
    {
        var adminId = GetRequiredAdminId();
        var user = await _service.GetAccountByIdAsync(id);
        await _service.PermanentDeleteAccountAsync(id, adminId);
        return Ok(new { message = $"Da xoa vinh vien tai khoan '{user.FullName}'. Du lieu ca nhan da duoc an danh." });
    }

    /// <summary>
    /// GET /api/admin/accounts/deleted
    /// </summary>
    [HttpGet("deleted")]
    public async Task<IActionResult> GetDeletedAccounts()
    {
        var result = await _service.GetDeletedAccountsAsync();
        return Ok(result);
    }
}
