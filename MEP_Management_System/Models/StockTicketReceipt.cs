using System;

namespace MEP_Management_System.Models;

public class StockTicketReceipt
{
    public Guid ReceiptId { get; set; }
    public string ReceiptCode { get; set; } = null!;
    public Guid StockTicketId { get; set; }
    public string? HtmlContent { get; set; }
    public DateTime? PrintedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual StockTicket StockTicket { get; set; } = null!;
}
