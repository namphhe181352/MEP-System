using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class ReturnItem
{
    public Guid ReturnItemId { get; set; }

    public Guid ReturnOrderId { get; set; }

    public Guid BranchProductId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>Tiền hoàn cho dòng hàng này (snapshot lúc trả).</summary>
    public decimal? RefundAmount { get; set; }

    /// <summary>Lý do trả: DEFECTIVE / WRONG_ITEM / NOT_SATISFIED / OTHER.</summary>
    public string? Reason { get; set; }

    public virtual BranchProduct BranchProduct { get; set; } = null!;

    public virtual WarrantyTicket? WarrantyTicket { get; set; }

    public virtual ReturnOrder ReturnOrder { get; set; } = null!;
}
