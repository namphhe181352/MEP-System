using MEP_Management_System.Models;

namespace MEP_Management_System.Services.InventoryService;

public interface IStockPrintTemplateService
{
    Task<StockPrintTemplate?> ResolveTemplateAsync(Guid branchId, string ticketType);
    Task<string> BuildTicketHtmlAsync(StockTicket ticket, Branch branch, StockPrintTemplate tpl);
    Task UpsertTemplateAsync(Guid branchId, UpsertStockPrintTemplateRequest req);
    Task<bool> DeleteTemplateAsync(Guid id);
}

public class UpsertStockPrintTemplateRequest
{
    public string TicketType { get; set; } = "STOCK_CHECK";
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
    public bool ShowSupplier { get; set; } = true;
    public bool ShowBranchInfo { get; set; } = true;
    public bool ShowSignature { get; set; } = true;
    public string? CustomFieldsJson { get; set; }
    public bool IsActive { get; set; } = true;
}
