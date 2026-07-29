using System;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/invoices/{invoiceId:guid}")]
[HasPermission(PermissionConstants.PRINT_VIEW)]
[Tags("Quản lý bán hàng (Sale)")]
public class PrintingController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;
    private readonly IMapper _mapper;
    private readonly InvoicePrintTemplateService _templateService;

    public PrintingController(Models.MEPDatabaseContext context, IMapper mapper, InvoicePrintTemplateService templateService)
    {
        _context = context;
        _mapper = mapper;
        _templateService = templateService;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found"));

    // POST /api/pos/invoices/{invoiceId}/print
    [HttpPost("print")]
    public async Task<ActionResult<ReceiptDto>> PrintInvoice(Guid invoiceId)
    {
        var branchId = GetBranchId();
        var invoice = await _context.Invoices
            .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp.Product)
            .Include(i => i.Order).ThenInclude(o => o.Customer)
            .Include(i => i.Order).ThenInclude(o => o.User)
            .Include(i => i.Order).ThenInclude(o => o.Branch)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);

        if (invoice == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Invoice not found"));

        var todayReceipts = await _context.Receipts.CountAsync(r => r.CreatedAt.Date == DateTime.UtcNow.Date);
        var receiptCode = CodeGenerator.GenerateReceiptCode(todayReceipts + 1);

        var template = await _templateService.ResolveTemplateAsync(branchId);
        var html = BuildReceiptHtml(invoice, receiptCode, template);

        var receipt = new Receipt
        {
            ReceiptId = Guid.NewGuid(),
            ReceiptCode = receiptCode,
            InvoiceId = invoiceId,
            HtmlContent = html,
            PrintedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _context.Receipts.Add(receipt);
        await _context.SaveChangesAsync();

        return Ok(_mapper.Map<ReceiptDto>(receipt));
    }

    // GET /api/pos/invoices/{invoiceId}/print.pdf
    [HttpGet("print.pdf")]
    public async Task<ActionResult> DownloadPdf(Guid invoiceId)
    {
        var branchId = GetBranchId();
        var receipt = await _context.Receipts
            .Include(r => r.Invoice).ThenInclude(i => i.Order).ThenInclude(o => o.Branch)
            .Where(r => r.InvoiceId == invoiceId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        if (receipt == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Receipt not found"));
        if (receipt.Invoice.Order.BranchId != branchId) return Forbid();

        var bytes = Encoding.UTF8.GetBytes(receipt.HtmlContent ?? "");
        return File(bytes, "text/html", $"{receipt.ReceiptCode}.html");
    }

    private static string BuildReceiptHtml(Invoice invoice, string receiptCode, InvoicePrintTemplate? template)
    {
        var sb = new StringBuilder();
        var paperSize = template?.PaperSize ?? "K80";
        var fontFamily = template?.FontFamily ?? "monospace";
        var fontSize = template?.FontSize ?? 12;
        sb.AppendLine($"<!DOCTYPE html><html><head><meta charset='utf-8'/><title>Receipt {receiptCode}</title>");
        sb.AppendLine($"<style>body{{font-family:{fontFamily};max-width:300px;margin:0 auto;padding:10px;font-size:{fontSize}px}}td{{vertical-align:top;padding:2px 0}}.r{{text-align:right}}.b{{border-top:1px dashed #000;margin-top:4px;padding-top:4px}}</style>");
        sb.AppendLine("</head><body>");
        if (!string.IsNullOrEmpty(template?.HeaderText))
            sb.AppendLine($"<p style='text-align:center'>{template.HeaderText}</p>");
        sb.AppendLine($"<h3 style='text-align:center'>RECEIPT</h3>");
        sb.AppendLine($"<p style='text-align:center'>{invoice.InvoiceCode}<br/>{invoice.CreatedAt:dd/MM/yyyy HH:mm}</p>");

        if (template == null || template.ShowBranchInfo)
        {
            var displayName = !string.IsNullOrEmpty(template?.BranchName) ? template.BranchName : invoice.Order?.Branch?.BranchName ?? "";
            var displayAddress = !string.IsNullOrEmpty(template?.BranchAddress) ? template.BranchAddress : invoice.Order?.Branch?.Address ?? "";
            var displayPhone = !string.IsNullOrEmpty(template?.Phone) ? template.Phone : invoice.Order?.Branch?.Phone;
            var displayTax = !string.IsNullOrEmpty(template?.TaxCode) ? template.TaxCode : null;
            sb.AppendLine($"<p>{displayName}<br/>{displayAddress}</p>");
            var extraLine = new List<string>();
            if (!string.IsNullOrEmpty(displayPhone)) extraLine.Add($"ĐT: {displayPhone}");
            if (!string.IsNullOrEmpty(displayTax)) extraLine.Add($"MST: {displayTax}");
            if (extraLine.Count > 0) sb.AppendLine($"<p>{string.Join(" • ", extraLine)}</p>");
        }
        if (template == null || template.ShowCashierName)
            sb.AppendLine($"<p>Cashier: {invoice.Order?.User?.FullName ?? ""}</p>");
        if (template == null || template.ShowCustomerInfo)
            sb.AppendLine($"<p>Customer: {invoice.Order?.Customer?.CustomerName ?? "Guest"}</p>");
        if (template != null && template.ShowPaymentMethod && !string.IsNullOrEmpty(invoice.PaymentMethod))
            sb.AppendLine($"<p>Payment: {invoice.PaymentMethod}</p>");

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
        var taxRate = 8.00m;
        var vat = Math.Round((subtotal - discount) * taxRate / 100m, 2);
        var total = subtotal - discount + vat;
        sb.AppendLine($"<div class='b'><div><span>Subtotal</span><span class='r'>{subtotal:N0}</span></div>");
        if (discount > 0) sb.AppendLine($"<div><span>Discount</span><span class='r' style='color:red'>-{discount:N0}</span></div>");
        sb.AppendLine($"<div><span>VAT ({taxRate}%)</span><span class='r'>{vat:N0}</span></div>");
        sb.AppendLine($"<div style='font-weight:bold;font-size:16px;border-top:1px solid #000;margin-top:4px;padding-top:4px'><span>TOTAL</span><span class='r'>{total:N0}</span></div>");
        sb.AppendLine("</div>");
        var thankYou = template?.ThankYouMessage ?? "Thank you!";
        sb.AppendLine($"<p style='text-align:center;font-size:12px;margin-top:10px'>{thankYou}</p>");
        if (!string.IsNullOrEmpty(template?.FooterText))
            sb.AppendLine($"<p style='text-align:center;font-size:11px'>{template.FooterText}</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
