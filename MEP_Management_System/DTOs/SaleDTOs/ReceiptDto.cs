using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class ReceiptDto
{
    public Guid ReceiptId { get; set; }
    public string ReceiptCode { get; set; } = null!;
    public Guid InvoiceId { get; set; }
    public string? HtmlContent { get; set; }
    public DateTime? PrintedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
