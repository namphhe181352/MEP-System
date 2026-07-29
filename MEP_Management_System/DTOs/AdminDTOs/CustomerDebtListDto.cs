namespace MEP_Management_System.DTOs;

public class CustomerDebtListItemDto
{
    public Guid CustomerId { get; set; }
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public decimal CurrentDebt { get; set; }
    public decimal DebtLimit { get; set; }
    public DateTime? LastOrderDate { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public bool IsOverdue { get; set; }
}

public class CustomerDebtOverviewDto
{
    public decimal TotalDebt { get; set; }
    public decimal OverdueDebt { get; set; }
    public decimal AverageDebtPerCustomer { get; set; }
    public int OverdueCount { get; set; }
    public int ActiveCount { get; set; }
    public int TotalCustomers { get; set; }
}
