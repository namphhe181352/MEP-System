using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public enum InventoryCheckTicketStatus
{
    Draft,
    WaitingForApproval,
    Completed,
    Cancelled
}

public class InventoryCheckTicket
{
    public Guid TicketId { get; set; }
    public string TicketCode { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public InventoryCheckTicketStatus Status { get; set; }
    public Guid BranchId { get; set; }
    public Guid CreatedByUserId { get; set; }

    // Navigation property for Branch
    public virtual Branch? Branch { get; set; }

    // Navigation properties for CreatedBy and Assignee
    public virtual User? CreatedByUser { get; set; }
    public virtual User? AssigneeUser { get; set; }

    // audit trail fields
    public Guid? FilledByUserId { get; set; }
    public DateTime? FilledAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledReason { get; set; }

    // Navigation properties for audit fields
    public virtual User? FilledByUser { get; set; }
    public virtual User? ApprovedByUser { get; set; }
    public virtual User? CancelledByUser { get; set; }
    public virtual User? RecountRequestedByUser { get; set; }

    // Assignee for inventory check task
    public Guid? AssigneeUserId { get; set; }

    // Link to StockTicket for adjustment
    public Guid? LinkedStockTicketId { get; set; }

    public string? Notes { get; set; }

    // Recount fields - lưu lịch sử yêu cầu đếm lại
    public int RecountNumber { get; set; } = 0;
    public DateTime? RecountRequestedAt { get; set; }
    public Guid? RecountRequestedByUserId { get; set; }
    public string? RecountReason { get; set; }

    // Notification tracking
    public virtual ICollection<InventoryCheckDetail> Details { get; set; } = new List<InventoryCheckDetail>();
    public virtual StockTicket? LinkedStockTicket { get; set; }
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

public class InventoryCheckDetail
{
    public Guid DetailId { get; set; }
    public Guid TicketId { get; set; }
    public Guid ProductId { get; set; }
    public int SystemQuantity { get; set; }
    public int ActualQuantity { get; set; }
    public int Discrepancy { get; set; }

    // Flag to indicate if this detail has been counted
    public bool IsCounted { get; set; }

    // Reason note for discrepancy (e.g., "hao hụt", "nhầm lẫn", "mất hàng")
    public string? ReasonNote { get; set; }

    // Audit for who counted this specific line
    public Guid? CountedByUserId { get; set; }
    public DateTime? CountedAt { get; set; }

    // Navigation property for counted by user
    public virtual User? CountedByUser { get; set; }

    public virtual InventoryCheckTicket Ticket { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
