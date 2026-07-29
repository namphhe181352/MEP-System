using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/sales/invoice-templates")]
public class InvoicePrintTemplateController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly InvoicePrintTemplateService _service;

    public InvoicePrintTemplateController(MEPDatabaseContext context, InvoicePrintTemplateService service)
    {
        _context = context;
        _service = service;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found"));

    // GET /api/sales/invoice-templates
    [HttpGet]
    public async Task<ActionResult> GetTemplate()
    {
        var branchId = GetBranchId();
        var template = await _service.ResolveTemplateAsync(branchId);
        if (template == null) return NotFound(new { message = "Template not found" });
        return Ok(template);
    }

    // PUT /api/sales/invoice-templates
    [HttpPut]
    public async Task<ActionResult> UpsertTemplate([FromBody] UpsertInvoicePrintTemplateRequest req)
    {
        var branchId = GetBranchId();
        var existing = await _context.InvoicePrintTemplates
            .FirstOrDefaultAsync(t => t.BranchId == branchId);

        if (existing != null)
        {
            existing.HeaderText = req.HeaderText;
            existing.FooterText = req.FooterText;
            existing.ShowLogo = req.ShowLogo;
            existing.LogoUrl = req.LogoUrl;
            existing.PaperSize = req.PaperSize ?? "K80";
            existing.FontSize = req.FontSize > 0 ? req.FontSize : 12;
            existing.FontFamily = req.FontFamily ?? "monospace";
            existing.ShowCustomerInfo = req.ShowCustomerInfo;
            existing.ShowCashierName = req.ShowCashierName;
            existing.ShowBranchInfo = req.ShowBranchInfo;
            existing.ShowPaymentMethod = req.ShowPaymentMethod;
            existing.ShowTaxInfo = req.ShowTaxInfo;
            existing.TaxRate = req.TaxRate > 0 ? req.TaxRate : 8.00m;
            existing.ThankYouMessage = req.ThankYouMessage;
            existing.CustomFieldsJson = req.CustomFieldsJson ?? "{}";
            existing.IsActive = req.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.InvoicePrintTemplates.Add(new InvoicePrintTemplate
            {
                TemplateId = Guid.NewGuid(),
                BranchId = branchId,
                HeaderText = req.HeaderText,
                FooterText = req.FooterText,
                ShowLogo = req.ShowLogo,
                LogoUrl = req.LogoUrl,
                PaperSize = req.PaperSize ?? "K80",
                FontSize = req.FontSize > 0 ? req.FontSize : 12,
                FontFamily = req.FontFamily ?? "monospace",
                ShowCustomerInfo = req.ShowCustomerInfo,
                ShowCashierName = req.ShowCashierName,
                ShowBranchInfo = req.ShowBranchInfo,
                ShowPaymentMethod = req.ShowPaymentMethod,
                ShowTaxInfo = req.ShowTaxInfo,
                TaxRate = req.TaxRate > 0 ? req.TaxRate : 8.00m,
                ThankYouMessage = req.ThankYouMessage,
                CustomFieldsJson = req.CustomFieldsJson ?? "{}",
                IsActive = req.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Template saved" });
    }

    // DELETE /api/sales/invoice-templates/{id}
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteTemplate(Guid id)
    {
        var template = await _context.InvoicePrintTemplates.FindAsync(id);
        if (template == null) return NotFound(new { message = "Template not found" });
        _context.InvoicePrintTemplates.Remove(template);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Template deleted" });
    }

    // POST /api/sales/invoice-templates/preview?invoiceId=...
    [HttpPost("preview")]
    public async Task<ActionResult> Preview([FromQuery] Guid invoiceId)
    {
        var branchId = GetBranchId();
        var invoice = await _context.Invoices
            .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp.Product)
            .Include(i => i.Order).ThenInclude(o => o.Customer)
            .Include(i => i.Order).ThenInclude(o => o.User)
            .Include(i => i.Order).ThenInclude(o => o.Branch)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);

        if (invoice == null) return NotFound(new { message = "Invoice not found" });

        var template = await _service.ResolveTemplateAsync(branchId);
        var sb = new StringBuilder();

        sb.AppendLine($"<!DOCTYPE html><html><head><meta charset='utf-8'/><title>Hoa don {invoice.InvoiceCode}</title>");
        sb.AppendLine("<style>body{font-family:monospace;max-width:300px;margin:0 auto;padding:10px}td{vertical-align:top;padding:2px 0}.r{text-align:right}.b{border-top:1px dashed #000;margin-top:4px;padding-top:4px}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h3 style='text-align:center'>RECEIPT</h3>");
        sb.AppendLine($"<p style='text-align:center'>{invoice.InvoiceCode}<br/>{invoice.CreatedAt:dd/MM/yyyy HH:mm}</p>");

        if (template != null)
        {
            if (template.ShowBranchInfo && invoice.Order?.Branch != null)
                sb.AppendLine($"<p>{invoice.Order.Branch.BranchName}<br/>{invoice.Order.Branch.Address}</p>");
            if (template.ShowCashierName && invoice.Order?.User != null)
                sb.AppendLine($"<p>Cashier: {invoice.Order.User.FullName}</p>");
            if (template.ShowCustomerInfo)
                sb.AppendLine($"<p>Customer: {invoice.Order?.Customer?.CustomerName ?? "Guest"}</p>");
        }
        else
        {
            sb.AppendLine($"<p>{invoice.Order?.Branch?.BranchName ?? ""}<br/>{invoice.Order?.Branch?.Address ?? ""}</p>");
            sb.AppendLine($"<p>Cashier: {invoice.Order?.User?.FullName ?? ""}</p>");
            sb.AppendLine($"<p>Customer: {invoice.Order?.Customer?.CustomerName ?? "Guest"}</p>");
        }

        sb.AppendLine("<table style='width:100%'>");
        foreach (var item in invoice.InvoiceItems)
        {
            var name = item.BranchProduct?.Product?.ProductName ?? "";
            sb.AppendLine($"<tr><td>{item.Quantity} x {item.SellPrice:N0}</td><td class='r'>{(item.Quantity * item.SellPrice):N0}</td></tr>");
            sb.AppendLine($"<tr><td colspan='2' style='padding-left:10px;font-size:12px'>{name}</td></tr>");
        }
        sb.AppendLine("</table>");

        var subtotal = invoice.InvoiceItems.Sum(i => i.Quantity * i.SellPrice);
        var discount = invoice.Order?.DiscountAmount ?? 0;
        var taxRate = template?.TaxRate ?? 8.00m;
        var vat = Math.Round((subtotal - discount) * taxRate / 100m, 2);
        var total = subtotal - discount + vat;

        sb.AppendLine($"<div class='b'><div><span>Subtotal</span><span class='r'>{subtotal:N0}</span></div>");
        if (discount > 0) sb.AppendLine($"<div><span>Discount</span><span class='r' style='color:red'>-{discount:N0}</span></div>");
        if (template == null || template.ShowTaxInfo)
            sb.AppendLine($"<div><span>VAT ({taxRate}%)</span><span class='r'>{vat:N0}</span></div>");
        sb.AppendLine($"<div style='font-weight:bold;font-size:16px;border-top:1px solid #000;margin-top:4px;padding-top:4px'><span>TOTAL</span><span class='r'>{total:N0}</span></div>");
        sb.AppendLine("</div>");

        var thankYou = template?.ThankYouMessage ?? "Thank you!";
        sb.AppendLine($"<p style='text-align:center;font-size:12px;margin-top:10px'>{thankYou}</p>");
        sb.AppendLine("</body></html>");

        return Content(sb.ToString(), "text/html");
    }
}

public class UpsertInvoicePrintTemplateRequest
{
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public bool ShowLogo { get; set; } = true;
    public string? LogoUrl { get; set; }
    public string? PaperSize { get; set; }
    public int FontSize { get; set; } = 12;
    public string? FontFamily { get; set; }
    public bool ShowCustomerInfo { get; set; } = true;
    public bool ShowCashierName { get; set; } = true;
    public bool ShowBranchInfo { get; set; } = true;
    public bool ShowPaymentMethod { get; set; } = true;
    public bool ShowTaxInfo { get; set; } = true;
    public decimal TaxRate { get; set; } = 8.00m;
    public string? ThankYouMessage { get; set; }
    public string? CustomFieldsJson { get; set; }
    public bool IsActive { get; set; } = true;
}
