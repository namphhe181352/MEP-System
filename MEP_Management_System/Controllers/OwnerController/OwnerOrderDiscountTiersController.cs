using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Services;
using MEP_Management_System.Services.OwnerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.OwnerController;

[ApiController]
[Route("api/owner/order-discount-tiers")]
[Tags("Quản lý chiết khấu đơn hàng (Owner)")]
[Authorize(Roles = "Owner")]
public class OwnerOrderDiscountTiersController : ControllerBase
{
    private readonly IOwnerOrderDiscountTierService _ownerOrderDiscountTierService;
    private readonly ICurrentBranchAccessor _currentBranch;

    public OwnerOrderDiscountTiersController(
        IOwnerOrderDiscountTierService ownerOrderDiscountTierService,
        ICurrentBranchAccessor currentBranch)
    {
        _ownerOrderDiscountTierService = ownerOrderDiscountTierService;
        _currentBranch = currentBranch;
    }

    /// <summary>
    /// Tạo mới một bậc chiết khấu (chỉ Owner).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OrderDiscountTierDto>> Create([FromBody] CreateOrderDiscountTierDto request)
    {
        var resolvedBranchId = request.BranchId ?? _currentBranch.GetBranchId();
        var result = await _ownerOrderDiscountTierService.CreateAsync(resolvedBranchId, request);
        return CreatedAtAction("GetById", "OrderDiscountTiers", new { id = result.OrderDiscountTierId }, result);
    }

    /// <summary>
    /// Cập nhật một bậc chiết khấu (chỉ Owner).
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrderDiscountTierDto>> Update(Guid id, [FromBody] UpdateOrderDiscountTierDto request)
    {
        var resolvedBranchId = request.BranchId ?? _currentBranch.GetBranchId();
        var result = await _ownerOrderDiscountTierService.UpdateAsync(id, resolvedBranchId, request);
        return Ok(result);
    }

    /// <summary>
    /// Xóa một bậc chiết khấu (chỉ Owner).
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _ownerOrderDiscountTierService.DeleteAsync(id);
        return NoContent();
    }
}
