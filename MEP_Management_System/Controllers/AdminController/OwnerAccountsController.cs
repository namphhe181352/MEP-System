using MEP_Management_System.DTOs;
using MEP_Management_System.Services.OwnerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/owners")]
[Tags("Admin (Admin)")]
public class OwnerAccountsController : ControllerBase
{
    private readonly IOwnerAccountService _ownerAccountService;

    public OwnerAccountsController(IOwnerAccountService ownerAccountService)
    {
        _ownerAccountService = ownerAccountService;
    }

    /// <summary>
    /// GET /api/admin/owners?search=&status=&page=&pageSize=
    /// Returns paginated list of Owner accounts
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetOwners(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _ownerAccountService.GetOwnersAsync(search, status, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/staff/{id}
    /// Returns full details of a staff account
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStaffById(Guid id)
    {
        var result = await _ownerAccountService.GetStaffByIdAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/admin/staff
    /// Creates a new Staff account.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateOwner([FromBody] CreateOwnerRequest dto)
    {
        var id = await _ownerAccountService.CreateOwnerAsync(dto);
        return CreatedAtAction(nameof(GetStaffById), new { id }, new IdResponseDto { Id = id });
    }

    /// <summary>
    /// PUT /api/admin/staff/{id}
    /// Updates staff account info
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateStaff(Guid id, [FromBody] UpdateOwnerRequest dto)
    {
        await _ownerAccountService.UpdateStaffAsync(id, dto);
        return Ok(new { message = "Cập nhật thông tin nhân viên thành công." });
    }

    /// <summary>
    /// PUT /api/admin/staff/{id}/status
    /// Sets staff IsActive flag (active = 1, suspended = 0)
    /// </summary>
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOwnerStatusRequest dto)
    {
        await _ownerAccountService.UpdateStatusAsync(id, dto.Status);
        return Ok(new { message = $"Trạng thái tài khoản được đặt thành '{dto.Status}'." });
    }

    /// <summary>
    /// PUT /api/admin/staff/{id}/ban
    /// Permanently bans a staff account
    /// </summary>
    [HttpPut("{id:guid}/ban")]
    public async Task<IActionResult> BanStaff(Guid id, [FromBody] BanOwnerRequest dto)
    {
        var adminId = GetAdminUserId();
        await _ownerAccountService.BanStaffAsync(id, dto.Reason, adminId);
        return Ok(new { message = "Tài khoản đã bị banned vĩnh viễn." });
    }

    private Guid? GetAdminUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var id) ? id : null;
    }
}
