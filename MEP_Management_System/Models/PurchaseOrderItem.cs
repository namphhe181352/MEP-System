using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class PurchaseOrderItem
{
    public Guid PoItemId { get; set; }

    public Guid PurchaseOrderId { get; set; }

    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
}
