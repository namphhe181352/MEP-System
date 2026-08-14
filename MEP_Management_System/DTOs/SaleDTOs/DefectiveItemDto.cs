using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class DefectiveItemDto
{
    public Guid ReturnItemId { get; set; }
    public string ReturnCode { get; set; } = "";
    public DateTime? ExchangeDate { get; set; }

    public Guid BranchProductId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string SkuCode { get; set; } = "";

    public decimal Quantity { get; set; }
    public decimal ConversionRate { get; set; } = 1;
    public decimal BaseQuantity { get; set; }
    public string? Reason { get; set; }

    public string CustomerName { get; set; } = "";
    public string InvoiceCode { get; set; } = "";

    // Warranty workflow fields
    public Guid? WarrantyTicketId { get; set; }
    public string? WarrantyStatus { get; set; }
    public Guid? AssignedSupplierId { get; set; }
    public string? AssignedSupplierName { get; set; }
}
