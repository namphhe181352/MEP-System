using MEP_Management_System.Models;

namespace MEP_Management_System.Services.SaleServices;

public interface IInvoicePrintTemplateService
{
    Task<InvoicePrintTemplate?> GetTemplateAsync(Guid branchId);
    Task<InvoicePrintTemplate?> ResolveTemplateAsync(Guid branchId);
    Task UpsertTemplateAsync(Guid branchId, UpsertInvoicePrintTemplateRequest req);
    Task<bool> DeleteTemplateAsync(Guid id);
}

public class UpsertInvoicePrintTemplateRequest
{
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public bool ShowLogo { get; set; } = false;
    public string? LogoUrl { get; set; }
    public string? BranchName { get; set; }
    public string? BranchAddress { get; set; }
    public string? Phone { get; set; }
    public string? TaxCode { get; set; }
    public string? PaperSize { get; set; }
    public int FontSize { get; set; } = 12;
    public string? FontFamily { get; set; }
    public bool ShowCustomerInfo { get; set; } = true;
    public bool ShowCashierName { get; set; } = true;
    public bool ShowBranchInfo { get; set; } = true;
    public bool ShowPaymentMethod { get; set; } = true;
    public string? ThankYouMessage { get; set; }
    public string? CustomFieldsJson { get; set; }
    public bool IsActive { get; set; } = true;
}
