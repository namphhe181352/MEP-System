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
    public async Task<ActionResult<IEnumerable<OrderDiscountTierDto>>> GetAll([FromQuery] Guid? branchId)
    {
        try
        {
            var query = _context.OrderDiscountTiers.AsQueryable();

            if (branchId.HasValue)
            {
                query = query.Where(t => t.BranchId == branchId);
            }

            var tiers = await query
                .OrderBy(t => t.MinOrderValue)
                .ToListAsync();

            // Lấy branch names riêng để tránh lỗi Include
            var branchIds = tiers.Where(t => t.BranchId.HasValue).Select(t => t.BranchId!.Value).Distinct().ToList();
            var branches = await _context.Branches
                .Where(b => branchIds.Contains(b.BranchId))
                .GroupBy(b => b.BranchId)
                .Select(g => g.First())
                .ToDictionaryAsync(b => b.BranchId, b => b.BranchName);

            var result = tiers.Select(t => new OrderDiscountTierDto
            {
                OrderDiscountTierId = t.OrderDiscountTierId,
                BranchId = t.BranchId,
                BranchName = t.BranchId.HasValue && branches.ContainsKey(t.BranchId.Value) ? branches[t.BranchId.Value] : null,
                MinOrderValue = t.MinOrderValue,
                DiscountPercent = t.DiscountPercent,
                IsActive = t.IsActive,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy thông tin một bậc chiết khấu theo ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDiscountTierDto>> GetById(Guid id)
    {
        var tier = await _context.OrderDiscountTiers
            .Include(t => t.Branch)
            .FirstOrDefaultAsync(t => t.OrderDiscountTierId == id);

        if (tier == null)
            return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy bậc chiết khấu"));

        var result = new OrderDiscountTierDto
        {
            OrderDiscountTierId = tier.OrderDiscountTierId,
            BranchId = tier.BranchId,
            BranchName = tier.Branch != null ? tier.Branch.BranchName : null,
            MinOrderValue = tier.MinOrderValue,
            DiscountPercent = tier.DiscountPercent,
            IsActive = tier.IsActive,
            CreatedAt = tier.CreatedAt,
            UpdatedAt = tier.UpdatedAt
        };

        return Ok(result);
    }

    // POST /api/order-discount-tiers
    [HttpPost]
    public async Task<ActionResult<OrderDiscountTierDto>> CreateTier([FromBody] CreateOrderDiscountTierRequest req)
    {
        // Validate MinOrderValue >= 0
        if (req.MinOrderValue < 0)
            return BadRequest(ApiErrorResponse.Create("MSG-63", "MinOrderValue phải lớn hơn hoặc bằng 0"));

        // Validate DiscountPercent in [0, 100]
        if (req.DiscountPercent < 0 || req.DiscountPercent > 100)
            return BadRequest(ApiErrorResponse.Create("MSG-63", "DiscountPercent phải nằm trong khoảng 0 - 100"));

        // If BranchId has value, check Branch exists
        if (req.BranchId.HasValue)
        {
            var branchExists = await _context.Branches.AnyAsync(b => b.BranchId == req.BranchId.Value);
            if (!branchExists)
                return BadRequest(ApiErrorResponse.Create("MSG-01", "Không tìm thấy chi nhánh"));
        }

        // Unique (BranchId, MinOrderValue)
        var exists = await _context.OrderDiscountTiers
            .AnyAsync(t => t.BranchId == req.BranchId && t.MinOrderValue == req.MinOrderValue);
        if (exists)
            return Conflict(ApiErrorResponse.Create("MSG-68", "MinOrderValue đã tồn tại trong chi nhánh này"));

        var tier = new OrderDiscountTier
        {
            OrderDiscountTierId = Guid.NewGuid(),
            BranchId = req.BranchId,
            MinOrderValue = req.MinOrderValue,
            DiscountPercent = req.DiscountPercent,
            IsActive = req.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.OrderDiscountTiers.Add(tier);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("23505") == true
            || ex.InnerException?.Message?.Contains("duplicate key") == true
            || ex.InnerException?.Message?.Contains("unique constraint") == true)
        {
            return Conflict(ApiErrorResponse.Create("MSG-68", "MinOrderValue đã tồn tại trong chi nhánh này"));
        }

        var dto = new OrderDiscountTierDto
        {
            OrderDiscountTierId = tier.OrderDiscountTierId,
            BranchId = tier.BranchId,
            MinOrderValue = tier.MinOrderValue,
            DiscountPercent = tier.DiscountPercent,
            IsActive = tier.IsActive,
            CreatedAt = tier.CreatedAt,
            UpdatedAt = tier.UpdatedAt
        };

        return CreatedAtAction(nameof(GetById), new { id = tier.OrderDiscountTierId }, dto);
    }

    // PUT /api/order-discount-tiers/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrderDiscountTierDto>> UpdateTier(Guid id, [FromBody] CreateOrderDiscountTierRequest req)
    {
        var tier = await _context.OrderDiscountTiers.FindAsync(id);
        if (tier == null)
            return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy bậc chiết khấu"));

        if (req.MinOrderValue < 0)
            return BadRequest(ApiErrorResponse.Create("MSG-63", "MinOrderValue phải lớn hơn hoặc bằng 0"));

        if (req.DiscountPercent < 0 || req.DiscountPercent > 100)
            return BadRequest(ApiErrorResponse.Create("MSG-63", "DiscountPercent phải nằm trong khoảng 0 - 100"));

        if (req.BranchId.HasValue)
        {
            var branchExists = await _context.Branches.AnyAsync(b => b.BranchId == req.BranchId.Value);
            if (!branchExists)
                return BadRequest(ApiErrorResponse.Create("MSG-01", "Không tìm thấy chi nhánh"));
        }

        // Unique check (exclude current record)
        var duplicate = await _context.OrderDiscountTiers
            .AnyAsync(t => t.OrderDiscountTierId != id && t.BranchId == req.BranchId && t.MinOrderValue == req.MinOrderValue);
        if (duplicate)
            return Conflict(ApiErrorResponse.Create("MSG-68", "MinOrderValue đã tồn tại trong chi nhánh này"));

        tier.BranchId = req.BranchId;
        tier.MinOrderValue = req.MinOrderValue;
        tier.DiscountPercent = req.DiscountPercent;
        tier.IsActive = req.IsActive;
        tier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = new OrderDiscountTierDto
        {
            OrderDiscountTierId = tier.OrderDiscountTierId,
            BranchId = tier.BranchId,
            MinOrderValue = tier.MinOrderValue,
            DiscountPercent = tier.DiscountPercent,
            IsActive = tier.IsActive,
            CreatedAt = tier.CreatedAt,
            UpdatedAt = tier.UpdatedAt
        };

        return Ok(dto);
    }

    // DELETE /api/order-discount-tiers/{id}
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteTier(Guid id)
    {
        var tier = await _context.OrderDiscountTiers.FindAsync(id);
        if (tier == null)
            return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy bậc chiết khấu"));

        _context.OrderDiscountTiers.Remove(tier);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Đã xóa bậc chiết khấu" });
    }
}

public class CreateOrderDiscountTierRequest
{
    public Guid? BranchId { get; set; }
    public decimal MinOrderValue { get; set; }
    public decimal DiscountPercent { get; set; }
    public bool IsActive { get; set; } = true;
}
