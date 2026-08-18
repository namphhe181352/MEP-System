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

    public Guid? SupplierId { get; set; }

    /// <summary>FK tới phiếu nhập gốc (dùng khi xuất trả NCC).</summary>
    public Guid? InwardTicketId { get; set; }

    public decimal TotalAmount { get; set; }

    /// <summary>Loại phiếu: STOCK_CHECK / WRITE_OFF / TRANSFER / RETURN_SUPPLIER (theo UC 2.5).</summary>
    public string? TicketType { get; set; }

    /// <summary>Trạng thái: PENDING / COMPLETED / CANCELLED.</summary>
    public string? Status { get; set; }

    public string? Notes { get; set; }

    public string? Reason { get; set; }

    // === Audit trail ===
    public DateTime? CompletedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancelReason { get; set; }
    // === Recount fields (cho phiếu kiểm kê) ===
    public int RecountNumber { get; set; } = 0;
    public DateTime? RecountRequestedAt { get; set; }
    public Guid? RecountRequestedByUserId { get; set; }
    public string? RecountReason { get; set; }

    public virtual User? ConfirmedByUser { get; set; }
    public virtual User? CancelledByUser { get; set; }
    public virtual User? RecountRequestedByUser { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<StockTicketItem> StockTicketItems { get; set; } = new List<StockTicketItem>();

    public virtual Supplier? Supplier { get; set; }

    // Self-referencing: phiếu nhập gốc (dùng cho xuất trả NCC)
    public virtual StockTicket? InwardTicket { get; set; }
    public virtual ICollection<StockTicket> OutwardTickets { get; set; } = new List<StockTicket>();

    public virtual User User { get; set; } = null!;

}
