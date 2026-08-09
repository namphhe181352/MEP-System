using System;

namespace MEP_Management_System.Models;

/// <summary>
/// Nhật ký biến động kho - mỗi giao dịch COMPLETED ghi 1 dòng.
/// Báo cáo Xuất Nhập Tồn query từ bảng này thay vì join nhiều bảng.
/// </summary>
public partial class InventoryLedger
{
    public Guid LedgerId { get; set; }
    public Guid BranchId { get; set; }
    public Guid BranchProductId { get; set; }

    /// <summary>PURCHASE / SALE / RETURN_SUPPLIER / CUSTOMER_RETURN / BALANCE_ADJUST / WRITE_OFF / TRANSFER</summary>
    public string DocType { get; set; } = null!;

    public Guid DocId { get; set; }

    /// <summary>Mã phiếu (PUR-xxx, INV-xxx, OUT-xxx)</summary>
    public string? DocCode { get; set; }

    public DateTime TransactionTime { get; set; }

    /// <summary>Dương (+) cho nhập, Âm (-) cho xuất</summary>
    public decimal QtyChange { get; set; }

    /// <summary>Đơn giá vốn snapshot tại thời điểm phát sinh (NUMERIC 18,4)</summary>
    public decimal UnitCostPrice { get; set; }

    /// <summary>= QtyChange * UnitCostPrice</summary>
    public decimal TotalCostValue { get; set; }

    /// <summary>Tồn thực tế ngay sau giao dịch</summary>
    public decimal StockAfter { get; set; }

    /// <summary>Giá vốn bình quân ngay sau giao dịch (NUMERIC 18,4)</summary>
    public decimal AvgCostAfter { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
