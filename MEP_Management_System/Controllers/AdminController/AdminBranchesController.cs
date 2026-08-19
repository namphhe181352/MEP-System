using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Services.AdminBranchServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.AdminController;

/// <summary>
/// Thin controller for Admin branch management.
/// All business logic moved to AdminBranchService.
/// </summary>
[ApiController]
[Route("api/admin/branches")]
[Authorize(Roles = "Admin")]
[Tags("Admin (Branches)")]
public class AdminBranchesController : ControllerBase
{
    private readonly IAdminBranchService _service;

    public AdminBranchesController(IAdminBranchService service)
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
    /// GET /api/admin/branches
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBranches(
        [FromQuery] string? search,
        [FromQuery] short? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetBranchesAsync(search, isActive, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/branches/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBranchById(Guid id)
    {
        var result = await _service.GetBranchByIdAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/admin/branches
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateBranch([FromBody] CreateAdminBranchRequest dto)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var adminId = GetRequiredAdminId();
        var result = await _service.CreateBranchAsync(dto, adminId);
        return CreatedAtAction(nameof(GetBranchById), new { id = result.BranchId }, result);
    }

    /// <summary>
    /// PUT /api/admin/branches/{id}
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateBranch(Guid id, [FromBody] UpdateAdminBranchRequest dto)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var adminId = GetRequiredAdminId();
        var result = await _service.UpdateBranchAsync(id, dto, adminId);
        return Ok(result);
    }

    /// <summary>
    /// DELETE /api/admin/branches/{id}
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteBranch(Guid id, [FromQuery] bool force = false)
    {
        var adminId = GetRequiredAdminId();
        var (message, isActive) = await _service.DeleteBranchAsync(id, adminId, force);
        return Ok(new { message, isActive });
    }

    /// <summary>
    /// PATCH /api/admin/branches/{id}/restore
    /// </summary>
    [HttpPatch("{id:guid}/restore")]
    public async Task<IActionResult> RestoreBranch(Guid id)
    {
        var adminId = GetRequiredAdminId();
        var (message, isActive) = await _service.RestoreBranchAsync(id, adminId);
        return Ok(new { message, isActive });
    }

    /// <summary>
    /// DELETE /api/admin/branches/{id}/permanent
    /// </summary>
    [HttpDelete("{id:guid}/permanent")]
    public async Task<IActionResult> HardDeleteBranch(Guid id, [FromQuery] bool force = false)
    {
        var adminId = GetRequiredAdminId();
        var message = await _service.HardDeleteBranchAsync(id, adminId, force);
        return Ok(new { message });
    }

    /// <summary>
    /// GET /api/admin/branches/{id}/users
    /// </summary>
    [HttpGet("{id:guid}/users")]
    public async Task<IActionResult> GetBranchUsers(Guid id)
    {
        var result = await _service.GetBranchUsersAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// PUT /api/admin/branches/{id}/users
    /// </summary>
    [HttpPut("{id:guid}/users")]
    public async Task<IActionResult> AssignUserToBranch(Guid id, [FromBody] AssignUserToBranchRequest dto)
    {
        var adminId = GetRequiredAdminId();
        await _service.AssignUserToBranchAsync(id, dto, adminId);
        return Ok(new { message = "Gan nguoi dung thanh cong." });
    }

    /// <summary>
    /// DELETE /api/admin/branches/{id}/users/{userId}
    /// </summary>
    [HttpDelete("{id:guid}/users/{userId:guid}")]
    public async Task<IActionResult> RemoveUserFromBranch(Guid id, Guid userId)
    {
        var adminId = GetRequiredAdminId();
        await _service.RemoveUserFromBranchAsync(id, userId, adminId);
        return Ok(new { message = "Xoa nguoi dung thanh cong." });
    }
}
