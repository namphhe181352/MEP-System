using System;
using System.Collections.Generic;

namespace MEP.Data.Models;

public partial class ProductAttribute
{
    public Guid AttributeId { get; set; }

    public Guid ProductId { get; set; }

    public string AttributeName { get; set; } = null!;

    public string AttributeValue { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
