using System;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.Services;

namespace MEP_Management_System.Controllers.OwnerController;

[ApiController]
[Route("api/owner/order-discount-tiers")]
[Tags("Quản lý chiết khấu đơn hàng (Owner)")]
[Authorize(Roles = PermissionConstants.OWNER_ROLE)]
public class OwnerOrderDiscountTiersController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly ICurrentBranchAccessor _currentBranch;

    public OwnerOrderDiscountTiersController(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch)
    {
        _context = context;
        _currentBranch = currentBranch;
    }

    /// <summary>
    /// Tạo mới một bậc chiết khấu (chỉ Owner).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<OrderDiscountTierDto>> Create([FromBody] CreateOrderDiscountTierDto request)
    {
        var resolvedBranchId = request.BranchId ?? _currentBranch.GetBranchId();

        // Validate MinOrderValue > 0
        if (request.MinOrderValue <= 0)
            return BadRequest(ApiErrorResponse.Create("MSG-02", "Ngưỡng giá trị phải lớn hơn 0"));

        // Validate DiscountPercent trong khoảng 0.01 - 100
        if (request.DiscountPercent < 0.01m || request.DiscountPercent > 100)
            return BadRequest(ApiErrorResponse.Create("MSG-02", "Phần trăm chiết khấu phải trong khoảng 0.01 - 100"));

        // Kiểm tra trùng MinOrderValue (theo branch nếu có)
        var existingTier = await _context.OrderDiscountTiers
            .AnyAsync(t => t.MinOrderValue == request.MinOrderValue
                && t.BranchId == resolvedBranchId);

        if (existingTier)
            return Conflict(ApiErrorResponse.Create("MSG-03", $"Ngưỡng giá trị {request.MinOrderValue:N0} đã tồn tại"));

        var tier = new OrderDiscountTier
        {
            BranchId = resolvedBranchId,
            MinOrderValue = request.MinOrderValue,
            DiscountPercent = request.DiscountPercent,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
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
            // Race: another request inserted the same (BranchId, MinOrderValue) between
            // the AnyAsync check above and SaveChangesAsync. DB unique index caught it.
            return Conflict(ApiErrorResponse.Create("MSG-03", $"Ngưỡng giá trị {request.MinOrderValue:N0} đã tồn tại"));
        }

        // Load branch for response
        await _context.Entry(tier).Reference(t => t.Branch).LoadAsync();

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

        // Trả về URI dùng endpoint GET của SaleController
        return CreatedAtAction("GetById", "OrderDiscountTiers", new { id = tier.OrderDiscountTierId }, result);
    }

    /// <summary>
    /// Cập nhật một bậc chiết khấu (chỉ Owner).
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrderDiscountTierDto>> Update(Guid id, [FromBody] UpdateOrderDiscountTierDto request)
    {
        var resolvedBranchId = request.BranchId ?? _currentBranch.GetBranchId();

        var tier = await _context.OrderDiscountTiers
            .Include(t => t.Branch)
            .FirstOrDefaultAsync(t => t.OrderDiscountTierId == id);

        if (tier == null)
            return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy bậc chiết khấu"));

        // Validate MinOrderValue > 0
        if (request.MinOrderValue <= 0)
            return BadRequest(ApiErrorResponse.Create("MSG-02", "Ngưỡng giá trị phải lớn hơn 0"));

        // Validate DiscountPercent trong khoảng 0.01 - 100
        if (request.DiscountPercent < 0.01m || request.DiscountPercent > 100)
            return BadRequest(ApiErrorResponse.Create("MSG-02", "Phần trăm chiết khấu phải trong khoảng 0.01 - 100"));

        // Kiểm tra trùng MinOrderValue (trừ chính bản thân, theo branch)
        var duplicateTier = await _context.OrderDiscountTiers
            .AnyAsync(t => t.MinOrderValue == request.MinOrderValue
                && t.BranchId == resolvedBranchId
                && t.OrderDiscountTierId != id);

        if (duplicateTier)
            return Conflict(ApiErrorResponse.Create("MSG-03", $"Ngưỡng giá trị {request.MinOrderValue:N0} đã tồn tại"));

        tier.BranchId = resolvedBranchId;
        tier.MinOrderValue = request.MinOrderValue;
        tier.DiscountPercent = request.DiscountPercent;
        tier.IsActive = request.IsActive;
        tier.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("23505") == true
            || ex.InnerException?.Message?.Contains("duplicate key") == true
            || ex.InnerException?.Message?.Contains("unique constraint") == true)
        {
            return Conflict(ApiErrorResponse.Create("MSG-03", $"Ngưỡng giá trị {request.MinOrderValue:N0} đã tồn tại"));
        }

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

    /// <summary>
    /// Xóa một bậc chiết khấu (chỉ Owner).
    /// </summary>
    [HttpDelete("{id:guid}")]
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
