using System;
using System.Text;
using System.Threading.Tasks;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using MEP_Management_System.Services.InventoryService;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

public class StockPrintTemplateService : IStockPrintTemplateService
{
    private readonly MEPDatabaseContext _context;

    public StockPrintTemplateService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<StockPrintTemplate?> ResolveTemplateAsync(Guid branchId, string ticketType)
    {
        var template = await _context.StockPrintTemplates
            .FirstOrDefaultAsync(t =>
                t.BranchId == branchId &&
                t.TicketType == ticketType &&
                t.IsActive);

        if (template != null) return template;

        return await _context.StockPrintTemplates
            .FirstOrDefaultAsync(t =>
                t.BranchId == null &&
                t.TicketType == ticketType &&
                t.IsActive);
    }

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

        if (!string.IsNullOrEmpty(tpl.HeaderText))
            sb.AppendLine($"<div class='header'><h2>{tpl.HeaderText}</h2></div>");

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

        sb.AppendLine($"<p><strong>Mã phiếu:</strong> {ticket.TicketCode}</p>");
        sb.AppendLine($"<p><strong>Loại:</strong> {ticket.TicketType}</p>");
        sb.AppendLine($"<p><strong>Ngày:</strong> {ticket.CreatedAt:dd/MM/yyyy HH:mm}</p>");

        if (tpl.ShowSupplier && ticket.Supplier != null)
        {
            sb.AppendLine($"<p><strong>Nhà cung cấp:</strong> {ticket.Supplier.SupplierName}</p>");
        }

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

        sb.AppendLine($"<p style='margin-top:10px'><strong>Tổng tiền:</strong> {ticket.TotalAmount:N0} VNĐ</p>");

        if (!string.IsNullOrEmpty(ticket.Notes))
            sb.AppendLine($"<p><strong>Ghi chú:</strong> {ticket.Notes}</p>");

        if (!string.IsNullOrEmpty(tpl.FooterText))
            sb.AppendLine($"<div class='footer'><p>{tpl.FooterText}</p></div>");

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

    public async Task UpsertTemplateAsync(Guid branchId, UpsertStockPrintTemplateRequest req)
    {
        var existing = await _context.StockPrintTemplates
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.TicketType == req.TicketType);

        if (existing != null)
        {
            existing.HeaderText = req.HeaderText;
            existing.FooterText = req.FooterText;
            existing.ShowLogo = req.ShowLogo;
            existing.LogoUrl = req.LogoUrl;
            existing.BranchName = req.BranchName;
            existing.BranchAddress = req.BranchAddress;
            existing.Phone = req.Phone;
            existing.TaxCode = req.TaxCode;
            existing.PaperSize = req.PaperSize ?? "A5";
            existing.FontSize = req.FontSize > 0 ? req.FontSize : 12;
            existing.FontFamily = req.FontFamily ?? "monospace";
            existing.ShowSupplier = req.ShowSupplier;
            existing.ShowBranchInfo = req.ShowBranchInfo;
            existing.ShowSignature = req.ShowSignature;
            existing.CustomFieldsJson = req.CustomFieldsJson ?? "{}";
            existing.IsActive = req.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.StockPrintTemplates.Add(new StockPrintTemplate
            {
                TemplateId = Guid.NewGuid(),
                BranchId = branchId,
                TicketType = req.TicketType,
                HeaderText = req.HeaderText,
                FooterText = req.FooterText,
                ShowLogo = req.ShowLogo,
                LogoUrl = req.LogoUrl,
                BranchName = req.BranchName,
                BranchAddress = req.BranchAddress,
                Phone = req.Phone,
                TaxCode = req.TaxCode,
                PaperSize = req.PaperSize ?? "A5",
                FontSize = req.FontSize > 0 ? req.FontSize : 12,
                FontFamily = req.FontFamily ?? "monospace",
                ShowSupplier = req.ShowSupplier,
                ShowBranchInfo = req.ShowBranchInfo,
                ShowSignature = req.ShowSignature,
                CustomFieldsJson = req.CustomFieldsJson ?? "{}",
                IsActive = req.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> DeleteTemplateAsync(Guid id)
    {
        var template = await _context.StockPrintTemplates.FindAsync(id);
        if (template == null)
            throw new KeyNotFoundException("Template not found");

        _context.StockPrintTemplates.Remove(template);
        await _context.SaveChangesAsync();
        return true;
    }
}
