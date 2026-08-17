using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

/// <summary>
/// Phiếu xuất kho nội bộ (Goods Issue).
/// Là entity độc lập, ghi nhận phiếu xuất kèm thông tin khách hàng/tham chiếu
/// theo contract FE module Xuất Kho (Goods Issue).
/// <b>NOTE:</b> Đây là model code-first mới. Cần chạy migration để tạo bảng
/// <c>goods_issues</c> trước khi sử dụng ở runtime.
/// </summary>
public partial class GoodsIssue
{
    public Guid GoodsIssueId { get; set; }

    /// <summary>Mã phiếu xuất, VD: XK01133.</summary>
    public string IssueNumber { get; set; } = "";

    /// <summary>Loại phiếu xuất: Xuất kho bán hàng / Xuất kho nội bộ / Xuất kho khác / ...</summary>
    public string IssueType { get; set; } = "Xuất kho khác";

    /// <summary>Định danh khách hàng/đối tượng do FE gửi (mã hoặc guid dạng string).</summary>
    public string? CustomerId { get; set; }

    public string? CustomerName { get; set; }

    public string? Description { get; set; }

    /// <summary>Tham chiếu đơn hàng / chứng từ gốc.</summary>
    public string? Reference { get; set; }

    public DateTime? IssueDate { get; set; }

    /// <summary>Tên người lập phiếu (display).</summary>
    public string? CreatedBy { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public string SyncStatus { get; set; } = "Chưa đồng bộ";

    public string? SyncNumber { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal? TotalPayment { get; set; }

    public string? PaymentMethod { get; set; }

    public Guid BranchId { get; set; }

    /// <summary>PENDING / COMPLETED / CANCELLED.</summary>
    public string Status { get; set; } = "PENDING";

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual ICollection<GoodsIssueItem> Items { get; set; } = new List<GoodsIssueItem>();

    public virtual ICollection<GoodsIssueAttachment> Attachments { get; set; } = new List<GoodsIssueAttachment>();
}