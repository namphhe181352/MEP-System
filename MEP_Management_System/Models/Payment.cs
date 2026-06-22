namespace MEP_Management_System.Models;

public partial class Payment
{
    public Guid PaymentId { get; set; }

    public Guid InvoiceId { get; set; }

    /// <summary>Cash | Transfer | Combined | Debt</summary>
    public string Method { get; set; } = null!;

    public decimal Amount { get; set; }

    /// <summary>Pending | Confirmed | Cancelled</summary>
    public string Status { get; set; } = "Pending";

    public decimal? CashReceived { get; set; }

    public decimal? ChangeAmount { get; set; }

    /// <summary>VietQR string for Transfer method</summary>
    public string? VietQRString { get; set; }

    public DateTime? QRExpiredAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optional — chi nhánh ghi nhận giao dịch. Nullable cho store không có branch.</summary>
    public Guid? BranchId { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;
}
