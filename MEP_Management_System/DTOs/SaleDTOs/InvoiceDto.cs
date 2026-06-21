using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class InvoiceDto
{
    public Guid InvoiceId { get; set; }
    public string? InvoiceCode { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PromotionCode { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? PaymentMethod { get; set; }
    public string Status { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<InvoiceItemDto> Items { get; set; } = new();
}
