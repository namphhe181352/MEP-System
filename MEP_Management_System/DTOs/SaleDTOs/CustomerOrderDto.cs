using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class CustomerOrderDto
{
    public Guid InvoiceId { get; set; }
    public string? InvoiceCode { get; set; }
    public decimal TotalAmount { get; set; }
    public string? PaymentMethod { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public int ItemCount { get; set; }
}
