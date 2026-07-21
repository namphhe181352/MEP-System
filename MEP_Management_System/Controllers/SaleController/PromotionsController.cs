using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/invoices/{invoiceId:guid}/promo")]
[HasPermission(PermissionConstants.PROMOTION_VIEW)]
[Tags("Quản lý bán hàng (Sale)")]
public class PromotionsController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;
    private readonly IMapper _mapper;

    public PromotionsController(Models.MEPDatabaseContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found"));
    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? throw new Exception("userId"));

    // POST /api/pos/invoices/{invoiceId}/promo
    [HttpPost]
    public async Task<ActionResult<PromotionDto>> ApplyPromo(Guid invoiceId, [FromBody] ApplyPromoRequest request)
    {
        var branchId = GetBranchId();
        var userId = GetUserId();

        var invoice = await _context.Invoices
            .Include(i => i.Order)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);

        if (invoice == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Invoice not found"));
        if (invoice.Status != "Draft") return BadRequest(ApiErrorResponse.Create("MSG-78", "Cannot apply promo to finalized invoice"));

        var promo = await _context.Promotions
            .FirstOrDefaultAsync(p => p.PromotionCode == request.PromoCode.ToUpper() && p.IsActive);

        if (promo == null) return NotFound(ApiErrorResponse.Create("MSG-43", "Invalid promotion code"));
        if (promo.StartDate > DateTime.UtcNow || promo.EndDate < DateTime.UtcNow) return BadRequest(ApiErrorResponse.Create("MSG-43", "Promotion code has expired"));
        if (promo.UsageLimit.HasValue && promo.UsedCount >= promo.UsageLimit.Value) return BadRequest(ApiErrorResponse.Create("MSG-43", "Promotion usage limit reached"));

        var subtotal = invoice.InvoiceItems.Sum(ii => ii.Quantity * ii.SellPrice);
        if (promo.MinOrderAmount.HasValue && subtotal < promo.MinOrderAmount.Value) return BadRequest(ApiErrorResponse.Create("MSG-43", $"Minimum order amount: {promo.MinOrderAmount.Value:N0}"));

        decimal discountAmount;
        if (promo.DiscountType == "Percentage")
        {
            discountAmount = Math.Round(subtotal * promo.DiscountValue / 100m, 2);
            if (promo.MaxDiscountAmount.HasValue && discountAmount > promo.MaxDiscountAmount.Value)
                discountAmount = promo.MaxDiscountAmount.Value;
        }
        else
        {
            discountAmount = promo.DiscountValue;
        }

        invoice.Order.PromotionCode = promo.PromotionCode;
        invoice.Order.DiscountAmount = discountAmount;
        promo.UsedCount++;

        await _context.SaveChangesAsync();

        var dto = _mapper.Map<PromotionDto>(promo);
        dto.DiscountAmount = discountAmount;
        return Ok(dto);
    }

    // DELETE /api/pos/invoices/{invoiceId}/promo
    [HttpDelete]
    public async Task<ActionResult> RemovePromo(Guid invoiceId)
    {
        var branchId = GetBranchId();
        var invoice = await _context.Invoices
            .Include(i => i.Order).ThenInclude(o => o.Invoice)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);

        if (invoice == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Invoice not found"));
        if (invoice.Status != "Draft") return BadRequest(ApiErrorResponse.Create("MSG-78", "Cannot remove promo from finalized invoice"));
        if (string.IsNullOrEmpty(invoice.Order.PromotionCode)) return BadRequest(ApiErrorResponse.Create("MSG-43", "No promo applied"));

        invoice.Order.PromotionCode = null;
        invoice.Order.DiscountAmount = 0;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
