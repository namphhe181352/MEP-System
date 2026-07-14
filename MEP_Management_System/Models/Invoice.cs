using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class Invoice
{
    public Guid InvoiceId { get; set; }

    /// <summary>Mã hóa đơn hiển thị (VD: INV-2026-0001), UNIQUE.</summary>
    public string? InvoiceCode { get; set; }

    public Guid OrderId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Tổng tiền TRƯỚC chiết khấu (dùng để so ngưỡng).</summary>
    public decimal SubTotalAmount { get; set; }

    /// <summary>% chiết khấu đã áp dụng (null nếu không áp dụng).</summary>
    public decimal? DiscountPercent { get; set; }

    /// <summary>Số tiền đã giảm.</summary>
    public decimal DiscountAmount { get; set; }

    /// <summary>Tổng tiền SAU chiết khấu (số khách thực trả).</summary>
    public decimal TotalAmount { get; set; }

    public string? PaymentMethod { get; set; }

    /// <summary>Trạng thái: COMPLETED / CANCELLED / REFUNDED / ONHOLD.</summary>
    public string? Status { get; set; }

    public string? Note { get; set; }

/// <summary>Ghi chú khi treo hóa đơn (Hold Invoice).</summary>
    public string? HoldNote { get; set; }

    /// <summary>Thời điểm hóa đơn được treo.</summary>
    public DateTime? HeldAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();

    public virtual Order Order { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
