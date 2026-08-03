using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using ClosedXML.Excel;

namespace MEP_Management_System.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/dashboard")]
[Tags("Admin (Admin)")]
public class AdminDashboardController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly CustomerDebtService _customerDebtService;
    private static readonly string[] CompletedStatuses = { "COMPLETED", "Completed", "PAID", "Paid", "DONE", "Done" };

    public AdminDashboardController(MEPDatabaseContext context, CustomerDebtService customerDebtService)
    {
        _context = context;
        _customerDebtService = customerDebtService;
    }

    /// <summary>
    /// GET /api/admin/dashboard/stats
    /// Returns: activeTenants, subscriptionRevenue (last 30d), b2bCommission (1% TRANSFER), overdueAlerts
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var thirtyDaysAgo = now.AddDays(-30);

        // activeTenants = Tất cả tài khoản đang hoạt động (Owner + Staff)
        var activeTenants = await _context.Users
            .AsNoTracking()
            .Where(u => u.IsActive == 1)
            .CountAsync();

        // subscriptionRevenue = Doanh thu hóa đơn đã thanh toán trong 30 ngày qua
        var subscriptionRevenue = await _context.Invoices
            .AsNoTracking()
            .Where(i => CompletedStatuses.Contains(i.Status) && i.CreatedAt >= thirtyDaysAgo)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

        // b2bCommission = 1% of total TRANSFER payment orders
        var b2bCommissionRaw = await _context.Orders
            .AsNoTracking()
            .Where(o => o.PaymentMethod == "TRANSFER")
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        var b2bCommission = Math.Round(b2bCommissionRaw * 0.01m, 2);

        // overdueAlerts = Số lượng hóa đơn đang nợ (PENDING)
        var overdueAlerts = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.Status == "PENDING" || i.Status == "Pending")
            .CountAsync();

        return Ok(new DashboardStatsDto
        {
            ActiveTenants = activeTenants,
            SubscriptionRevenue = subscriptionRevenue,
            B2bCommission = b2bCommission,
            OverdueAlerts = overdueAlerts
        });
    }

    /// <summary>
    /// GET /api/admin/dashboard/overview
    /// Returns global system metrics using DashboardOverviewDto
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var thirtyDaysAgo = now.AddDays(-30);
        var last7Days = now.AddDays(-7);
        var last24Hours = now.AddDays(-1);
        var today = now.Date;
        var yearStart = new DateTime(now.Year, 1, 1);
        var twelveMonthsAgo = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

        var branch = await _context.Branches
            .AsNoTracking()
            .Include(b => b.ManagerUser)
            .FirstOrDefaultAsync(b => b.IsActive == 1);
        if (branch == null) return NotFound("No active branch found.");
        var branchId = branch.BranchId;

        // --- Metrics by time range ---
        var (rev30, ord30, aov30, exp30, ref30, cogs30, prof30, gm30, pb30) = await GetMetrics(branchId, thirtyDaysAgo, now);
        var (rev7, ord7, aov7, exp7, ref7, cogs7, prof7, gm7, pb7) = await GetMetrics(branchId, last7Days, now);
        var (rev24, ord24, aov24, exp24, ref24, cogs24, prof24, gm24, pb24) = await GetMetrics(branchId, last24Hours, now);
        var (revYtd, ordYtd, _, expYtd, refYtd, cogsYtd, profYtd, gmYtd, _) = await GetMetrics(branchId, yearStart, now);
        var (revToday, ordToday, _, _, _, _, _, _, _) = await GetMetrics(branchId, today, now);

        // --- Today ---
        var newCustomersToday = await _context.Customers.CountAsync(c => c.BranchId == branchId && c.CreatedAt >= today);
        var newCustomersLast7Days = await _context.Customers.CountAsync(c => c.BranchId == branchId && c.CreatedAt >= last7Days);
        var newCustomersLast30Days = await _context.Customers.CountAsync(c => c.BranchId == branchId && c.CreatedAt >= thirtyDaysAgo);

        // --- Top branches (all active, not just one) ---
        var topBranches = await _context.Branches
            .AsNoTracking()
            .Where(b => b.IsActive == 1)
            .Select(b => new DashboardBranchRevenueDto
            {
                BranchId = b.BranchId,
                BranchName = b.BranchName,
                Revenue = b.Orders.Where(o => o.CreatedAt >= thirtyDaysAgo).Sum(o => (decimal?)o.TotalAmount) ?? 0m,
                OrderCount = b.Orders.Count(o => o.CreatedAt >= thirtyDaysAgo)
            })
            .OrderByDescending(b => b.Revenue)
            .Take(5)
            .ToListAsync();

        // --- Top products by revenue ---
        var topProductsRaw = await _context.InvoiceItems
            .AsNoTracking()
            .Where(ii => ii.Invoice.Order.BranchId == branchId && CompletedStatuses.Contains(ii.Invoice.Status) && ii.Invoice.CreatedAt >= thirtyDaysAgo)
            .GroupBy(ii => new { ii.BranchProduct.ProductId, ii.BranchProduct.Product.ProductName, ii.BranchProduct.Product.ProductCode, ii.BranchProduct.Product.CategoryName })
            .Select(g => new DashboardProductPerformanceDto
            {
                ProductId = g.Key.ProductId,
                ProductCode = g.Key.ProductCode,
                ProductName = g.Key.ProductName,
                CategoryName = g.Key.CategoryName ?? string.Empty,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Quantity * x.SellPrice),
                Cost = g.Sum(x => x.Quantity * x.BranchProduct.CostPrice)
            })
            .ToListAsync();

        topProductsRaw = topProductsRaw.Select(x => {
            x.Profit = x.Revenue - x.Cost;
            x.ProfitMarginPercent = x.Revenue != 0 ? Math.Round(x.Profit / x.Revenue * 100m, 2) : 0m;
            return x;
        }).ToList();

        var topProductsByRevenue = topProductsRaw.OrderByDescending(x => x.Revenue).Take(5).ToList();
        var topProductsByProfit = topProductsRaw.OrderByDescending(x => x.Profit).Take(5).ToList();

        // --- Top customers ---
        var topCustomers = await _context.Orders
            .AsNoTracking()
            .Where(o => o.BranchId == branchId && o.CustomerId != null && (o.Status == "COMPLETED" || o.Status == "Completed") && o.CreatedAt >= thirtyDaysAgo)
            .GroupBy(o => new { o.CustomerId, o.Customer.CustomerName, o.Customer.PhoneNumber, o.Customer.Email })
            .Select(g => new DashboardCustomerPerformanceDto
            {
                CustomerId = g.Key.CustomerId.Value,
                CustomerName = g.Key.CustomerName,
                PhoneNumber = g.Key.PhoneNumber,
                Email = g.Key.Email,
                OrderCount = g.Count(),
                TotalSpent = g.Sum(o => o.TotalAmount)
            })
            .OrderByDescending(x => x.TotalSpent)
            .Take(5)
            .ToListAsync();

        // Set TotalDebt from CustomerDebtService
        var topCustomerIds = topCustomers.Select(c => c.CustomerId).ToList();
        var debtMap = await _customerDebtService.CalculateCurrentDebtBatchAsync(topCustomerIds);
        foreach (var tc in topCustomers)
            tc.TotalDebt = debtMap.TryGetValue(tc.CustomerId, out var d) ? d : 0m;

        // --- Low stock ---
        var lowStockProducts = await _context.BranchProducts
            .AsNoTracking()
            .Where(bp => bp.BranchId == branchId && bp.IsActive == 1 && bp.MinimumStock.HasValue && bp.AvailableStock <= bp.MinimumStock)
            .Select(bp => new DashboardLowStockProductDto
            {
                ProductId = bp.ProductId,
                ProductCode = bp.Product.ProductCode,
                ProductName = bp.Product.ProductName,
                CategoryName = bp.Product.CategoryName ?? string.Empty,
                AvailableStock = bp.AvailableStock,
                MinimumStock = bp.MinimumStock ?? 0,
                Shortage = Math.Max(0, (bp.MinimumStock ?? 0) - bp.AvailableStock)
            })
            .Take(10)
            .ToListAsync();

        // --- Trends 12 months ---
        var revenueTrend = await BuildTrendAsync(branchId, "Invoices", "TotalAmount", twelveMonthsAgo, now);
        var expenseTrend = await BuildTrendAsync(branchId, "ExpenseVouchers", "Amount", twelveMonthsAgo, now);
        var refundTrend = await BuildRefundTrendAsync(branchId, twelveMonthsAgo, now);
        var customerGrowthTrend = await BuildCustomerGrowthTrendAsync(branchId, twelveMonthsAgo, now);

        // --- Expense by category ---
        var expenseByCategory = await _context.ExpenseVouchers
            .AsNoTracking()
            .Where(ev => ev.BranchId == branchId && (ev.Status == "COMPLETED" || ev.Status == "Completed") && ev.CreatedAt >= thirtyDaysAgo)
            .GroupBy(ev => ev.Category.CategoryName)
            .Select(g => new DashboardExpenseCategoryDto
            {
                CategoryName = g.Key,
                TotalAmount = g.Sum(ev => ev.Amount),
                VoucherCount = g.Count()
            })
            .OrderByDescending(x => x.TotalAmount)
            .Take(10)
            .ToListAsync();

        // --- Top categories by revenue ---
        var topCategories = await _context.InvoiceItems
            .AsNoTracking()
            .Where(ii => ii.Invoice.Order.BranchId == branchId && CompletedStatuses.Contains(ii.Invoice.Status) && ii.Invoice.CreatedAt >= thirtyDaysAgo)
            .GroupBy(ii => ii.BranchProduct.Product.CategoryName)
            .Select(g => new DashboardCategoryPerformanceDto
            {
                CategoryName = g.Key ?? string.Empty,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Quantity * x.SellPrice),
                Cost = g.Sum(x => x.Quantity * x.BranchProduct.CostPrice)
            })
            .ToListAsync();

        topCategories = topCategories.Select(x => { x.Profit = x.Revenue - x.Cost; return x; })
            .OrderByDescending(x => x.Revenue).Take(5).ToList();

        // --- Customer debt summary ---
        var activeCustomerIds = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status == 1)
            .Select(c => c.CustomerId)
            .ToListAsync();
        var allDebtMap = await _customerDebtService.CalculateCurrentDebtBatchAsync(activeCustomerIds);
        var activeCustomerTotalDebt = allDebtMap.Values.Sum();

        var overdueCustomers = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status == 1 && c.DebtLimit.HasValue && c.DebtLimit > 0)
            .Select(c => new { c.CustomerId, c.DebtLimit })
            .ToListAsync();
        decimal overdueDebtSum = 0m;
        int overdueCount = 0;
        foreach (var oc in overdueCustomers)
        {
            var debt = allDebtMap.TryGetValue(oc.CustomerId, out var dd) ? dd : 0m;
            if (debt > (oc.DebtLimit ?? 0m)) { overdueDebtSum += debt; overdueCount++; }
        }

        var customerDebtSummary = new DashboardDebtSummaryDto
        {
            CustomerCount = activeCustomerIds.Count(id => allDebtMap.TryGetValue(id, out var d) && d > 0),
            TotalDebt = activeCustomerTotalDebt,
            OverdueCustomerCount = overdueCount,
            OverdueDebt = overdueDebtSum
        };

        // --- Recent system events ---
        var recentEvents = await _context.SystemLogs
            .AsNoTracking()
            .Include(l => l.User)
            .OrderByDescending(l => l.Timestamp)
            .Take(20)
            .Select(l => new SystemEventDto
            {
                LogId = l.LogId,
                Action = l.Action,
                Description = l.Description,
                TableName = l.TableName,
                Level = l.Level,
                Source = l.Source,
                UserName = l.User != null ? l.User.FullName : null,
                IpAddress = l.IpAddress,
                Timestamp = l.Timestamp
            })
            .ToListAsync();

        // --- Supplier debt ---
        var totalSupplierDebt = 0m; // TODO: calculate from purchase orders vs payments

        var overview = new DashboardOverviewDto
        {
            GeneratedAt = now,
            BranchId = branch.BranchId,
            BranchName = branch.BranchName ?? string.Empty,
            BranchAddress = branch.Address,
            BranchManagerName = branch.ManagerUser?.FullName,

            // Today
            TotalRevenueToday = revToday,
            TotalOrdersToday = ordToday,
            NewCustomersToday = newCustomersToday,

            // 30 days
            TotalRevenueLast30Days = rev30,
            TotalOrdersLast30Days = ord30,
            TotalCostOfGoodsSoldLast30Days = cogs30,
            TotalExpenseLast30Days = exp30,
            TotalRefundAmountLast30Days = ref30,
            TotalProfitLast30Days = prof30,
            GrossMarginPercentLast30Days = gm30,
            AverageOrderValueLast30Days = aov30,
            AverageDailyRevenueLast30Days = Math.Round(rev30 / 30m, 2),
            NewCustomersLast30Days = newCustomersLast30Days,
            PaymentBreakdownLast30Days = pb30,

            // 7 days
            TotalRevenueLast7Days = rev7,
            TotalOrdersLast7Days = ord7,
            TotalCostOfGoodsSoldLast7Days = cogs7,
            TotalExpenseLast7Days = exp7,
            TotalRefundAmountLast7Days = ref7,
            TotalProfitLast7Days = prof7,
            GrossMarginPercentLast7Days = gm7,
            AverageOrderValueLast7Days = aov7,
            AverageDailyRevenueLast7Days = Math.Round(rev7 / 7m, 2),
            NewCustomersLast7Days = newCustomersLast7Days,
            PaymentBreakdownLast7Days = pb7,

            // 24 hours
            TotalRevenueLast24Hours = rev24,
            TotalOrdersLast24Hours = ord24,
            TotalCostOfGoodsSoldLast24Hours = cogs24,
            TotalRefundAmountLast24Hours = ref24,
            TotalProfitLast24Hours = prof24,
            GrossMarginPercentLast24Hours = gm24,
            AverageOrderValueLast24Hours = aov24,
            PaymentBreakdownLast24Hours = pb24,

            // YTD
            TotalRevenueYearToDate = revYtd,
            TotalOrdersYearToDate = ordYtd,
            TotalCostOfGoodsSoldYearToDate = cogsYtd,
            TotalExpenseYearToDate = expYtd,
            TotalRefundAmountYearToDate = refYtd,
            TotalProfitYearToDate = profYtd,
            GrossMarginPercentYearToDate = gmYtd,

            // Counts
            ActiveBranches = await _context.Branches.CountAsync(b => b.IsActive == 1),
            ActiveProducts = await _context.BranchProducts.CountAsync(bp => bp.BranchId == branchId && bp.IsActive == 1),
            LowStockProductCount = lowStockProducts.Count,
            ActiveCustomerCount = activeCustomerIds.Count,
            ActiveCustomerTotalDebt = activeCustomerTotalDebt,
            OverdueCustomerCount = overdueCount,
            OverdueCustomerTotalDebt = overdueDebtSum,
            PendingOrderCount = await _context.Orders.CountAsync(o => o.BranchId == branchId && o.Status == "PENDING" && o.CreatedAt >= thirtyDaysAgo),
            PendingOrderAmount = await _context.Orders.Where(o => o.BranchId == branchId && o.Status == "PENDING" && o.CreatedAt >= thirtyDaysAgo).SumAsync(o => (decimal?)o.TotalAmount) ?? 0m,
            PendingExpenseCount = await _context.ExpenseVouchers.CountAsync(ev => ev.BranchId == branchId && ev.Status == "PENDING" && ev.CreatedAt >= thirtyDaysAgo),
            PendingExpenseAmount = await _context.ExpenseVouchers.Where(ev => ev.BranchId == branchId && ev.Status == "PENDING" && ev.CreatedAt >= thirtyDaysAgo).SumAsync(ev => (decimal?)ev.Amount) ?? 0m,
            TotalSupplierDebt = totalSupplierDebt,
            SupplierDebtPaidLast30Days = await _context.SupplierPayments.Where(sp => sp.CreatedAt >= thirtyDaysAgo && sp.IsDeleted == 0).SumAsync(sp => (decimal?)sp.Amount) ?? 0m,
            RepaymentCoverageRatio = totalSupplierDebt > 0 ? Math.Round(activeCustomerTotalDebt / totalSupplierDebt * 100m, 2) : 0m,

            // Trends
            RevenueTrendLast12Months = revenueTrend,
            RevenueTrendLast30Days = new(),
            ExpenseTrendLast12Months = expenseTrend,
            RefundTrendLast12Months = refundTrend,
            CustomerGrowthTrendLast12Months = customerGrowthTrend,

            // Top lists
            TopProductsByRevenue = topProductsByRevenue,
            TopProductsByProfit = topProductsByProfit,
            TopCustomers = topCustomers,
            TopCategoriesByRevenue = topCategories,
            LowStockProducts = lowStockProducts,
            ExpenseByCategory = expenseByCategory,
            TopBranchesByRevenue = topBranches,
            RecentSystemEvents = recentEvents,

            CustomerDebtSummary = customerDebtSummary
        };

        return Ok(overview);
    }

    private async Task<(decimal revenue, int orderCount, decimal aov, decimal expense, decimal refund, decimal cogs, decimal profit, decimal grossMargin, DashboardPaymentBreakdownDto pb)> GetMetrics(Guid branchId, DateTime start, DateTime end)
    {
        var revenue = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.Order.BranchId == branchId && (i.Status == "COMPLETED" || i.Status == "Completed") && i.CreatedAt >= start && i.CreatedAt < end)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

        var orders = await _context.Orders
            .AsNoTracking()
            .CountAsync(o => o.BranchId == branchId && (o.Status == "COMPLETED" || o.Status == "Completed") && o.CreatedAt >= start && o.CreatedAt < end);

        var expense = await _context.ExpenseVouchers
            .AsNoTracking()
            .Where(ev => ev.BranchId == branchId && (ev.Status == "COMPLETED" || ev.Status == "Completed") && ev.CreatedAt >= start && ev.CreatedAt < end)
            .SumAsync(ev => (decimal?)ev.Amount) ?? 0m;

        var refund = await _context.ReturnOrders
            .AsNoTracking()
            .Where(ro => ro.Order.BranchId == branchId && (ro.Status == "COMPLETED" || ro.Status == "Completed") && ro.CreatedAt >= start && ro.CreatedAt < end)
            .SumAsync(ro => (decimal?)ro.RefundAmount) ?? 0m;

        var cogs = await _context.InvoiceItems
            .AsNoTracking()
            .Where(ii => ii.Invoice.Order.BranchId == branchId && CompletedStatuses.Contains(ii.Invoice.Status) && ii.Invoice.CreatedAt >= start && ii.Invoice.CreatedAt < end)
            .SumAsync(ii => (decimal?)ii.Quantity * ii.BranchProduct.CostPrice) ?? 0m;

        var paymentRaw = await _context.Payments
            .AsNoTracking()
            .Where(p => p.BranchId == branchId && p.Status == "Confirmed" && p.CreatedAt >= start && p.CreatedAt < end)
            .GroupBy(p => p.Method)
            .Select(g => new { g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync();

        var pb = new DashboardPaymentBreakdownDto();
        foreach (var item in paymentRaw)
        {
            if (item.Key == "Cash") { pb.CashAmount = item.Amount; pb.CashCount = item.Count; }
            else if (item.Key == "Transfer") { pb.TransferAmount = item.Amount; pb.TransferCount = item.Count; }
            else if (item.Key == "Combined") { pb.CombinedAmount = item.Amount; pb.CombinedCount = item.Count; }
            else if (item.Key == "Debt") { pb.DebtAmount = item.Amount; pb.DebtCount = item.Count; }
        }

        var profit = revenue - cogs - expense - refund;
        var grossMargin = revenue != 0 ? Math.Round((revenue - cogs) / revenue * 100m, 2) : 0m;
        var aov = orders > 0 ? Math.Round(revenue / orders, 2) : 0m;

        return (revenue, orders, aov, expense, refund, cogs, profit, grossMargin, pb);
    }

    private static List<DashboardTrendPointDto> PadTrend(DateTime from, Func<int, int, decimal> getAmount)
    {
        return Enumerable.Range(0, 12).Select(offset => from.AddMonths(offset)).Select(date =>
        {
            var amount = getAmount(date.Year, date.Month);
            return new DashboardTrendPointDto { Period = date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), Amount = amount, Count = 0 };
        }).ToList();
    }

    private async Task<List<DashboardTrendPointDto>> BuildTrendAsync(Guid branchId, string table, string amountCol, DateTime from, DateTime to)
    {
        if (table == "Invoices")
        {
            var data = await _context.Invoices.AsNoTracking()
                .Where(i => i.Order.BranchId == branchId && (i.Status == "COMPLETED" || i.Status == "Completed") && i.CreatedAt >= from && i.CreatedAt < to)
                .Select(i => new { i.CreatedAt, i.TotalAmount })
                .ToListAsync();
            var grouped = data.Where(x => x.CreatedAt.HasValue).GroupBy(x => new { x.CreatedAt!.Value.Year, x.CreatedAt!.Value.Month })
                .ToDictionary(g => new { g.Key.Year, g.Key.Month }, g => g.Sum(x => x.TotalAmount));
            return PadTrend(from, (y, m) => grouped.TryGetValue(new { Year = y, Month = m }, out var v) ? v : 0m);
        }
        else
        {
            var data = await _context.ExpenseVouchers.AsNoTracking()
                .Where(ev => ev.BranchId == branchId && (ev.Status == "COMPLETED" || ev.Status == "Completed") && ev.CreatedAt >= from && ev.CreatedAt < to)
                .Select(ev => new { ev.CreatedAt, Amount = ev.Amount })
                .ToListAsync();
            var grouped = data.Where(x => x.CreatedAt.HasValue).GroupBy(x => new { x.CreatedAt!.Value.Year, x.CreatedAt!.Value.Month })
                .ToDictionary(g => new { g.Key.Year, g.Key.Month }, g => g.Sum(x => x.Amount));
            return PadTrend(from, (y, m) => grouped.TryGetValue(new { Year = y, Month = m }, out var v) ? v : 0m);
        }
    }

    private async Task<List<DashboardTrendPointDto>> BuildRefundTrendAsync(Guid branchId, DateTime from, DateTime to)
    {
        var data = await _context.ReturnOrders.AsNoTracking()
            .Where(ro => ro.Order.BranchId == branchId && (ro.Status == "COMPLETED" || ro.Status == "Completed") && ro.CreatedAt >= from && ro.CreatedAt < to)
            .Select(ro => new { ro.CreatedAt, ro.RefundAmount })
            .ToListAsync();
        var grouped = data.Where(x => x.CreatedAt.HasValue).GroupBy(x => new { x.CreatedAt!.Value.Year, x.CreatedAt!.Value.Month })
            .ToDictionary(g => new { g.Key.Year, g.Key.Month }, g => g.Sum(x => x.RefundAmount));
        return PadTrend(from, (y, m) => grouped.TryGetValue(new { Year = y, Month = m }, out var v) ? v : 0m);
    }

    private async Task<List<DashboardTrendPointDto>> BuildCustomerGrowthTrendAsync(Guid branchId, DateTime from, DateTime to)
    {
        var data = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status == 1 && c.CreatedAt >= from && c.CreatedAt < to)
            .GroupBy(c => new { c.CreatedAt!.Value.Year, c.CreatedAt!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();
        return Enumerable.Range(0, 12).Select(offset => from.AddMonths(offset)).Select(date =>
        {
            var count = data.FirstOrDefault(c => c.Year == date.Year && c.Month == date.Month)?.Count ?? 0;
            return new DashboardTrendPointDto { Period = date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), Amount = count, Count = count };
        }).ToList();
    }

    private static List<DashboardTrendPointDto> PadTrend(DateTime from, Dictionary<object, decimal> grouped)
    {
        return Enumerable.Range(0, 12).Select(offset => from.AddMonths(offset)).Select(date =>
        {
            var key = new { date.Year, date.Month };
            grouped.TryGetValue(key, out var amount);
            return new DashboardTrendPointDto { Period = date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), Amount = amount, Count = 0 };
        }).ToList();
    }


    /// <summary>
    /// GET /api/admin/dashboard/revenue-chart?year=2026
    /// Returns: 12-month array of { month, revenue, orderCount }
    /// </summary>
    [HttpGet("revenue-chart")]
    public async Task<IActionResult> GetRevenueChart([FromQuery] int year = 2026)
    {
        var startOfYear = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var endOfYear = startOfYear.AddYears(1);

        var chartData = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.Status == "COMPLETED"
                        && i.CreatedAt.HasValue
                        && i.CreatedAt >= startOfYear
                        && i.CreatedAt < endOfYear)
            .GroupBy(i => i.CreatedAt!.Value.Month)
            .Select(g => new RevenueChartPointDto
            {
                Month = g.Key,
                Revenue = g.Sum(i => i.TotalAmount),
                OrderCount = g.Count()
            })
            .ToListAsync();

        // Pad missing months with zero entries
        var result = Enumerable.Range(1, 12)
            .Select(month => chartData.FirstOrDefault(c => c.Month == month)
                             ?? new RevenueChartPointDto { Month = month, Revenue = 0, OrderCount = 0 })
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/dashboard/recent-events?limit=20
    /// Returns: top system logs as SystemEventDto
    /// </summary>
    [HttpGet("recent-events")]
    public async Task<IActionResult> GetRecentEvents([FromQuery] int limit = 20)
    {
        if (limit <= 0) limit = 20;
        if (limit > 100) limit = 100;

        var events = await _context.SystemLogs
            .AsNoTracking()
            .Include(l => l.User)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .Select(l => new SystemEventDto
            {
                LogId = l.LogId,
                Action = l.Action,
                Description = l.Description,
                TableName = l.TableName,
                Level = l.Level,
                Source = l.Source,
                UserName = l.User != null ? l.User.FullName : null,
                IpAddress = l.IpAddress,
                Timestamp = l.Timestamp
            })
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>
    /// GET /api/admin/dashboard/export
    /// Returns: CSV file download with dashboard summary
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string format = "csv")
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var thirtyDaysAgo = now.AddDays(-30);

        var activeTenants = await _context.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .Where(u => u.Roles.Any(r => r.RoleName == "Owner") && u.IsActive == 1)
            .CountAsync();

        var subscriptionRevenue = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.Status == "COMPLETED" && i.CreatedAt >= thirtyDaysAgo)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

        var b2bCommission = Math.Round(
            (await _context.Orders
            .AsNoTracking()
            .Where(o => o.PaymentMethod == "TRANSFER")
                .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m) * 0.01m, 2);

        var overdueAlerts = await _context.Customers
            .AsNoTracking()
            .Where(c => c.Status == 1 && c.DebtLimit.HasValue)
            .CountAsync(c => c.CurrentDebt > c.DebtLimit);

        var recentLogs = await _context.SystemLogs
            .AsNoTracking()
            .Include(l => l.User)
            .OrderByDescending(l => l.Timestamp)
            .Take(100)
            .Select(l => new
            {
                l.Action,
                l.Description,
                l.Level,
                l.Source,
                UserName = l.User != null ? l.User.FullName : "System",
                l.IpAddress,
                l.Timestamp
            })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("M.E.P Admin Dashboard Export");
        sb.AppendLine($"Generated:,{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("=== SUMMARY ===");
        sb.AppendLine($"Active Tenants,{activeTenants}");
        sb.AppendLine($"Subscription Revenue (30d),{subscriptionRevenue:N0}");
        sb.AppendLine($"B2B Commission (1%),{b2bCommission:N0}");
        sb.AppendLine($"Overdue Alerts,{overdueAlerts}");
        sb.AppendLine();
        sb.AppendLine("=== RECENT EVENTS ===");
        sb.AppendLine("Timestamp,Action,Description,Level,Source,User,IP Address");
        foreach (var log in recentLogs)
        {
            sb.AppendLine($"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{log.Action}\",\"{EscapeCsv(log.Description)}\",\"{log.Level}\",\"{log.Source}\",\"{EscapeCsv(log.UserName)}\",\"{log.IpAddress}\"");
        }

        if (format.ToLower() == "excel")
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Dashboard Summary");

            // Header for Summary
            worksheet.Cell(1, 1).Value = "M.E.P Admin Dashboard Export";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 14;
            worksheet.Range(1, 1, 1, 4).Merge();

            worksheet.Cell(2, 1).Value = "Generated:";
            worksheet.Cell(2, 2).Value = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";

            // Summary Table
            worksheet.Cell(4, 1).Value = "=== SUMMARY ===";
            worksheet.Cell(4, 1).Style.Font.Bold = true;

            worksheet.Cell(5, 1).Value = "Active Tenants";
            worksheet.Cell(5, 2).Value = activeTenants;
            
            worksheet.Cell(6, 1).Value = "Subscription Revenue (30d)";
            worksheet.Cell(6, 2).Value = subscriptionRevenue;
            worksheet.Cell(6, 2).Style.NumberFormat.Format = "#,##0";

            worksheet.Cell(7, 1).Value = "B2B Commission (1%)";
            worksheet.Cell(7, 2).Value = b2bCommission;
            worksheet.Cell(7, 2).Style.NumberFormat.Format = "#,##0";

            worksheet.Cell(8, 1).Value = "Overdue Alerts";
            worksheet.Cell(8, 2).Value = overdueAlerts;

            // Recent Events
            worksheet.Cell(10, 1).Value = "=== RECENT EVENTS ===";
            worksheet.Cell(10, 1).Style.Font.Bold = true;

            string[] headers = { "Timestamp", "Action", "Description", "Level", "Source", "User", "IP Address" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(11, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            int row = 12;
            foreach (var log in recentLogs)
            {
                worksheet.Cell(row, 1).Value = $"{log.Timestamp:yyyy-MM-dd HH:mm:ss}";
                worksheet.Cell(row, 2).Value = log.Action;
                worksheet.Cell(row, 3).Value = log.Description;
                worksheet.Cell(row, 4).Value = log.Level;
                worksheet.Cell(row, 5).Value = log.Source;
                worksheet.Cell(row, 6).Value = log.UserName;
                worksheet.Cell(row, 7).Value = log.IpAddress;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"mep-dashboard-{now:yyyyMMdd}.xlsx");
        }
        else
        {
            var contentBytes = Encoding.UTF8.GetBytes(sb.ToString());
            var bom = Encoding.UTF8.GetPreamble();
            var result = bom.Concat(contentBytes).ToArray();
            return File(result, "text/csv", $"mep-dashboard-{now:yyyyMMdd}.csv");
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var dblQuote = "\"";
        var escaped = value.Replace(dblQuote, dblQuote + dblQuote);
        // Prevent CSV formula injection
        if (escaped.Length > 0 && (escaped[0] == '=' || escaped[0] == '+' ||
                                   escaped[0] == '-' || escaped[0] == '@' ||
                                   escaped[0] == '\t'))
            return "\"" + escaped + "\"";
        return escaped.Length != value.Length ? "\"" + escaped + "\"" : escaped;
    }
}
