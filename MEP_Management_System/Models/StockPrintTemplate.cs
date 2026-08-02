using System;

namespace MEP_Management_System.Models;

public class StockPrintTemplate
{
    public Guid TemplateId { get; set; }
    public Guid? BranchId { get; set; }
    public string? TicketType { get; set; }
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public bool ShowLogo { get; set; } = false;
    public string? LogoUrl { get; set; }
    public string? BranchName { get; set; }
    public string? BranchAddress { get; set; }
    public string? Phone { get; set; }
    public string? TaxCode { get; set; }
    public string PaperSize { get; set; } = "A5";
    public int FontSize { get; set; } = 12;
    public string FontFamily { get; set; } = "monospace";
    public bool ShowSupplier { get; set; } = true;
    public bool ShowBranchInfo { get; set; } = true;
    public bool ShowSignature { get; set; } = true;
    public string CustomFieldsJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual Branch? Branch { get; set; }
}
