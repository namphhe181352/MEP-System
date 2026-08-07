using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class InvoiceItem
{
    public Guid InvoiceItemId { get; set; }

    public Guid InvoiceId { get; set; }

    public Guid BranchProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal SellPrice { get; set; }

    /// <summary>Đơn vị đã chọn khi bán (ví dụ: "Cuộn", "Mét"). Null = BaseUnit.</summary>
    public string? SelectedUnit { get; set; }

    /// <summary>Hệ số quy đổi tại thời điểm bán (ví dụ: 100 = 1 Cuộn = 100 Mét).</summary>
    public decimal? ConvertValue { get; set; }

    /// <summary>Giá vốn snapshot tại thời điểm bán hàng (NUMERIC 18,4).</summary>
    public decimal CostPrice { get; set; }

    public virtual BranchProduct BranchProduct { get; set; } = null!;

    public virtual Invoice Invoice { get; set; } = null!;
}
