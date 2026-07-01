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
[Authorize(Roles = "SalesStaff,Owner")]
[Tags("Quản lý bán hàng (Sale)")]
public class PrintingController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;
    private readonly IMapper _mapper;

    public PrintingController(Models.MEPDatabaseContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
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

        var html = BuildReceiptHtml(invoice, receiptCode);

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
            .FirstOrDefaultAsync(r => r.InvoiceId == invoiceId);

        if (receipt == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Receipt not found"));
        if (receipt.Invoice.Order.BranchId != branchId) return Forbid();

        var bytes = Encoding.UTF8.GetBytes(receipt.HtmlContent ?? "");
        return File(bytes, "text/html", $"{receipt.ReceiptCode}.html");
    }

    private static string BuildReceiptHtml(Invoice invoice, string receiptCode)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<!DOCTYPE html><html><head><meta charset='utf-8'/><title>Receipt {receiptCode}</title>");
        sb.AppendLine("<style>body{font-family:monospace;max-width:300px;margin:0 auto;padding:10px}td{vertical-align:top;padding:2px 0}.r{text-align:right}.b{border-top:1px dashed #000;margin-top:4px;padding-top:4px}</style>");
        sb.AppendLine("</head><body>");
        sb.AppendLine($"<h3 style='text-align:center'>RECEIPT</h3>");
        sb.AppendLine($"<p style='text-align:center'>{invoice.InvoiceCode}<br/>{invoice.CreatedAt:dd/MM/yyyy HH:mm}</p>");
        sb.AppendLine($"<p>{invoice.Order?.Branch?.BranchName ?? ""}<br/>{invoice.Order?.Branch?.Address ?? ""}</p>");
        sb.AppendLine($"<p>Cashier: {invoice.Order?.User?.FullName ?? ""}</p>");
        sb.AppendLine($"<p>Customer: {invoice.Order?.Customer?.CustomerName ?? "Guest"}</p>");
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
        var vat = Math.Round((subtotal - discount) * 0.08m, 2);
        var total = subtotal - discount + vat;
        sb.AppendLine($"<div class='b'><div><span>Subtotal</span><span class='r'>{subtotal:N0}</span></div>");
        if (discount > 0) sb.AppendLine($"<div><span>Discount</span><span class='r' style='color:red'>-{discount:N0}</span></div>");
        sb.AppendLine($"<div><span>VAT (8%)</span><span class='r'>{vat:N0}</span></div>");
        sb.AppendLine($"<div style='font-weight:bold;font-size:16px;border-top:1px solid #000;margin-top:4px;padding-top:4px'><span>TOTAL</span><span class='r'>{total:N0}</span></div>");
        sb.AppendLine("</div>");
        sb.AppendLine("<p style='text-align:center;font-size:12px;margin-top:10px'>Thank you!</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
