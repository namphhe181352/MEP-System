using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.SaleDTOs;

/// <summary>
/// Thông tin sản phẩm trong hóa đơn kèm trạng thái được phép đổi/trả.
/// Mỗi item được đánh giá độc lập dựa trên policy của category riêng.
/// </summary>
public class InvoiceItemEligibilityDto
{
    public Guid InvoiceItemId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductCode { get; set; }
    public string? Barcode { get; set; }
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>ID nhóm hàng (ProductTaxonomy.TaxonomyId).</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Tên nhóm hàng (ProductTaxonomy.Name).</summary>
    public string? CategoryName { get; set; }

    /// <summary>Cho phép trả hàng (Refund) hay không.</summary>
    public bool RefundAllowed { get; set; }

    /// <summary>Cho phép đổi hàng (Exchange) hay không.</summary>
    public bool ExchangeAllowed { get; set; }

    /// <summary>Số ngày còn lại được phép trả (dựa trên policy). Null nếu không được phép.</summary>
    public int? RemainingRefundDays { get; set; }

    /// <summary>Số ngày còn lại được phép đổi (dựa trên policy). Null nếu không được phép.</summary>
    public int? RemainingExchangeDays { get; set; }

    /// <summary>Lý do không được phép (null nếu được phép).</summary>
    public string? RestrictionReason { get; set; }
}

public class InvoiceEligibilityResponse
{
    public Guid InvoiceId { get; set; }
    public string? InvoiceCode { get; set; }
    public DateTime? InvoiceDate { get; set; }
    public List<InvoiceItemEligibilityDto> Items { get; set; } = new();
}
