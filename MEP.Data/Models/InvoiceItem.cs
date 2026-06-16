using System;
using System.Collections.Generic;

namespace MEP.Data.Models;

public partial class InvoiceItem
{
    public Guid InvoiceItemId { get; set; }

    public Guid InvoiceId { get; set; }

    public Guid BranchProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal SellPrice { get; set; }

    public virtual BranchProduct BranchProduct { get; set; } = null!;

    public virtual Invoice Invoice { get; set; } = null!;
}
