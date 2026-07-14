using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class ReturnOrder
{
    public Guid ReturnOrderId { get; set; }

    /// <summary>Mã phiếu trả hàng (VD: RET-2026-0001), UNIQUE.</summary>
    public string? ReturnCode { get; set; }

    public Guid OrderId { get; set; }

    public Guid UserId { get; set; }

    public decimal RefundAmount { get; set; }

    /// <summary>Phương thức hoàn tiền: CASH / TRANSFER / EXCHANGE.</summary>
    public string? RefundMethod { get; set; }

    /// <summary>Loại đổi trả: EXCHANGE (đổi hàng) / REFUND (trả hàng).</summary>
    public string? ReturnType { get; set; }

    /// <summary>Trạng thái: PENDING / COMPLETED / CANCELLED.</summary>
    public string? Status { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<ReturnItem> ReturnItems { get; set; } = new List<ReturnItem>();

    public virtual User User { get; set; } = null!;
}
