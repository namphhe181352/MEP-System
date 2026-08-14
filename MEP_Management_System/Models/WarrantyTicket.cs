using System;

namespace MEP_Management_System.Models;

public partial class WarrantyTicket
{
    public Guid WarrantyTicketId { get; set; }

    /// <summary>FK tới ReturnItem gốc (phiếu đổi trả loại EXCHANGE).</summary>
    public Guid ReturnItemId { get; set; }

    /// <summary>FK tới BranchProduct.</summary>
    public Guid BranchProductId { get; set; }

    /// <summary>FK tới Product (để tiện query).</summary>
    public Guid ProductId { get; set; }

    /// <summary>Số lượng sản phẩm bảo hành (theo ĐVT bán).</summary>
    public decimal Quantity { get; set; }

    /// <summary>Hệ số quy đổi từ ĐVT bán về ĐVT cơ bản.</summary>
    public decimal ConversionRate { get; set; } = 1;

    /// <summary>SL quy về ĐVT cơ bản = Quantity * ConversionRate.</summary>
    public decimal BaseQuantity { get; set; }

    /// <summary>FK tới Supplier được chọn gửi bảo hành.</summary>
    public Guid? SupplierId { get; set; }

    /// <summary>
    /// Trạng thái bảo hành:
    /// PENDING_ASSIGN - Chờ phân công NCC
    /// AWAITING_SUPPLIER - Chờ hàng về từ NCC
    /// COMPLETED - Đã nhận hàng về, khôi phục tồn
    /// </summary>
    public string? Status { get; set; }

    public string? Notes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? AssignedAt { get; set; }

    public Guid? AssignedBy { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Guid? CompletedBy { get; set; }

    // Navigation
    public virtual ReturnItem ReturnItem { get; set; } = null!;
    public virtual Supplier? Supplier { get; set; }
}