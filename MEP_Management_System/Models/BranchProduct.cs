using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class BranchProduct
{
    public Guid BranchProductId { get; set; }

    public Guid BranchId { get; set; }

    public Guid ProductId { get; set; }

    public decimal CostPrice { get; set; }

    public decimal SellPrice { get; set; }

    public decimal ActualStock { get; set; }

    public decimal ReservedStock { get; set; }

    public decimal AvailableStock { get; set; }

    public decimal? MinimumStock { get; set; }

    public decimal? MaximumStock { get; set; }

    public string? ShelfLocation { get; set; }

    public short? IsActive { get; set; }

    public short? DirectSale { get; set; }

    public DateTime? EstimatedOutAt { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ProductConversionUnit> ProductConversionUnits { get; set; } = new List<ProductConversionUnit>();

    public virtual ICollection<ReturnItem> ReturnItems { get; set; } = new List<ReturnItem>();

    public virtual ICollection<StockTicketItem> StockTicketItems { get; set; } = new List<StockTicketItem>();
}
