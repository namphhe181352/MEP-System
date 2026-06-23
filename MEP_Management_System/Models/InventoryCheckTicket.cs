using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public enum InventoryCheckStatus
{
    Draft,
    Pending,
    Completed,
    Cancelled
}

public class InventoryCheckTicket
{
    public Guid TicketId { get; set; }
    public string TicketCode { get; set; } = null!;
    public Guid BranchId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public InventoryCheckStatus Status { get; set; }
    public string? Notes { get; set; }

    public virtual ICollection<InventoryCheckDetail> Details { get; set; } = new List<InventoryCheckDetail>();
}

public class InventoryCheckDetail
{
    public Guid DetailId { get; set; }
    public Guid TicketId { get; set; }
    public Guid ProductId { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal ActualQuantity { get; set; }
    public decimal Discrepancy { get; set; }

    public virtual InventoryCheckTicket Ticket { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
