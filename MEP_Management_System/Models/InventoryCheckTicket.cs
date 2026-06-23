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
    public string TicketCode { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public InventoryCheckTicketStatus Status { get; set; }
    public Guid BranchId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string? Notes { get; set; }

    public virtual ICollection<InventoryCheckDetail> Details { get; set; } = new List<InventoryCheckDetail>();
}

public class InventoryCheckDetail
{
    public Guid DetailId { get; set; }
    public Guid TicketId { get; set; }
    public Guid ProductId { get; set; }
    public int SystemQuantity { get; set; }
    public int ActualQuantity { get; set; }
    public int Discrepancy { get; set; }

    public virtual InventoryCheckTicket Ticket { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
