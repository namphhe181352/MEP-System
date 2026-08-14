using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace MEP_Management_System.Models;

public partial class Product
{
    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = "";

    public string ProductName { get; set; } = "";

    public string? Barcode { get; set; }

    public string BaseUnit { get; set; } = "";

    public string? Specification { get; set; }

    public string? ItemType { get; set; }

    /// <summary>
    /// Brand ID tham chiếu đến product_taxonomies (type = 'Brand').
    /// Null = dùng BrandName string (legacy).
    /// </summary>
    public Guid? BrandTaxonomyId { get; set; }

    /// <summary>
    /// Category ID tham chiếu đến product_taxonomies (type = 'Category').
    /// Null = dùng CategoryName string (legacy).
    /// </summary>
    public Guid? CategoryTaxonomyId { get; set; }

    /// <summary>
    /// Legacy field: kept for backward compatibility.
    /// Nên dùng BrandTaxonomyId thay vì field này.
    /// </summary>
    public string? BrandName { get; set; }

    /// <summary>
    /// Legacy field: kept for backward compatibility.
    /// Nên dùng CategoryTaxonomyId thay vì field này.
    /// </summary>
    public string? CategoryName { get; set; }

    public Guid? SupplierId { get; set; }

    public decimal? Weight { get; set; }

    public string? WeightUnit { get; set; }

    public decimal? Width { get; set; }

    public decimal? Length { get; set; }

    public decimal? Height { get; set; }

    public string? SizeUnit { get; set; }

    public short? SalesChannelLinked { get; set; }

    /// <summary>Thời hạn bảo hành mặc định (số lượng). 0 = không bảo hành.</summary>
    public int WarrantyPeriod { get; set; } = 12;

    /// <summary>Đơn vị bảo hành: DAY, MONTH, YEAR. Mặc định MONTH.</summary>
    public string WarrantyUnit { get; set; } = "MONTH";

    /// <summary>Trạng thái sản phẩm: 1=Active, 0=Inactive. Mặc định 1 (theo SRS).</summary>
    public short? Status { get; set; }

    /// <summary>Tenant identifier: Các sản phẩm được cô lập theo chi nhánh/cửa hàng.</summary>
    public Guid? BranchId { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual User? CreatedByUser { get; set; }

    public virtual User? UpdatedByUser { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("BrandTaxonomyId")]
    public virtual ProductTaxonomy? BrandTaxonomy { get; set; }

    [ForeignKey("CategoryTaxonomyId")]
    public virtual ProductTaxonomy? CategoryTaxonomy { get; set; }

    public virtual ICollection<BranchProduct> BranchProducts { get; set; } = new List<BranchProduct>();

    public virtual ICollection<MediaResource> MediaResources { get; set; } = new List<MediaResource>();

    public virtual ICollection<ProductAttribute> ProductAttributes { get; set; } = new List<ProductAttribute>();

    public virtual ICollection<ProductConversionUnit> ProductConversionUnits { get; set; } = new List<ProductConversionUnit>();

    public virtual ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();

    public virtual Supplier? Supplier { get; set; }
}
