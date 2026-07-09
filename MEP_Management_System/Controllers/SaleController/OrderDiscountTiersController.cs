using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/order-discount-tiers")]
[Tags("Quản lý chiết khấu đơn hàng")]
public class OrderDiscountTiersController : ControllerBase
{
    private readonly MEPDatabaseContext _context;

    public OrderDiscountTiersController(MEPDatabaseContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy danh sách tất cả các bậc chiết khấu (sort theo MinOrderValue tăng dần).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDiscountTierDto>>> GetAll()
    {
        var tiers = await _context.OrderDiscountTiers
            .OrderBy(t => t.MinOrderValue)
            .ToListAsync();

        var result = tiers.Select(t => new OrderDiscountTierDto
        {
            OrderDiscountTierId = t.OrderDiscountTierId,
            MinOrderValue = t.MinOrderValue,
            DiscountPercent = t.DiscountPercent,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        });

        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin một bậc chiết khấu theo ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDiscountTierDto>> GetById(Guid id)
    {
        var tier = await _context.OrderDiscountTiers.FindAsync(id);

        if (tier == null)
            return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy bậc chiết khấu"));

        var result = new OrderDiscountTierDto
        {
            OrderDiscountTierId = tier.OrderDiscountTierId,
            MinOrderValue = tier.MinOrderValue,
            DiscountPercent = tier.DiscountPercent,
            IsActive = tier.IsActive,
            CreatedAt = tier.CreatedAt,
            UpdatedAt = tier.UpdatedAt
        };

        return Ok(result);
    }

    /// <summary>
    /// Tạo mới một bậc chiết khấu (chỉ Owner).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<OrderDiscountTierDto>> Create([FromBody] CreateOrderDiscountTierDto request)
    {
        // Validate MinOrderValue > 0
        if (request.MinOrderValue <= 0)
            return BadRequest(ApiErrorResponse.Create("MSG-02", "Ngưỡng giá trị phải lớn hơn 0"));

        // Validate DiscountPercent trong khoảng 0.01 - 100
        if (request.DiscountPercent < 0.01m || request.DiscountPercent > 100)
            return BadRequest(ApiErrorResponse.Create("MSG-02", "Phần trăm chiết khấu phải trong khoảng 0.01 - 100"));

        // Kiểm tra trùng MinOrderValue
        var existingTier = await _context.OrderDiscountTiers
            .FirstOrDefaultAsync(t => t.MinOrderValue == request.MinOrderValue);

        if (existingTier != null)
            return BadRequest(ApiErrorResponse.Create("MSG-03", $"Ngưỡng giá trị {request.MinOrderValue:N0} đã tồn tại"));

        var tier = new OrderDiscountTier
        {
            MinOrderValue = request.MinOrderValue,
            DiscountPercent = request.DiscountPercent,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.OrderDiscountTiers.Add(tier);
        await _context.SaveChangesAsync();

        var result = new OrderDiscountTierDto
        {
            OrderDiscountTierId = tier.OrderDiscountTierId,
            MinOrderValue = tier.MinOrderValue,
            DiscountPercent = tier.DiscountPercent,
            IsActive = tier.IsActive,
            CreatedAt = tier.CreatedAt,
            UpdatedAt = tier.UpdatedAt
        };

        return CreatedAtAction(nameof(GetById), new { id = tier.OrderDiscountTierId }, result);
    }

    /// <summary>
    /// Cập nhật một bậc chiết khấu (chỉ Owner).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<OrderDiscountTierDto>> Update(Guid id, [FromBody] UpdateOrderDiscountTierDto request)
    {
        var tier = await _context.OrderDiscountTiers.FindAsync(id);

        if (tier == null)
            return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy bậc chiết khấu"));

        // Validate MinOrderValue > 0
        if (request.MinOrderValue <= 0)
            return BadRequest(ApiErrorResponse.Create("MSG-02", "Ngưỡng giá trị phải lớn hơn 0"));

        // Validate DiscountPercent trong khoảng 0.01 - 100
        if (request.DiscountPercent < 0.01m || request.DiscountPercent > 100)
            return BadRequest(ApiErrorResponse.Create("MSG-02", "Phần trăm chiết khấu phải trong khoảng 0.01 - 100"));

        // Kiểm tra trùng MinOrderValue (trừ chính bản thân)
        var duplicateTier = await _context.OrderDiscountTiers
            .FirstOrDefaultAsync(t => t.MinOrderValue == request.MinOrderValue && t.OrderDiscountTierId != id);

        if (duplicateTier != null)
            return BadRequest(ApiErrorResponse.Create("MSG-03", $"Ngưỡng giá trị {request.MinOrderValue:N0} đã tồn tại"));

        tier.MinOrderValue = request.MinOrderValue;
        tier.DiscountPercent = request.DiscountPercent;
        tier.IsActive = request.IsActive;
        tier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var result = new OrderDiscountTierDto
        {
            OrderDiscountTierId = tier.OrderDiscountTierId,
            MinOrderValue = tier.MinOrderValue,
            DiscountPercent = tier.DiscountPercent,
            IsActive = tier.IsActive,
            CreatedAt = tier.CreatedAt,
            UpdatedAt = tier.UpdatedAt
        };

        return Ok(result);
    }

    /// <summary>
    /// Xóa một bậc chiết khấu (chỉ Owner).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tier = await _context.OrderDiscountTiers.FindAsync(id);

        if (tier == null)
            return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy bậc chiết khấu"));

        _context.OrderDiscountTiers.Remove(tier);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
