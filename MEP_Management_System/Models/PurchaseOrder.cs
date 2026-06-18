using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class PurchaseOrder
{
    public Guid PurchaseOrderId { get; set; }

    /// <summary>Mã PO hiển thị (VD: PO-2026-0001), UNIQUE (theo SRS BR-01).</summary>
    public string? OrderCode { get; set; }

    public Guid SupplierId { get; set; }

    public Guid UserId { get; set; }

    public decimal? TotalAmount { get; set; }

    /// <summary>Trạng thái: PENDING / APPROVED / REJECTED / COMPLETED / CANCELLED.</summary>
    public string Status { get; set; } = null!;

    /// <summary>UserId của Owner đã duyệt PO (theo UC Approve PO).</summary>
    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    /// <summary>Ngày dự kiến NCC giao hàng.</summary>
    public DateTime? ExpectedDeliveryDate { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();

    public virtual Supplier Supplier { get; set; } = null!;

    public virtual User User { get; set; } = null!;

    /// <summary>User đã duyệt (navigation optional, theo ApprovedBy FK).</summary>
    public virtual User? ApprovedByUser { get; set; }
}
