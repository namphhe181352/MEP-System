using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class Supplier
{
    public Guid SupplierId { get; set; }

    public string SupplierName { get; set; } = null!;

    public string? ContactPhone { get; set; }

    /// <summary>Email liên hệ NCC (thêm theo SRS UC Supplier Management).</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Địa chỉ kho/VP NCC.</summary>
    public string? Address { get; set; }

    /// <summary>Trạng thái NCC: 1=Active, 0=Inactive. Mặc định 1.</summary>
    public short? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}
