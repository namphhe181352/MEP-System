using System;

namespace MEP_Management_System.Models;

public partial class ExpenseVoucher
{
    public Guid VoucherId { get; set; }

    public string VoucherCode { get; set; } = null!;

    public Guid BranchId { get; set; }

    public Guid CategoryId { get; set; }

    public Guid? SupplierId { get; set; }

    public decimal Amount { get; set; }

    public string Reason { get; set; } = null!;

    public string? Note { get; set; }

    public string Status { get; set; } = "PENDING";

    public Guid CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public Guid? ConfirmedBy { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Guid? CancelledBy { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual ExpenseCategory Category { get; set; } = null!;

    public virtual Supplier? Supplier { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual User? ConfirmedByUser { get; set; }

    public virtual User? CancelledByUser { get; set; }
}
