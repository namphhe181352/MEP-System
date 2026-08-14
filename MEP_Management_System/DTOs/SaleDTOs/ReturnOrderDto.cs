using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class ReturnOrderDto
{
    public Guid ReturnOrderId { get; set; }
    public string? ReturnCode { get; set; }
    public Guid OrderId { get; set; }
    public string? InvoiceCode { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid UserId { get; set; }
    public string? StaffName { get; set; }
    public decimal RefundAmount { get; set; }
    public string? RefundMethod { get; set; }
    public string? ReturnType { get; set; }
    public string? Status { get; set; }
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<ReturnItemDto> Items { get; set; } = new();
}

public class ReturnItemDto
{
    public Guid ReturnItemId { get; set; }
    public Guid BranchProductId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public decimal Quantity { get; set; }

    /// <summary>Hệ số quy đổi từ ĐVT bán về ĐVT cơ bản (vd 1 Thùng = 20 Cái -> 20).</summary>
    public decimal ConversionRate { get; set; } = 1;

    /// <summary>SL quy về ĐVT cơ bản = Quantity * ConversionRate.</summary>
    public decimal BaseQuantity { get; set; }

    /// <summary>Đơn vị bán (khớp với ConversionRate). Ví dụ "Thùng". Fallback = BaseUnit.</summary>
    public string? Unit { get; set; }

    /// <summary>Đơn vị cơ bản của sản phẩm (Product.BaseUnit). Ví dụ "Cái".</summary>
    public string? BaseUnit { get; set; }

    public decimal? RefundAmount { get; set; }
    public string? Reason { get; set; }

    /// <summary>ID nhóm hàng (ProductTaxonomy.TaxonomyId).</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Tên nhóm hàng (ProductTaxonomy.Name).</summary>
    public string? CategoryName { get; set; }
}
