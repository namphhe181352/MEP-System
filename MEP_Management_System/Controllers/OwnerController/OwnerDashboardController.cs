using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.OwnerController;

[ApiController]
[Route("api/owner/dashboard")]
[Authorize(Roles = "Owner")]
[Tags("Owner Dashboard")]
public class OwnerDashboardController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly ICurrentBranchAccessor _currentBranch;
    private readonly CustomerDebtService _customerDebtService;

    public OwnerDashboardController(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch, CustomerDebtService customerDebtService)
    {
        _context = context;
        _currentBranch = currentBranch;
        _customerDebtService = customerDebtService;
    }

    /// <summary>
    /// GET /api/owner/dashboard/overview
    /// Returns the owner dashboard overview for the current default branch.
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var branchId = _currentBranch.GetBranchId();
        if (branchId == null)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy chi nhánh mặc định của Owner.",
                Errors = new() { "Default branch is required for owner dashboard." }
            });
        }

        var branch = await _context.Branches
            .AsNoTracking()
            .Include(b => b.ManagerUser)
            .FirstOrDefaultAsync(b => b.BranchId == branchId.Value && b.IsActive == 1);
        if (branch == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy chi nhánh.",
                Errors = new() { "Branch does not exist or is inactive." }
            });
        }

        var now = VietnamTime.Now;
        var last30Days = now.AddDays(-30);
        var last7Days = now.AddDays(-7);
        var last24Hours = now.AddDays(-1);
        var yearStart = new DateTime(VietnamTime.Now.Year, 1, 1);
        var twelveMonthsAgo = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

        var branchCount = await _context.Branches
            .AsNoTracking()
            .Where(b => b.StaffAssignments.Any(sa => sa.UserId == GetCurrentUserId()) && b.IsActive == 1)
            .CountAsync();

        var revenueLast30Days = await GetBranchMetricsAsync(branchId.Value, last30Days, now);
        var revenueLast7Days = await GetBranchMetricsAsync(branchId.Value, last7Days, now);
        var revenueLast24Hours = await GetBranchMetricsAsync(branchId.Value, last24Hours, now);
        var revenueYearToDate = await GetBranchMetricsAsync(branchId.Value, yearStart, now);

        var activeProducts = await _context.BranchProducts.AsNoTracking()
            .Where(bp => bp.BranchId == branchId.Value && bp.IsActive == 1)
            .CountAsync();

        var lowStockProductCount = await _context.BranchProducts.AsNoTracking()
            .Where(bp => bp.BranchId == branchId.Value && bp.IsActive == 1 && bp.MinimumStock.HasValue && bp.AvailableStock <= bp.MinimumStock)
            .CountAsync();

        var activeCustomerCount = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId.Value && c.Status == 1)
            .CountAsync();

        var activeCustomerIds = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId.Value && c.Status == 1)
            .Select(c => c.CustomerId)
            .ToListAsync();
        var debtMap = await _customerDebtService.CalculateCurrentDebtBatchAsync(activeCustomerIds);
        var activeCustomerTotalDebt = debtMap.Values.Sum();

        var overdueCustomers = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId.Value && c.Status == 1 && c.DebtLimit.HasValue && c.DebtLimit > 0)
            .Select(c => new { c.CustomerId, c.DebtLimit })
            .ToListAsync();

        var overdueDebtSum = 0m;
        var overdueCount = 0;
        foreach (var oc in overdueCustomers)
        {
            var debt = debtMap.TryGetValue(oc.CustomerId, out var d) ? d : 0m;
            if (debt > (oc.DebtLimit ?? 0m))
            {
                overdueDebtSum += debt;
                overdueCount++;
            }
        }

        var customerDebtSummary = new DashboardDebtSummaryDto
        {
            CustomerCount = activeCustomerCount,
            TotalDebt = activeCustomerTotalDebt,
            OverdueCustomerCount = overdueCount,
            OverdueDebt = overdueDebtSum
        };

        var topProductsByRevenue = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order != null && oi.Order.BranchId == branchId.Value
                         && (oi.Order.Status == "COMPLETED" || oi.Order.Status == "Completed")
                         && oi.Order.CreatedAt.HasValue && oi.Order.CreatedAt >= last30Days && oi.Order.CreatedAt < now)
            .GroupBy(oi => new
            {
                oi.BranchProduct.ProductId,
                oi.BranchProduct.Product.ProductCode,
                oi.BranchProduct.Product.ProductName,
                oi.BranchProduct.Product.CategoryName
            })
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

        topProductsByRevenue = topProductsByRevenue
            .Select(x =>
            {
                x.Profit = x.Revenue - x.Cost;
                x.ProfitMarginPercent = x.Revenue != 0 ? Math.Round(x.Profit / x.Revenue * 100m, 2) : 0m;
                return x;
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToList();

        var topProductsByProfit = topProductsByRevenue
            .OrderByDescending(x => x.Profit)
            .Take(5)
            .ToList();

        var topCustomers = await _context.Orders
            .AsNoTracking()
            .Where(o => o.BranchId == branchId.Value && o.CustomerId != null
                        && (o.Status == "COMPLETED" || o.Status == "Completed")
                        && o.CreatedAt.HasValue && o.CreatedAt >= last30Days && o.CreatedAt < now)
            .GroupBy(o => new { o.CustomerId, o.Customer!.CustomerName, o.Customer!.PhoneNumber, o.Customer!.Email })
            .Select(g => new DashboardCustomerPerformanceDto
            {
                CustomerId = g.Key.CustomerId!.Value,
                CustomerName = g.Key.CustomerName,
                PhoneNumber = g.Key.PhoneNumber,
                Email = g.Key.Email,
                OrderCount = g.Count(),
                TotalSpent = g.Sum(o => o.TotalAmount),
                TotalDebt = 0m
            })
            .OrderByDescending(x => x.TotalSpent)
            .Take(5)
            .ToListAsync();

        foreach (var tc in topCustomers)
        {
            tc.TotalDebt = debtMap.TryGetValue(tc.CustomerId, out var d) ? d : 0m;
        }

        var expenseByCategory = await _context.ExpenseVouchers
            .AsNoTracking()
            .Where(ev => ev.BranchId == branchId.Value
                         && (ev.Status == "COMPLETED" || ev.Status == "Completed")
                         && ev.CreatedAt.HasValue && ev.CreatedAt >= last30Days && ev.CreatedAt < now)
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

        var paymentsBreakdown = await _context.Payments
            .AsNoTracking()
            .Where(p => p.BranchId == branchId.Value && p.Status == "Confirmed"
                        && p.PaidAt.HasValue && p.PaidAt >= last30Days && p.PaidAt < now)
            .GroupBy(p => p.Method)
            .Select(g => new
            {
                g.Key,
                Amount = g.Sum(p => p.Amount),
                Count = g.Count()
            })
            .ToListAsync();

        var paymentBreakdownLast30Days = new DashboardPaymentBreakdownDto();
        foreach (var item in paymentsBreakdown)
        {
            if (item.Key == "Cash")
            {
                paymentBreakdownLast30Days.CashAmount = item.Amount;
                paymentBreakdownLast30Days.CashCount = item.Count;
            }
            else if (item.Key == "Transfer")
            {
                paymentBreakdownLast30Days.TransferAmount = item.Amount;
                paymentBreakdownLast30Days.TransferCount = item.Count;
            }
            else if (item.Key == "Combined")
            {
                paymentBreakdownLast30Days.CombinedAmount = item.Amount;
                paymentBreakdownLast30Days.CombinedCount = item.Count;
            }
            else if (item.Key == "Debt")
            {
                paymentBreakdownLast30Days.DebtAmount = item.Amount;
                paymentBreakdownLast30Days.DebtCount = item.Count;
            }
        }

        var branchRevenueTrend = await BuildTrendAsync(_context.Invoices.AsNoTracking()
            .Where(i => i.Order.BranchId == branchId.Value
                        && i.CreatedAt.HasValue && i.CreatedAt >= twelveMonthsAgo && i.CreatedAt < now),
            "CreatedAt", "TotalAmount", twelveMonthsAgo, now, 12);

        var branchExpenseTrend = await BuildTrendAsync(_context.ExpenseVouchers.AsNoTracking()
            .Where(ev => ev.BranchId == branchId.Value
                         && (ev.Status == "COMPLETED" || ev.Status == "Completed")
                         && ev.CreatedAt.HasValue && ev.CreatedAt >= twelveMonthsAgo && ev.CreatedAt < now),
            "CreatedAt", "Amount", twelveMonthsAgo, now, 12);

        var revenueToday = await GetBranchMetricsAsync(branchId.Value, VietnamTime.TodayStart, VietnamTime.TodayEnd);
        var newCustomerCountToday = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId.Value && c.Status == 1 && c.CreatedAt.HasValue && c.CreatedAt >= now.Date && c.CreatedAt < now)
            .CountAsync();

        var newCustomerCountLast7Days = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId.Value && c.Status == 1 && c.CreatedAt.HasValue && c.CreatedAt >= last7Days && c.CreatedAt < now)
            .CountAsync();

        var newCustomerCountLast30Days = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId.Value && c.Status == 1 && c.CreatedAt.HasValue && c.CreatedAt >= last30Days && c.CreatedAt < now)
            .CountAsync();

        var pendingOrderCount = await _context.Orders.AsNoTracking()
            .Where(o => o.BranchId == branchId.Value
                        && o.Status == "Pending"
                        && o.CreatedAt.HasValue && o.CreatedAt >= last30Days && o.CreatedAt < now)
            .CountAsync();

        var totalSupplierDebt = 0m; // supplier debt summary not directly available per branch in current data model

        var lowStockProducts = await _context.BranchProducts.AsNoTracking()
            .Where(bp => bp.BranchId == branchId.Value && bp.IsActive == 1
                         && bp.MinimumStock.HasValue && bp.AvailableStock <= bp.MinimumStock)
            .OrderBy(bp => bp.AvailableStock)
            .Select(bp => new DashboardLowStockProductDto
            {
                ProductId = bp.ProductId,
                ProductCode = bp.Product.ProductCode,
                ProductName = bp.Product.ProductName,
                CategoryName = bp.Product.CategoryName ?? string.Empty,
                AvailableStock = bp.AvailableStock,
                MinimumStock = bp.MinimumStock ?? 0m,
                Shortage = Math.Max(0, (bp.MinimumStock ?? 0m) - bp.AvailableStock)
            })
            .Take(10)
            .ToListAsync();

        var categoryPerformance = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order != null && oi.Order.BranchId == branchId.Value
                         && (oi.Order.Status == "COMPLETED" || oi.Order.Status == "Completed")
                         && oi.Order.CreatedAt.HasValue && oi.Order.CreatedAt >= last30Days && oi.Order.CreatedAt < now)
            .GroupBy(oi => oi.BranchProduct.Product.CategoryName)
            .Select(g => new DashboardCategoryPerformanceDto
            {
                CategoryName = g.Key ?? string.Empty,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Quantity * x.SellPrice),
                Cost = g.Sum(x => x.Quantity * x.BranchProduct.CostPrice)
            })
            .ToListAsync();

        categoryPerformance = categoryPerformance
            .Select(x =>
            {
                x.Profit = x.Revenue - x.Cost;
                return x;
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToList();

        var customerGrowthTrend = await BuildCustomerGrowthTrendAsync(branchId.Value, twelveMonthsAgo, now);

        return Ok(new DashboardOverviewDto
        {
            GeneratedAt = now,
            BranchId = branch.BranchId,
            BranchName = branch.BranchName ?? string.Empty,
            BranchAddress = branch.Address,
            BranchManagerName = branch.ManagerUser?.FullName,
            TotalRevenueToday = revenueToday.revenue,
            TotalOrdersToday = revenueToday.orderCount,
            TotalRevenueLast30Days = revenueLast30Days.revenue,
            TotalRevenueLast7Days = revenueLast7Days.revenue,
            TotalRevenueLast24Hours = revenueLast24Hours.revenue,
            TotalRevenueYearToDate = revenueYearToDate.revenue,
            TotalOrdersLast30Days = revenueLast30Days.orderCount,
            TotalOrdersLast7Days = revenueLast7Days.orderCount,
            TotalOrdersLast24Hours = revenueLast24Hours.orderCount,
            TotalOrdersYearToDate = revenueYearToDate.orderCount,
            AverageOrderValueLast30Days = revenueLast30Days.averageOrderValue,
            AverageOrderValueLast7Days = revenueLast7Days.averageOrderValue,
            AverageOrderValueLast24Hours = revenueLast24Hours.averageOrderValue,
            NewCustomersLast30Days = newCustomerCountLast30Days,
            NewCustomersLast7Days = newCustomerCountLast7Days,
            NewCustomersToday = newCustomerCountToday,
            TotalExpenseLast30Days = revenueLast30Days.expense,
            TotalExpenseLast7Days = revenueLast7Days.expense,
            TotalExpenseYearToDate = revenueYearToDate.expense,
            TotalRefundAmountLast30Days = revenueLast30Days.refund,
            TotalRefundAmountLast7Days = revenueLast7Days.refund,
            TotalRefundAmountLast24Hours = revenueToday.refund,
            TotalRefundAmountYearToDate = revenueYearToDate.refund,
            TotalCostOfGoodsSoldLast30Days = revenueLast30Days.cogs,
            TotalCostOfGoodsSoldLast7Days = revenueLast7Days.cogs,
            TotalCostOfGoodsSoldLast24Hours = revenueToday.cogs,
            TotalCostOfGoodsSoldYearToDate = revenueYearToDate.cogs,
            TotalProfitLast30Days = revenueLast30Days.profit,
            TotalProfitLast7Days = revenueLast7Days.profit,
            TotalProfitLast24Hours = revenueToday.profit,
            TotalProfitYearToDate = revenueYearToDate.profit,
            GrossMarginPercentLast30Days = revenueLast30Days.grossMargin,
            GrossMarginPercentLast7Days = revenueLast7Days.grossMargin,
            GrossMarginPercentLast24Hours = revenueToday.grossMargin,
            GrossMarginPercentYearToDate = revenueYearToDate.grossMargin,
            PaymentBreakdownLast30Days = paymentBreakdownLast30Days,
            PaymentBreakdownLast7Days = revenueLast7Days.paymentBreakdown,
            PaymentBreakdownLast24Hours = revenueToday.paymentBreakdown,
            CustomerDebtSummary = customerDebtSummary,
            ActiveBranches = branchCount,
            ActiveProducts = activeProducts,
            LowStockProductCount = lowStockProductCount,
            ActiveCustomerCount = activeCustomerCount,
            ActiveCustomerTotalDebt = activeCustomerTotalDebt,
            OverdueCustomerCount = customerDebtSummary.OverdueCustomerCount,
            OverdueCustomerTotalDebt = customerDebtSummary.OverdueDebt,
            AverageDailyRevenueLast7Days = Math.Round(revenueLast7Days.revenue / 7m, 2),
            AverageDailyRevenueLast30Days = Math.Round(revenueLast30Days.revenue / 30m, 2),
            PendingExpenseCount = await _context.ExpenseVouchers.AsNoTracking()
                .Where(ev => ev.BranchId == branchId.Value && ev.Status == "Pending" && ev.CreatedAt.HasValue && ev.CreatedAt >= last30Days && ev.CreatedAt < now)
                .CountAsync(),
            PendingExpenseAmount = await _context.ExpenseVouchers.AsNoTracking()
                .Where(ev => ev.BranchId == branchId.Value && ev.Status == "Pending" && ev.CreatedAt.HasValue && ev.CreatedAt >= last30Days && ev.CreatedAt < now)
                .SumAsync(ev => (decimal?)ev.Amount) ?? 0m,
            PendingOrderCount = pendingOrderCount,
            TotalSupplierDebt = totalSupplierDebt,
            RepaymentCoverageRatio = 0m,
            RevenueTrendLast12Months = branchRevenueTrend,
            ExpenseTrendLast12Months = branchExpenseTrend,
            RefundTrendLast12Months = new List<DashboardTrendPointDto>(),
            CustomerGrowthTrendLast12Months = customerGrowthTrend,
            TopProductsByRevenue = topProductsByRevenue,
            TopProductsByProfit = topProductsByProfit,
            TopCustomers = topCustomers,
            TopCategoriesByRevenue = categoryPerformance,
            LowStockProducts = lowStockProducts,
            ExpenseByCategory = expenseByCategory,
            TopBranchesByRevenue = new List<DashboardBranchRevenueDto>
            {
                new() { BranchId = branch.BranchId, BranchName = branch.BranchName ?? string.Empty, Revenue = revenueLast30Days.revenue, OrderCount = revenueLast30Days.orderCount }
            },
            RecentSystemEvents = new List<SystemEventDto>()
        });

        async Task<List<DashboardTrendPointDto>> BuildCustomerGrowthTrendAsync(Guid branch, DateTime from, DateTime to)
        {
            var customers = await _context.Customers.AsNoTracking()
                .Where(c => c.BranchId == branch && c.Status == 1 && c.CreatedAt.HasValue && c.CreatedAt >= from && c.CreatedAt < to)
                .GroupBy(c => new { c.CreatedAt!.Value.Year, c.CreatedAt!.Value.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
                .ToListAsync();

            return Enumerable.Range(0, 12)
                .Select(offset => from.AddMonths(offset))
                .Select(date =>
                {
                    var count = customers.FirstOrDefault(c => c.Year == date.Year && c.Month == date.Month)?.Count ?? 0;
                    return new DashboardTrendPointDto
                    {
                        Period = date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
                        Amount = count,
                        Count = count
                    };
                })
                .ToList();
        }


        async Task<(decimal revenue, int orderCount, decimal averageOrderValue, decimal expense, decimal refund, decimal cogs, decimal profit, decimal grossMargin, DashboardPaymentBreakdownDto paymentBreakdown)> GetBranchMetricsAsync(Guid branch, DateTime start, DateTime end)
        {
            var revenue = await _context.Invoices
                .AsNoTracking()
                .Where(i => i.Order.BranchId == branch
                            && i.Status == "COMPLETED"
                            && i.Payments.Any(p => p.Status == "Confirmed" && p.PaidAt.HasValue && p.PaidAt >= start && p.PaidAt < end))
                .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

            var orderCount = await _context.Orders
                .AsNoTracking()
                .Where(o => o.BranchId == branch
                            && o.Status == "COMPLETED"
                            && o.CreatedAt.HasValue && o.CreatedAt >= start && o.CreatedAt < end)
                .CountAsync();

            var expense = await _context.ExpenseVouchers
                .AsNoTracking()
                .Where(ev => ev.BranchId == branch
                             && ev.Status == "COMPLETED"
                             && ev.CreatedAt.HasValue && ev.CreatedAt >= start && ev.CreatedAt < end)
                .SumAsync(ev => (decimal?)ev.Amount) ?? 0m;

            var refund = await _context.ReturnOrders
                .AsNoTracking()
                .Where(ro => ro.Order.BranchId == branch
                             && ro.Status == "COMPLETED"
                             && ro.CreatedAt.HasValue && ro.CreatedAt >= start && ro.CreatedAt < end)
                .SumAsync(ro => (decimal?)ro.RefundAmount) ?? 0m;

            var cogs = await _context.OrderItems
                .AsNoTracking()
                .Where(oi => oi.Order.BranchId == branch
                             && oi.Order.Invoice.Status == "COMPLETED"
                             && oi.Order.Invoice.Payments.Any(p => p.Status == "Confirmed" && p.PaidAt.HasValue && p.PaidAt >= start && p.PaidAt < end))
                .SumAsync(oi => (decimal?)oi.Quantity * oi.BranchProduct.CostPrice) ?? 0m;

            var paymentBreakdown = await _context.Payments
                .AsNoTracking()
                .Where(p => p.BranchId == branch && p.Status == "Confirmed"
                            && p.PaidAt.HasValue && p.PaidAt >= start && p.PaidAt < end)
                .GroupBy(p => p.Method)
                .Select(g => new
                {
                    g.Key,
                    Amount = g.Sum(p => p.Amount),
                    Count = g.Count()
                })
                .ToListAsync();

            var breakdown = new DashboardPaymentBreakdownDto();
            foreach (var item in paymentBreakdown)
            {
                if (item.Key == "Cash")
                {
                    breakdown.CashAmount = item.Amount;
                    breakdown.CashCount = item.Count;
                }
                else if (item.Key == "Transfer")
                {
                    breakdown.TransferAmount = item.Amount;
                    breakdown.TransferCount = item.Count;
                }
                else if (item.Key == "Combined")
                {
                    breakdown.CombinedAmount = item.Amount;
                    breakdown.CombinedCount = item.Count;
                }
                else if (item.Key == "Debt")
                {
                    breakdown.DebtAmount = item.Amount;
                    breakdown.DebtCount = item.Count;
                }
            }

            var profit = revenue - cogs - expense - refund;
            var grossMargin = revenue != 0 ? Math.Round((revenue - cogs) / revenue * 100m, 2) : 0m;
            var averageOrderValue = orderCount > 0 ? Math.Round(revenue / orderCount, 2) : 0m;
            return (revenue, orderCount, averageOrderValue, expense, refund, cogs, profit, grossMargin, breakdown);
        }

        async Task<List<DashboardTrendPointDto>> BuildTrendAsync<T>(IQueryable<T> source, string createdAtProperty, string amountProperty, DateTime from, DateTime to, int months)
            where T : class
        {
            var items = await source
                .Select(item => new
                {
                    CreatedAt = EF.Property<DateTime?>(item, createdAtProperty),
                    Amount = EF.Property<decimal>(item, amountProperty)
                })
                .ToListAsync();

            var grouped = items
                .Where(x => x.CreatedAt.HasValue)
                .GroupBy(x => new { Year = x.CreatedAt!.Value.Year, Month = x.CreatedAt!.Value.Month })
                .ToDictionary(g => new { g.Key.Year, g.Key.Month }, g => g.Sum(x => x.Amount));

            return Enumerable.Range(0, months)
                .Select(offset => from.AddMonths(offset))
                .Select(date =>
                {
                    var periodKey = new { date.Year, date.Month };
                    grouped.TryGetValue(periodKey, out var amount);
                    return new DashboardTrendPointDto
                    {
                        Period = date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
                        Amount = amount,
                        Count = 0
                    };
                })
                .ToList();
        }
    }
        /// <summary>
    /// GET /api/owner/dashboard/customer-debt
    /// List customer debts with pagination, filter by isOverdue, sort by CurrentDebt DESC.
    /// </summary>
    [HttpGet("customer-debt")]
    public async Task<IActionResult> GetCustomerDebtList(
        [FromQuery] bool? isOverdue,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var branchId = _currentBranch.GetBranchId();
        if (branchId == null) return Unauthorized(new ApiResponse<object> { Success = false, Message = "Branch required." });

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId.Value && c.Status == 1);

        if (isOverdue == true)
            query = query.Where(c => c.DebtLimit.HasValue && c.CurrentDebt.HasValue && c.CurrentDebt > c.DebtLimit);
        else if (isOverdue == false)
            query = query.Where(c => !c.DebtLimit.HasValue || !c.CurrentDebt.HasValue || c.CurrentDebt <= c.DebtLimit);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(c =>
                (c.CustomerName != null && c.CustomerName.Contains(s)) ||
                (c.PhoneNumber != null && c.PhoneNumber.Contains(s)));
        }

        var total = await query.CountAsync();

        var customers = await query
            .OrderByDescending(c => c.CurrentDebt ?? 0m)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = new List<MEP_Management_System.DTOs.CustomerDebtListItemDto>();
        foreach (var c in customers)
        {
            var debt = await _customerDebtService.CalculateCurrentDebtAsync(c.CustomerId);
            var lastOrder = await _context.Orders
                .Where(o => o.CustomerId == c.CustomerId)
                .MaxAsync(o => (DateTime?)o.CreatedAt);
            var lastPayment = await _context.Payments
                .Where(p => p.Invoice != null && p.Invoice.Order.CustomerId == c.CustomerId && p.Status == "Confirmed")
                .MaxAsync(p => (DateTime?)p.CreatedAt);

            items.Add(new MEP_Management_System.DTOs.CustomerDebtListItemDto
            {
                CustomerId = c.CustomerId,
                FullName = c.CustomerName ?? "",
                Phone = c.PhoneNumber,
                CurrentDebt = debt.CurrentDebt,
                DebtLimit = c.DebtLimit ?? 0m,
                LastOrderDate = lastOrder,
                LastPaymentDate = lastPayment,
                IsOverdue = c.DebtLimit.HasValue && debt.CurrentDebt > c.DebtLimit.Value
            });
        }

        return Ok(new
        {
            success = true,
            data = items,
            pagination = new
            {
                page,
                pageSize,
                total,
                totalPages = (int)Math.Ceiling(total / (double)pageSize)
            }
        });
    }

    /// <summary>
    /// GET /api/owner/dashboard/customer-debt/overview
    /// Returns 6-field overview: TotalDebt, OverdueDebt, AverageDebtPerCustomer, OverdueCount, ActiveCount, TotalCustomers.
    /// </summary>
    [HttpGet("customer-debt/overview")]
    public async Task<IActionResult> GetCustomerDebtOverview()
    {
        var branchId = _currentBranch.GetBranchId();
        if (branchId == null) return Unauthorized(new ApiResponse<object> { Success = false, Message = "Branch required." });

        var customers = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId.Value && c.Status == 1)
            .ToListAsync();

        decimal totalDebt = 0m;
        decimal overdueDebt = 0m;
        int overdueCount = 0;

        foreach (var c in customers)
        {
            var debt = await _customerDebtService.CalculateCurrentDebtAsync(c.CustomerId);
            totalDebt += debt.CurrentDebt;
            if (c.DebtLimit.HasValue && debt.CurrentDebt > c.DebtLimit.Value)
            {
                overdueDebt += debt.CurrentDebt;
                overdueCount++;
            }
        }

        var totalCustomers = customers.Count;
        var activeCount = totalCustomers;
        var avgDebt = totalCustomers > 0 ? Math.Round(totalDebt / totalCustomers, 2) : 0m;

        return Ok(new MEP_Management_System.DTOs.CustomerDebtOverviewDto
        {
            TotalDebt = totalDebt,
            OverdueDebt = overdueDebt,
            AverageDebtPerCustomer = avgDebt,
            OverdueCount = overdueCount,
            ActiveCount = activeCount,
            TotalCustomers = totalCustomers
        });
    }

    /// <summary>
    /// GET /api/owner/dashboard/top-products?from=&to=&limit=10&sortBy=revenue
    /// Top products by revenue or profit within a date range.
    /// </summary>
    [HttpGet("top-products")]
    public async Task<IActionResult> GetTopProducts(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int limit = 10,
        [FromQuery] string sortBy = "revenue")
    {
        var branchId = _currentBranch.GetBranchId();
        if (branchId == null) return Unauthorized(new ApiResponse<object> { Success = false, Message = "Branch required." });

        var now = VietnamTime.Now;
        var start = from ?? now.AddDays(-30);
        var end = to ?? now;
        limit = Math.Clamp(limit, 1, 50);

        var products = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order != null && oi.Order.BranchId == branchId.Value
                         && (oi.Order.Status == "COMPLETED" || oi.Order.Status == "Completed")
                         && oi.Order.CreatedAt.HasValue && oi.Order.CreatedAt >= start && oi.Order.CreatedAt < end)
            .GroupBy(oi => new { oi.BranchProduct.ProductId, oi.BranchProduct.Product.ProductName, oi.BranchProduct.Product.ProductCode })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductName,
                g.Key.ProductCode,
                Revenue = g.Sum(x => x.Quantity * x.SellPrice),
                QuantitySold = g.Sum(x => x.Quantity),
                Cost = g.Sum(x => x.Quantity * x.BranchProduct.CostPrice)
            })
            .ToListAsync();

        var sorted = (sortBy == "profit"
            ? products.OrderByDescending(p => p.Revenue - p.Cost)
            : products.OrderByDescending(p => p.Revenue))
            .Take(limit)
            .Select(p => new MEP_Management_System.DTOs.TopProductDto
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                ProductCode = p.ProductCode,
                Revenue = p.Revenue,
                QuantitySold = (int)p.QuantitySold,
                Profit = p.Revenue - p.Cost
            })
            .ToList();

        return Ok(new { success = true, data = sorted });
    }

    private Guid GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
        }
    }
