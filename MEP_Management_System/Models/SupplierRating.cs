using System;

namespace MEP_Management_System.Models;

/// <summary>
/// Một lượt đánh giá 1 NCC. Mỗi lần đánh giá = 1 record mới (lưu lịch sử).
/// Tiêu chí đánh giá (1-5 sao): chất lượng / giao hàng / giá cả / thái độ.
/// OverallRating = trung bình 4 tiêu chí.
/// </summary>
public partial class SupplierRating
{
    public Guid RatingId { get; set; }

    public Guid SupplierId { get; set; }

    /// <summary>PO liên quan đến lượt đánh giá (nullable - cho phép đánh giá tổng quát).</summary>
    public Guid? PurchaseOrderId { get; set; }

    /// <summary>Chất lượng sản phẩm (đúng quy cách, không lỗi). 1-5.</summary>
    public short QualityScore { get; set; }

    /// <summary>Giao hàng đúng hạn. 1-5.</summary>
    public short DeliveryScore { get; set; }

    /// <summary>Giá cả cạnh tranh. 1-5.</summary>
    public short PriceScore { get; set; }

    /// <summary>Thái độ phục vụ, hỗ trợ kỹ thuật. 1-5.</summary>
    public short ServiceScore { get; set; }

    /// <summary>Điểm tổng = trung bình 4 tiêu chí, lưu để query nhanh.</summary>
    public decimal OverallRating { get; set; }

    public string? Comment { get; set; }

    public Guid? RatedBy { get; set; }

    public DateTime? RatedAt { get; set; }

    // ======= Navigation =======
    public virtual Supplier? Supplier { get; set; }

    public virtual PurchaseOrder? PurchaseOrder { get; set; }

    public virtual User? RatedByUser { get; set; }
}
