using System;

namespace MEP_Management_System.Models;

public partial class OrderItem
{
    public Guid OrderItemId { get; set; }

    public Guid OrderId { get; set; }

    public Guid BranchProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal SellPrice { get; set; }

    public virtual BranchProduct BranchProduct { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;
}
