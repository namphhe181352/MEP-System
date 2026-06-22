using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class Order
{
    public Guid OrderId { get; set; }

    public string OrderCode { get; set; } = null!;

    public Guid BranchId { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid UserId { get; set; }

    public decimal TotalAmount { get; set; }

    /// <summary>Tiền giảm giá (theo SRS Business Rules BR-75).</summary>
    public decimal? DiscountAmount { get; set; }

    /// <summary>Mã khuyến mãi được áp dụng (BR-75).</summary>
    public string? PromotionCode { get; set; }

    /// <summary>Tiền thuế VAT (áp dụng theo SystemConfig.VAT_RATE).</summary>
    public decimal? TaxAmount { get; set; }

    /// <summary>Phương thức thanh toán: CASH / TRANSFER / DEBT / COMBINED.</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>Trạng thái: PENDING / COMPLETED / CANCELLED.</summary>
    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual Customer? Customer { get; set; }

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<ReturnOrder> ReturnOrders { get; set; } = new List<ReturnOrder>();

    public virtual User User { get; set; } = null!;
}
