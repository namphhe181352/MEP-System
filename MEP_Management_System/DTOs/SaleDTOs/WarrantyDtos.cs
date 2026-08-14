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
    public decimal ConversionRate { get; set; } = 1;
    public decimal BaseQuantity { get; set; }
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
    /// <summary>Tổng SL sản phẩm này đã nhập từ NCC.</summary>
    public decimal TotalSuppliedQuantity { get; set; }
    /// <summary>SL còn lại có thể bảo hành (TotalSupplied - đã gửi bảo hành cho NCC này).</summary>
    public decimal RemainingWarrantyQuantity { get; set; }
    /// <summary>Ngày hết hạn BH gần nhất trong các lô còn hiệu lực.</summary>
    public DateTime? NearestExpiryDate { get; set; }
}

public class SupplierWarrantyBatchDto
{
    public Guid TicketItemId { get; set; }
    public string TicketCode { get; set; } = "";
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public DateTime? ImportDate { get; set; }
    public int WarrantyPeriod { get; set; }
    public string WarrantyUnit { get; set; } = "MONTH";
    public DateTime? WarrantyExpiryDate { get; set; }
    public decimal SuppliedQuantity { get; set; }
    public decimal UsedForWarranty { get; set; }
    public decimal RemainingWarranty { get; set; }
    public int? DaysUntilExpiry { get; set; }
    public string BatchStatus { get; set; } = "ACTIVE";
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
}