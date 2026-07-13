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

}
