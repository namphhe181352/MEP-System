namespace MEP_Management_System.Models;

public partial class RefundRecord
{
    public Guid RefundId { get; set; }

    /// <summary>Format: RFD-YYYYMMDD-XXXX</summary>
    public string RefundCode { get; set; } = null!;

    public Guid OrderId { get; set; }

    public Guid? InvoiceId { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid UserId { get; set; }

    public decimal RefundAmount { get; set; }

    /// <summary>Cash | Transfer | Original</summary>
    public string? RefundMethod { get; set; }

    /// <summary>PENDING | APPROVED | REJECTED | REFUNDED</summary>
    public string Status { get; set; } = "PENDING";

    public string? Reason { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Ca bán (Shift) ghi nhận hoàn này (đổi hàng lệch giá delta<0). Null cho dữ liệu cũ → fallback theo UserId+thời gian.</summary>
    public Guid? ShiftId { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual Invoice? Invoice { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual User User { get; set; } = null!;
}
