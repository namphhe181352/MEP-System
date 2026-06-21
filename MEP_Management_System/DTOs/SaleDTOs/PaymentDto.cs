using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class PaymentDto
{
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public string Method { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = null!;
    public decimal? CashReceived { get; set; }
    public decimal? ChangeAmount { get; set; }
    public string? VietQRString { get; set; }
    public DateTime? QRExpiredAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
