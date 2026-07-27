using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class InvoiceItemDto
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

    /// <summary>Đơn vị bán được chọn (null = BaseUnit).</summary>
    public string? SelectedUnit { get; set; }

    /// <summary>Hệ số quy đổi (null hoặc 1 = BaseUnit).</summary>
    public decimal? ConvertValue { get; set; }

    /// <summary>ID nhóm hàng (ProductTaxonomy.TaxonomyId). Dùng để tra cứu chính sách đổi/trả.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>Tên nhóm hàng (ProductTaxonomy.Name).</summary>
    public string? CategoryName { get; set; }
}
