namespace MEP_Management_System.Models;

public partial class Shift
{
    public Guid ShiftId { get; set; }

    /// <summary>Format: SHIFT-YYYYMMDD-XXX</summary>
    public string ShiftCode { get; set; } = null!;

    public Guid UserId { get; set; }

    public Guid BranchId { get; set; }

    /// <summary>OPEN | CLOSED</summary>
    public string Status { get; set; } = "OPEN";

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public decimal OpeningBalance { get; set; }

    public decimal? ExpectedCash { get; set; }

    public decimal? ActualCash { get; set; }

    public decimal? Variance { get; set; }

    public decimal TotalRevenue { get; set; } = 0;

    public int TotalOrders { get; set; } = 0;

    public string? Note { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Branch Branch { get; set; } = null!;
}
