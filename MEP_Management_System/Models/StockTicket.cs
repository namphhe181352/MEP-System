using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class StockTicket
{
    public Guid StockTicketId { get; set; }

    /// <summary>Mã phiếu kiểm kê (VD: STK-2026-0001), UNIQUE (theo SRS).</summary>
    public string? TicketCode { get; set; }

    public Guid BranchId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Loại phiếu: STOCK_CHECK / WRITE_OFF / TRANSFER / RETURN_SUPPLIER (theo UC 2.5).</summary>
    public string? TicketType { get; set; }

    /// <summary>Trạng thái: PENDING / COMPLETED / CANCELLED.</summary>
    public string? Status { get; set; }

    public string? Notes { get; set; }

    // === Audit trail ===
    public DateTime? CompletedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancelReason { get; set; }

    public virtual User? ConfirmedByUser { get; set; }
    public virtual User? CancelledByUser { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<StockTicketItem> StockTicketItems { get; set; } = new List<StockTicketItem>();

    public virtual User User { get; set; } = null!;

}
