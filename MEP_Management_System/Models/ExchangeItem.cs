using System;

namespace MEP_Management_System.Models;

/// <summary>
/// Hàng mới (SP B) xuất cho khách trong phiếu đổi hàng chênh lệch (ReturnOrder.ReturnType=EXCHANGE).
/// </summary>
public class ExchangeItem
{
    public Guid ExchangeItemId { get; set; } = Guid.NewGuid();

    public Guid ReturnOrderId { get; set; }       // FK → ReturnOrder (ReturnType=EXCHANGE)

    public Guid BranchProductId { get; set; }      // SP B (hàng mới)

    public Guid? ProductId { get; set; }           // snapshot cho filter

    public decimal Quantity { get; set; }           // SL theo ĐVT bán

    public decimal ConversionRate { get; set; } = 1m; // ĐVT bán → ĐVT cơ bản

    public decimal BaseQuantity { get; set; }       // = Quantity * ConversionRate

    public decimal UnitPrice { get; set; }          // snapshot SellPrice lúc đổi

    public decimal LineTotal { get; set; }          // = Quantity * UnitPrice

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ReturnOrder? ReturnOrder { get; set; }
    public virtual BranchProduct? BranchProduct { get; set; }
}
