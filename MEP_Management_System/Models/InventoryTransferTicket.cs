using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public enum InventoryTransferStatus
{
    Draft,
    Pending,
    Shipping,
    Completed,
    Cancelled
}

public class InventoryTransferTicket
{
    public Guid TransferId { get; set; }
    public string TransferCode { get; set; } = null!;
    public Guid FromBranchId { get; set; }
    public Guid ToBranchId { get; set; }
    public InventoryTransferStatus Status { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }

    public virtual ICollection<InventoryTransferDetail> Details { get; set; } = new List<InventoryTransferDetail>();
}

public class InventoryTransferDetail
{
    public Guid DetailId { get; set; }
    public Guid TransferId { get; set; }
    public Guid ProductId { get; set; }
    public int TransferQuantity { get; set; }

    public virtual InventoryTransferTicket Transfer { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
