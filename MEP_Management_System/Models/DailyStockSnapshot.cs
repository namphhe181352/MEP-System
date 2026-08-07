using System;

namespace MEP_Management_System.Models;

/// <summary>
/// Chốt tồn kho cuối ngày - dùng để tính Tồn Đầu Kỳ nhanh
/// mà không cần scan toàn bộ inventory_ledger từ đầu.
/// </summary>
public partial class DailyStockSnapshot
{
    public Guid SnapshotId { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public Guid BranchId { get; set; }
    public Guid BranchProductId { get; set; }

    /// <summary>Số lượng tồn cuối ngày</summary>
    public decimal ClosingQuantity { get; set; }

    /// <summary>Giá vốn bình quân tại thời điểm chốt (NUMERIC 18,4)</summary>
    public decimal CostPrice { get; set; }

    /// <summary>Giá trị tồn = ClosingQuantity * CostPrice</summary>
    public decimal ClosingValue { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
