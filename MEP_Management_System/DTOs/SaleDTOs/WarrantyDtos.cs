using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class WarrantyTicketDto
{
    public Guid WarrantyTicketId { get; set; }
    public Guid ReturnItemId { get; set; }
    public Guid BranchProductId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? SkuCode { get; set; }
    public decimal Quantity { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class AssignSupplierRequest
{
    public Guid SupplierId { get; set; }
}

public class SuggestedSupplierDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public string? ContactPhone { get; set; }
    public int PurchaseCount { get; set; }
    public DateTime? LastPurchaseAt { get; set; }
}