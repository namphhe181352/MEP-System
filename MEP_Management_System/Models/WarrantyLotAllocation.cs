using System;

namespace MEP_Management_System.Models;

/// <summary>
/// Phân bổ số lượng gửi bảo hành NCC theo từng lô nhập (StockTicketItem).
/// Một WarrantyTicket có thể được phân bổ trên nhiều lô (FEFO) và một lô có thể
/// phục vụ nhiều WarrantyTicket. SL_Con_BH_NCC của 1 lô = ActualQuantity − SUM(AllocateQty).
/// </summary>
public partial class WarrantyLotAllocation
{
    public Guid AllocationId { get; set; }

    /// <summary>FK tới WarrantyTicket (phiếu gửi bảo hành).</summary>
    public Guid WarrantyTicketId { get; set; }

    /// <summary>FK tới StockTicketItem (lô nhập gốc cung cấp suất bảo hành).</summary>
    public Guid TicketItemId { get; set; }

    /// <summary>Số lượng (đơn vị cơ bản) được phân bổ từ lô này cho phiếu bảo hành.</summary>
    public decimal AllocateQty { get; set; }

    public DateTime? CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    // Navigation
    public virtual WarrantyTicket WarrantyTicket { get; set; } = null!;
    public virtual StockTicketItem StockTicketItem { get; set; } = null!;
}
