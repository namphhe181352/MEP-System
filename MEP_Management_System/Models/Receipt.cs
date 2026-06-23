namespace MEP_Management_System.Models;

public partial class Receipt
{
    public Guid ReceiptId { get; set; }

    /// <summary>Format: RCP-YYYYMMDD-XXXXXX</summary>
    public string ReceiptCode { get; set; } = null!;

    public Guid InvoiceId { get; set; }

    public string? HtmlContent { get; set; }

    public DateTime? PrintedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Invoice Invoice { get; set; } = null!;
}
