using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class ReturnOrderDto
{
    public Guid ReturnOrderId { get; set; }
    public string? ReturnCode { get; set; }
    public Guid OrderId { get; set; }
    public string? InvoiceCode { get; set; }
    public Guid UserId { get; set; }
    public string? StaffName { get; set; }
    public decimal RefundAmount { get; set; }
    public string? RefundMethod { get; set; }
    public string? Status { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<ReturnItemDto> Items { get; set; } = new();
}

public class ReturnItemDto
{
    public Guid ReturnItemId { get; set; }
    public Guid BranchProductId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public decimal Quantity { get; set; }
    public decimal? RefundAmount { get; set; }
    public string? Reason { get; set; }
}
