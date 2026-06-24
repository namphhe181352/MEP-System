using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class Product
{
    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = null!;

    public string ProductName { get; set; } = null!;

    public string? Barcode { get; set; }

    public string BaseUnit { get; set; } = null!;

    public string? Specification { get; set; }

    public string? ItemType { get; set; }

    public string? BrandName { get; set; }

    public string? CategoryName { get; set; }

    public Guid? SupplierId { get; set; }

    public decimal? Weight { get; set; }

    public string? WeightUnit { get; set; }

    public decimal? Width { get; set; }

    public decimal? Length { get; set; }

    public decimal? Height { get; set; }

    public string? SizeUnit { get; set; }

    public short? SalesChannelLinked { get; set; }

    /// <summary>Trạng thái sản phẩm: 1=Active, 0=Inactive. Mặc định 1 (theo SRS).</summary>
    public short? Status { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<BranchProduct> BranchProducts { get; set; } = new List<BranchProduct>();

    public virtual ICollection<MediaResource> MediaResources { get; set; } = new List<MediaResource>();

    public virtual ICollection<ProductAttribute> ProductAttributes { get; set; } = new List<ProductAttribute>();

    public virtual ICollection<ProductConversionUnit> ProductConversionUnits { get; set; } = new List<ProductConversionUnit>();

    public virtual ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();

    public virtual Supplier? Supplier { get; set; }
}
