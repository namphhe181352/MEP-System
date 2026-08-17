using System;

namespace MEP_Management_System.Models;

/// <summary>Dòng hàng hóa của phiếu xuất kho nội bộ.</summary>
public partial class GoodsIssueItem
{
    public Guid GoodsIssueItemId { get; set; }

    public Guid GoodsIssueId { get; set; }

    public int LineNumber { get; set; }

    /// <summary>ID/Mã sản phẩm do FE gửi (có thể là ProductCode hoặc ProductId dạng string).</summary>
    public string? ProductId { get; set; }

    public string? ProductCode { get; set; }

    public string? ProductName { get; set; }

    public string? Unit { get; set; }

    public string? LotNumber { get; set; }

    public string? ExpiryDate { get; set; }

    /// <summary>Mã kho do FE chọn (VD: KHO_TONG) hoặc BranchId dạng string.</summary>
    public string? WarehouseId { get; set; }

    public string? WarehouseName { get; set; }

    public string? Location { get; set; }

    public decimal Quantity { get; set; }

    public decimal? UnitPrice { get; set; }

    public decimal TotalAmount { get; set; }

    public virtual GoodsIssue GoodsIssue { get; set; } = null!;
}