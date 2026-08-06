using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs;

public class DashboardOverviewDto
{
    public DateTime GeneratedAt { get; set; }
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = null!;
    public string? BranchAddress { get; set; }
    public string? BranchManagerName { get; set; }

    // Business KPIs
    public decimal TotalRevenueToday { get; set; }
    public int TotalOrdersToday { get; set; }
    public decimal TotalRevenueLast30Days { get; set; }
    public decimal TotalRevenueLast7Days { get; set; }
    public decimal TotalRevenueLast24Hours { get; set; }
    public decimal TotalRevenueYearToDate { get; set; }

    public int TotalOrdersLast30Days { get; set; }
    public int TotalOrdersLast7Days { get; set; }
    public int TotalOrdersLast24Hours { get; set; }
    public int TotalOrdersYearToDate { get; set; }

    public decimal AverageOrderValueLast30Days { get; set; }
    public decimal AverageOrderValueLast7Days { get; set; }
    public decimal AverageOrderValueLast24Hours { get; set; }

    public int NewCustomersLast30Days { get; set; }
    public int NewCustomersLast7Days { get; set; }
    public int NewCustomersToday { get; set; }

    public decimal TotalExpenseLast30Days { get; set; }
    public decimal TotalExpenseLast7Days { get; set; }
    public decimal TotalExpenseYearToDate { get; set; }

    public decimal TotalRefundAmountLast30Days { get; set; }
    public decimal TotalRefundAmountLast7Days { get; set; }
    public decimal TotalRefundAmountLast24Hours { get; set; }
    public decimal TotalRefundAmountYearToDate { get; set; }

    public decimal TotalCostOfGoodsSoldLast30Days { get; set; }
    public decimal TotalCostOfGoodsSoldLast7Days { get; set; }
    public decimal TotalCostOfGoodsSoldLast24Hours { get; set; }
    public decimal TotalCostOfGoodsSoldYearToDate { get; set; }

    public decimal TotalProfitLast30Days { get; set; }
    public decimal TotalProfitLast7Days { get; set; }
    public decimal TotalProfitLast24Hours { get; set; }
    public decimal TotalProfitYearToDate { get; set; }
    public decimal GrossMarginPercentLast30Days { get; set; }
    public decimal GrossMarginPercentLast7Days { get; set; }
    public decimal GrossMarginPercentLast24Hours { get; set; }
    public decimal GrossMarginPercentYearToDate { get; set; }

    public DashboardPaymentBreakdownDto PaymentBreakdownLast30Days { get; set; } = new();
    public DashboardPaymentBreakdownDto PaymentBreakdownLast7Days { get; set; } = new();
    public DashboardPaymentBreakdownDto PaymentBreakdownLast24Hours { get; set; } = new();

    public DashboardDebtSummaryDto CustomerDebtSummary { get; set; } = new();

    public int ActiveBranches { get; set; }
    public int ActiveProducts { get; set; }
    public int LowStockProductCount { get; set; }
    public int ActiveCustomerCount { get; set; }
    public decimal ActiveCustomerTotalDebt { get; set; }
    public int OverdueCustomerCount { get; set; }
    public decimal OverdueCustomerTotalDebt { get; set; }
    public decimal AverageDailyRevenueLast7Days { get; set; }
    public decimal AverageDailyRevenueLast30Days { get; set; }
    public int PendingExpenseCount { get; set; }
    public decimal PendingExpenseAmount { get; set; }
    public int PendingOrderCount { get; set; }
    public decimal PendingOrderAmount { get; set; }
    public decimal TotalSupplierDebt { get; set; }
    public decimal SupplierDebtPaidLast30Days { get; set; }
    public decimal RepaymentCoverageRatio { get; set; }

    public List<DashboardTrendPointDto> RevenueTrendLast12Months { get; set; } = new();
    public List<DashboardTrendPointDto> RevenueTrendLast30Days { get; set; } = new();
    public List<DashboardTrendPointDto> ExpenseTrendLast12Months { get; set; } = new();
    public List<DashboardTrendPointDto> RefundTrendLast12Months { get; set; } = new();
    public List<DashboardTrendPointDto> CustomerGrowthTrendLast12Months { get; set; } = new();

    public List<DashboardProductPerformanceDto> TopProductsByRevenue { get; set; } = new();
    public List<DashboardProductPerformanceDto> TopProductsByProfit { get; set; } = new();
    public List<DashboardCustomerPerformanceDto> TopCustomers { get; set; } = new();
    public List<DashboardCategoryPerformanceDto> TopCategoriesByRevenue { get; set; } = new();
    public List<DashboardLowStockProductDto> LowStockProducts { get; set; } = new();
    public List<DashboardExpenseCategoryDto> ExpenseByCategory { get; set; } = new();
    public List<DashboardBranchRevenueDto> TopBranchesByRevenue { get; set; } = new();

    public List<SystemEventDto> RecentSystemEvents { get; set; } = new();

    // Admin-only platform fields (Owner leaves these at default 0)
    public int ActiveTenants { get; set; }
    public decimal SubscriptionRevenue { get; set; }
    public decimal B2bCommission { get; set; }
    public int OverdueAlerts { get; set; }
}

public class DashboardCategoryPerformanceDto
{
    public string CategoryName { get; set; } = null!;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
}

public class DashboardLowStockProductDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public decimal AvailableStock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal Shortage { get; set; }
}

public class DashboardPaymentBreakdownDto
{
    public decimal CashAmount { get; set; }
    public int CashCount { get; set; }
    public decimal TransferAmount { get; set; }
    public int TransferCount { get; set; }
    public decimal CombinedAmount { get; set; }
    public int CombinedCount { get; set; }
    public decimal DebtAmount { get; set; }
    public int DebtCount { get; set; }
}

public class DashboardTrendPointDto
{
    public string Period { get; set; } = null!;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class DashboardProductPerformanceDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public decimal ProfitMarginPercent { get; set; }
}

public class DashboardCustomerPerformanceDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public decimal TotalDebt { get; set; }
}

public class DashboardExpenseCategoryDto
{
    public string CategoryName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public int VoucherCount { get; set; }
}

public class DashboardBranchRevenueDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = null!;
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
}

public class DashboardDebtSummaryDto
{
    public int CustomerCount { get; set; }
    public decimal TotalDebt { get; set; }
    public int OverdueCustomerCount { get; set; }
    public decimal OverdueDebt { get; set; }
}
