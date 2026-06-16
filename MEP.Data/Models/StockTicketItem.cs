using System;
using System.Collections.Generic;

namespace MEP.Data.Models;

public partial class StockTicketItem
{
    public Guid TicketItemId { get; set; }

    public Guid StockTicketId { get; set; }

    public Guid BranchProductId { get; set; }

    public decimal SystemQuantity { get; set; }

    public decimal ActualQuantity { get; set; }

    public decimal Discrepancy { get; set; }

    public virtual BranchProduct BranchProduct { get; set; } = null!;

    public virtual StockTicket StockTicket { get; set; } = null!;
}
