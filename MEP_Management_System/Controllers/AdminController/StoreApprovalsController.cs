using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.AdminController;

/// <summary>
/// Thin controller duyệt cửa hàng mới (store approvals). Business logic nằm trong StoreApprovalService.
/// </summary>
[ApiController]
[Route("api/admin/store-approvals")]
[Authorize(Roles = "Admin")]
[Tags("Admin (Store Approvals)")]
public class StoreApprovalsController : ControllerBase
{
    private readonly IStoreApprovalService _service;

    public StoreApprovalsController(IStoreApprovalService service)
    {
        _service = service;
    }

    private Guid GetRequiredAdminId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var adminId))
            throw new UnauthorizedAccessException("Khong xac dinh duoc nguoi dung hien tai.");
        return adminId;
    }

    /// <summary>
    /// GET /api/admin/store-approvals
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetApprovals(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetApprovalsAsync(status, search, page, pageSize);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/store-approvals/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetApprovalById(Guid id)
    {
        var result = await _service.GetApprovalByIdAsync(id);
        return Ok(result);
    }

    /// <summary>
    /// PUT /api/admin/store-approvals/{id}/approve  body { notes }
    /// </summary>
    [HttpPut("{id:guid}/approve")]
    [ProducesResponseType(typeof(StoreApprovalDto), 200)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveStoreRequest dto)
    {
        var adminId = GetRequiredAdminId();
        var result = await _service.ApproveAsync(id, dto, adminId);
        return Ok(result);
    }

    /// <summary>
    /// PUT /api/admin/store-approvals/{id}/reject  body { reason }
    /// </summary>
    [HttpPut("{id:guid}/reject")]
    [ProducesResponseType(typeof(StoreApprovalDto), 200)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectStoreRequest dto)
    {
        var adminId = GetRequiredAdminId();
        var result = await _service.RejectAsync(id, dto, adminId);
        return Ok(result);
    }
}