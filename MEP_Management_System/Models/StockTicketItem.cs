using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class StockTicketItem
{
    public Guid TicketItemId { get; set; }

    public Guid StockTicketId { get; set; }

    public Guid BranchProductId { get; set; }

    public decimal SystemQuantity { get; set; }

    public decimal ActualQuantity { get; set; }

    public decimal Discrepancy { get; set; }

    public decimal? UnitPrice { get; set; }

    /// <summary>Snapshot tên đơn vị cơ bản tại thời điểm tạo phiếu.</summary>
    public string? UnitName { get; set; }

    /// <summary>FK tới dòng hàng của phiếu nhập gốc (dùng khi xuất trả NCC).</summary>
    public Guid? InwardTicketItemId { get; set; }

    /// <summary>Thời hạn bảo hành (số lượng). 0 = không bảo hành.</summary>
    public int WarrantyPeriod { get; set; }

    /// <summary>Đơn vị bảo hành: DAY, MONTH, YEAR.</summary>
    public string WarrantyUnit { get; set; } = "MONTH";

    /// <summary>Ngày hết hạn bảo hành = ImportDate + WarrantyPeriod theo WarrantyUnit.</summary>
    public DateTime? WarrantyExpiryDate { get; set; }

    public virtual BranchProduct BranchProduct { get; set; } = null!;

    public virtual StockTicket StockTicket { get; set; } = null!;

    // Self-referencing: dòng hàng nhập gốc (dùng cho xuất trả NCC)
    public virtual StockTicketItem? InwardTicketItem { get; set; }
    public virtual ICollection<StockTicketItem> OutwardTicketItems { get; set; } = new List<StockTicketItem>();
}
