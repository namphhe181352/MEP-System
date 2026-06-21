namespace MEP_Management_System.Models;

public partial class DailySummary
{
    public Guid SummaryId { get; set; }

    public Guid BranchId { get; set; }

    public DateTime SummaryDate { get; set; }

    public decimal TotalRevenue { get; set; } = 0;

    public int TotalOrders { get; set; } = 0;

    public decimal TotalRefunds { get; set; } = 0;

    public decimal TotalCash { get; set; } = 0;

    public decimal TotalTransfer { get; set; } = 0;

    public decimal TotalDebt { get; set; } = 0;

    public decimal OpeningCash { get; set; } = 0;

    public decimal ClosingCash { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Branch Branch { get; set; } = null!;
}
