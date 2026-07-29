using System;
using System.Text;
using System.Threading.Tasks;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

public class StockPrintTemplateService
{
    private readonly MEPDatabaseContext _context;

    public StockPrintTemplateService(MEPDatabaseContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Resolve template: branch row first, fallback to global (BranchId = null).
    /// </summary>
    public async Task<StockPrintTemplate?> ResolveTemplateAsync(Guid branchId, string ticketType)
    {
        var template = await _context.StockPrintTemplates
            .FirstOrDefaultAsync(t =>
                t.BranchId == branchId &&
                t.TicketType == ticketType &&
                t.IsActive);

        if (template != null) return template;

        // Fallback to global
        return await _context.StockPrintTemplates
            .FirstOrDefaultAsync(t =>
                t.BranchId == null &&
                t.TicketType == ticketType &&
                t.IsActive);
    }

    /// <summary>
    /// Build HTML for a stock ticket using the resolved template.
    /// </summary>
    public async Task<string> BuildTicketHtmlAsync(StockTicket ticket, Branch branch, StockPrintTemplate tpl)
    {
        var items = await _context.StockTicketItems
            .Include(i => i.BranchProduct).ThenInclude(bp => bp.Product)
            .Where(i => i.StockTicketId == ticket.StockTicketId)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine($"<!DOCTYPE html><html><head><meta charset='utf-8'/>");
        sb.AppendLine($"<title>Phiếu kho {ticket.TicketCode}</title>");
        sb.AppendLine($"<style>");
        sb.AppendLine($"body{{font-family:{tpl.FontFamily};font-size:{tpl.FontSize}px;max-width:800px;margin:0 auto;padding:20px}}");
        sb.AppendLine($"table{{width:100%;border-collapse:collapse;margin-top:10px}}");
        sb.AppendLine($"th,td{{border:1px solid #000;padding:6px}}");
        sb.AppendLine($"th{{background:#f0f0f0}}");
        sb.AppendLine($".header{{text-align:center;margin-bottom:20px}}");
        sb.AppendLine($".footer{{margin-top:30px}}");
        sb.AppendLine($".signature{{margin-top:40px;display:flex;justify-content:space-around}}");
        sb.AppendLine($"</style></head><body>");

        // Header
        if (!string.IsNullOrEmpty(tpl.HeaderText))
            sb.AppendLine($"<div class='header'><h2>{tpl.HeaderText}</h2></div>");

        // Branch info
        if (tpl.ShowBranchInfo)
        {
            var displayName = !string.IsNullOrEmpty(tpl.BranchName) ? tpl.BranchName : branch.BranchName;
            var displayAddress = !string.IsNullOrEmpty(tpl.BranchAddress) ? tpl.BranchAddress : branch.Address;
            var displayPhone = !string.IsNullOrEmpty(tpl.Phone) ? tpl.Phone : branch.Phone;
            sb.AppendLine($"<p><strong>Chi nhánh:</strong> {displayName}</p>");
            sb.AppendLine($"<p><strong>Địa chỉ:</strong> {displayAddress}</p>");
            var extraLine = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(displayPhone)) extraLine.Add($"ĐT: {displayPhone}");
            if (!string.IsNullOrEmpty(tpl.TaxCode)) extraLine.Add($"MST: {tpl.TaxCode}");
            if (extraLine.Count > 0) sb.AppendLine($"<p>{string.Join(" • ", extraLine)}</p>");
        }

        // Ticket info
        sb.AppendLine($"<p><strong>Mã phiếu:</strong> {ticket.TicketCode}</p>");
        sb.AppendLine($"<p><strong>Loại:</strong> {ticket.TicketType}</p>");
        sb.AppendLine($"<p><strong>Ngày:</strong> {ticket.CreatedAt:dd/MM/yyyy HH:mm}</p>");

        // Supplier
        if (tpl.ShowSupplier && ticket.Supplier != null)
        {
            sb.AppendLine($"<p><strong>Nhà cung cấp:</strong> {ticket.Supplier.SupplierName}</p>");
        }

        // Items table
        sb.AppendLine("<table>");
        sb.AppendLine("<thead><tr><th>STT</th><th>Tên sản phẩm</th><th>Mã SP</th><th>SL hệ thống</th><th>SL thực tế</th><th>Chênh lệch</th></tr></thead>");
        sb.AppendLine("<tbody>");
        int stt = 1;
        foreach (var item in items)
        {
            var name = item.BranchProduct?.Product?.ProductName ?? "";
            var code = item.BranchProduct?.Product?.ProductCode ?? "";
            sb.AppendLine($"<tr><td>{stt++}</td><td>{name}</td><td>{code}</td><td>{item.SystemQuantity:N2}</td><td>{item.ActualQuantity:N2}</td><td>{item.Discrepancy:N2}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");

        // Total
        sb.AppendLine($"<p style='margin-top:10px'><strong>Tổng tiền:</strong> {ticket.TotalAmount:N0} VNĐ</p>");

        // Notes
        if (!string.IsNullOrEmpty(ticket.Notes))
            sb.AppendLine($"<p><strong>Ghi chú:</strong> {ticket.Notes}</p>");

        // Footer
        if (!string.IsNullOrEmpty(tpl.FooterText))
            sb.AppendLine($"<div class='footer'><p>{tpl.FooterText}</p></div>");

        // Signature
        if (tpl.ShowSignature)
        {
            sb.AppendLine("<div class='signature'>");
            sb.AppendLine("<div style='text-align:center'><p>Người lập phiếu</p><br/><br/></div>");
            sb.AppendLine("<div style='text-align:center'><p>Kế toán</p><br/><br/></div>");
            sb.AppendLine("<div style='text-align:center'><p>Thủ kho</p><br/><br/></div>");
            sb.AppendLine("</div>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}
