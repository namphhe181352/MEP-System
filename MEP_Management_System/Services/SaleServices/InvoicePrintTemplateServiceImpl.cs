using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.SaleServices;

public class InvoicePrintTemplateServiceImpl : IInvoicePrintTemplateService
{
    private readonly MEPDatabaseContext _context;

    public InvoicePrintTemplateServiceImpl(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<InvoicePrintTemplate?> GetTemplateAsync(Guid branchId)
    {
        return await _context.InvoicePrintTemplates.FirstOrDefaultAsync(t => t.BranchId == branchId);
    }

    public async Task<InvoicePrintTemplate?> ResolveTemplateAsync(Guid branchId)
    {
        var template = await _context.InvoicePrintTemplates
            .FirstOrDefaultAsync(t => t.BranchId == branchId && t.IsActive);

        return template ?? await _context.InvoicePrintTemplates
            .FirstOrDefaultAsync(t => t.BranchId == null && t.IsActive);
    }

    public async Task UpsertTemplateAsync(Guid branchId, UpsertInvoicePrintTemplateRequest req)
    {
        var existing = await _context.InvoicePrintTemplates.FirstOrDefaultAsync(t => t.BranchId == branchId);

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
            existing.PaperSize = req.PaperSize ?? "K80";
            existing.FontSize = req.FontSize > 0 ? req.FontSize : 12;
            existing.FontFamily = req.FontFamily ?? "monospace";
            existing.ShowCustomerInfo = req.ShowCustomerInfo;
            existing.ShowCashierName = req.ShowCashierName;
            existing.ShowBranchInfo = req.ShowBranchInfo;
            existing.ShowPaymentMethod = req.ShowPaymentMethod;
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
                BranchName = req.BranchName,
                BranchAddress = req.BranchAddress,
                Phone = req.Phone,
                TaxCode = req.TaxCode,
                PaperSize = req.PaperSize ?? "K80",
                FontSize = req.FontSize > 0 ? req.FontSize : 12,
                FontFamily = req.FontFamily ?? "monospace",
                ShowCustomerInfo = req.ShowCustomerInfo,
                ShowCashierName = req.ShowCashierName,
                ShowBranchInfo = req.ShowBranchInfo,
                ShowPaymentMethod = req.ShowPaymentMethod,
                ThankYouMessage = req.ThankYouMessage,
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
        var template = await _context.InvoicePrintTemplates.FindAsync(id);
        if (template == null)
            throw new KeyNotFoundException("Template not found");

        _context.InvoicePrintTemplates.Remove(template);
        await _context.SaveChangesAsync();
        return true;
    }
}
