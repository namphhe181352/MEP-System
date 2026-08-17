using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MEP_Management_System.Services;
using MEP_Management_System.Services.SaleServices;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/order-discount-tiers")]
[Tags("Quản lý chiết khấu đơn hàng")]
public class OrderDiscountTiersController : ControllerBase
{
    private readonly IOrderDiscountTierService _tierService;
    private readonly ICurrentBranchAccessor _currentBranch;

    public OrderDiscountTiersController(IOrderDiscountTierService tierService, ICurrentBranchAccessor currentBranch)
    {
        _tierService = tierService;
        _currentBranch = currentBranch;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDiscountTierDto>>> GetAll([FromQuery] Guid? branchId)
    {
        var resolvedBranchId = branchId ?? _currentBranch.GetBranchId();
        var tiers = await _tierService.GetAllAsync(resolvedBranchId);
        return Ok(tiers);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDiscountTierDto>> GetById(Guid id)
    {
        var tier = await _tierService.GetByIdAsync(id);
        if (tier == null)
            return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy bậc chiết khấu"));
        return Ok(tier);
    }

    [HttpPost]
    public async Task<ActionResult<OrderDiscountTierDto>> CreateTier([FromBody] CreateOrderDiscountTierRequest req)
    {
        var resolvedBranchId = req.BranchId ?? _currentBranch.GetBranchId();
        var tier = await _tierService.CreateAsync(req, resolvedBranchId);
        return CreatedAtAction(nameof(GetById), new { id = tier.OrderDiscountTierId }, tier);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrderDiscountTierDto>> UpdateTier(Guid id, [FromBody] CreateOrderDiscountTierRequest req)
    {
        var resolvedBranchId = req.BranchId ?? _currentBranch.GetBranchId();
        var tier = await _tierService.UpdateAsync(id, req, resolvedBranchId);
        if (tier == null)
            return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy bậc chiết khấu"));
        return Ok(tier);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteTier(Guid id)
    {
        await _tierService.DeleteAsync(id);
        return Ok(new { message = "Đã xóa bậc chiết khấu" });
    }
}
