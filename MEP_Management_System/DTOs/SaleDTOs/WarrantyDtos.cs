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

    /// <summary>
    /// Tổng số lượng gửi bảo hành (theo ĐVT cơ bản). Nếu null → dùng WarrantyTicket.BaseQuantity.
    /// </summary>
    public decimal? TotalClaimQty { get; set; }

    /// <summary>
    /// Danh sách lô người dùng TỰ CHỌN CHỈ ĐỊNH (manual).
    /// Nếu null/rỗng → hệ thống tự động phân bổ FEFO (First Expired, First Out).
    /// </summary>
    public List<ManualLotAllocationInput>? ManualLots { get; set; }
}

/// <summary>Input cho việc tự chọn lô khi gửi bảo hành.</summary>
public class ManualLotAllocationInput
{
    public Guid TicketItemId { get; set; }
    public decimal AllocateQty { get; set; }
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

// ============================================================
// FEFO WARRANTY LOT ALLOCATION (Quy tắc 1 + 2)
// ============================================================

/// <summary>
/// Một lô nhập còn đủ điều kiện bảo hành NCC (Quy tắc 1):
///  - Còn hạn bảo hành (WarrantyExpiryDate >= now)
///  - Còn suất bảo hành (SL_Con_BH_NCC &gt; 0)
/// </summary>
public class AvailableWarrantyLotDto
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
    /// <summary>SL đã nhập của lô (ActualQuantity).</summary>
    public decimal SuppliedQuantity { get; set; }
    /// <summary>SL đã được claim cho bảo hành (từ WarrantyLotAllocation).</summary>
    public decimal ClaimedWarrantyQty { get; set; }
    /// <summary>SL_Con_BH_NCC = SuppliedQuantity - ClaimedWarrantyQty.</summary>
    public decimal RemainingWarranty { get; set; }
    public int? DaysUntilExpiry { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
}

/// <summary>Kết quả phân bổ 1 lô cho phiếu bảo hành (sau FEFO hoặc manual).</summary>
public class LotAllocationItemDto
{
    public Guid? AllocationId { get; set; }
    public Guid TicketItemId { get; set; }
    public string TicketCode { get; set; } = "";
    public DateTime? WarrantyExpiryDate { get; set; }
    /// <summary>SL_Con_BH_NCC của lô trước khi phân bổ.</summary>
    public decimal AvailableBefore { get; set; }
    /// <summary>SL thực lấy từ lô này.</summary>
    public decimal AllocateQty { get; set; }
    /// <summary>SL_Con_BH_NCC còn lại sau khi phân bổ.</summary>
    public decimal RemainingAfter { get; set; }
}
public class CreateWarrantyClaimRequest
{
    public decimal Quantity { get; set; }
    public string ClaimType { get; set; } = "REPAIR";
    public string? IssueDescription { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public Guid? ReplacementProductId { get; set; }
}

public class UpdateWarrantyClaimRequest
{
    public string Status { get; set; } = "PENDING";
    public string? ProcessNotes { get; set; }
}

public class WarrantyClaimDto
{
    public Guid ClaimId { get; set; }
    public string? ClaimCode { get; set; }
    public DateTime? ClaimDate { get; set; }
    public decimal ClaimedQuantity { get; set; }
    public string? ClaimType { get; set; }
    public string? IssueDescription { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Status { get; set; }
    public string? ProcessNotes { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? TicketCode { get; set; }
    public string? SupplierName { get; set; }
    public string? ClaimedByUserName { get; set; }
    public string? ProcessedByUserName { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class WarrantyClaimResultDto
{
    public Guid ClaimId { get; set; }
    public string? ClaimCode { get; set; }
    public string? Status { get; set; }
    public decimal RemainingWarranty { get; set; }
}
