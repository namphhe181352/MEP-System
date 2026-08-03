namespace MEP_Management_System.DTOs;

public class DashboardStatsDto
{
    public int ActiveTenants { get; set; }
    public decimal SubscriptionRevenue { get; set; }
    public decimal B2bCommission { get; set; }
    public int OverdueAlerts { get; set; }
    public decimal OverdueDebtAmount { get; set; }
}
