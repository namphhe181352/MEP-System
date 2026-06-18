using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class ProductConversionUnit
{
    public Guid ConversionId { get; set; }

    public Guid ProductId { get; set; }

    public string UnitName { get; set; } = null!;

    public decimal ConvertValue { get; set; }

    public decimal Price { get; set; }

    public short? DirectSale { get; set; }

    public virtual Product Product { get; set; } = null!;
}
