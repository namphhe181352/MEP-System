using System.Security.Claims;
using MEP_Management_System.DTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.OwnerServices;

public class OwnerDashboardService : IOwnerDashboardService
{
    private readonly MEPDatabaseContext _context;
    private readonly CustomerDebtService _customerDebtService;
    private readonly ISupplierDebtService _supplierDebtService;

    private static readonly string[] CompletedStatuses = { "COMPLETED", "Completed", "PAID", "Paid", "DONE", "Done" };

    public OwnerDashboardService(
        MEPDatabaseContext context,
        CustomerDebtService customerDebtService,
        ISupplierDebtService supplierDebtService)
    {
        _context = context;
        _customerDebtService = customerDebtService;
        _supplierDebtService = supplierDebtService;
    }

    public async Task<DashboardOverviewDto> GetOverviewAsync(Guid ownerUserId, Guid branchId)
    {
        var branch = await _context.Branches
            .AsNoTracking()
            .Include(b => b.ManagerUser)
            .FirstOrDefaultAsync(b => b.BranchId == branchId && b.IsActive != 0);

        if (branch == null)
            throw new KeyNotFoundException("Không tìm thấy chi nhánh.");

        var now = VietnamTime.Now;
        var last30Days = now.AddDays(-30);
        var last7Days = now.AddDays(-7);
        var last24Hours = now.AddDays(-1);
        var yearStart = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(new DateTime(VietnamTime.Now.Year, 1, 1), DateTimeKind.Unspecified), VietnamTime.Tz);
        var twelveMonthsAgo = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(new DateTime(VietnamTime.Now.Year, VietnamTime.Now.Month, 1).AddMonths(-11), DateTimeKind.Unspecified), VietnamTime.Tz);
        var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(VietnamTime.TodayStart, DateTimeKind.Unspecified), VietnamTime.Tz);
        var todayEndUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(VietnamTime.TodayEnd, DateTimeKind.Unspecified), VietnamTime.Tz);

        var branchCount = await _context.Branches
            .AsNoTracking()
            .Where(b => b.StaffAssignments.Any(sa => sa.UserId == ownerUserId) && b.IsActive == 1)
            .CountAsync();

        var revenueLast30Days = await GetBranchMetricsAsync(branchId, last30Days, now);
        var revenueLast7Days = await GetBranchMetricsAsync(branchId, last7Days, now);
        var revenueLast24Hours = await GetBranchMetricsAsync(branchId, last24Hours, now);
        var revenueYearToDate = await GetBranchMetricsAsync(branchId, yearStart, now);

        var activeProducts = await _context.BranchProducts.AsNoTracking()
            .Where(bp => bp.BranchId == branchId && bp.IsActive == 1)
            .CountAsync();

        var lowStockProductCount = await _context.BranchProducts.AsNoTracking()
            .Where(bp => bp.BranchId == branchId && bp.IsActive == 1 && bp.MinimumStock.HasValue && bp.AvailableStock <= bp.MinimumStock)
            .CountAsync();

        var activeCustomerIds = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status == 1)
            .Select(c => c.CustomerId)
            .ToListAsync();

        var activeCustomerCount = activeCustomerIds.Count;
        var debtMap = await _customerDebtService.CalculateCurrentDebtBatchAsync(activeCustomerIds);
        var activeCustomerTotalDebt = debtMap.Values.Sum();

        var overdueCustomers = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status == 1 && c.DebtLimit.HasValue && c.DebtLimit > 0)
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
            CustomerCount = debtMap.Count(kv => kv.Value > 0),
            TotalDebt = debtMap.Values.Sum(),
            OverdueCustomerCount = overdueCount,
            OverdueDebt = overdueDebtSum
        };

        var topProductsByRevenue = await _context.InvoiceItems
            .AsNoTracking()
            .Where(ii => ii.Invoice.Order.BranchId == branchId
                         && CompletedStatuses.Contains(ii.Invoice.Status)
                         && ii.Invoice.CreatedAt.HasValue && ii.Invoice.CreatedAt >= last30Days && ii.Invoice.CreatedAt < now)
            .GroupBy(ii => new
            {
                ii.BranchProduct.ProductId,
                ii.BranchProduct.Product.ProductCode,
                ii.BranchProduct.Product.ProductName,
                ii.BranchProduct.Product.CategoryName
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
            .Where(o => o.BranchId == branchId && o.CustomerId != null
                        && CompletedStatuses.Contains(o.Status)
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
            tc.TotalDebt = debtMap.TryGetValue(tc.CustomerId, out var d) ? d : 0m;

        var expenseByCategory = await _context.ExpenseVouchers
            .AsNoTracking()
            .Where(ev => ev.BranchId == branchId
                         && CompletedStatuses.Contains(ev.Status)
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

        var revenueToday = await GetBranchMetricsAsync(branchId, todayStartUtc, todayEndUtc);

        var newCustomerCountToday = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status == 1 && c.CreatedAt.HasValue && c.CreatedAt >= todayStartUtc && c.CreatedAt < todayEndUtc)
            .CountAsync();

        var newCustomerCountLast7Days = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status == 1 && c.CreatedAt.HasValue && c.CreatedAt >= last7Days && c.CreatedAt < now)
            .CountAsync();

        var newCustomerCountLast30Days = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status == 1 && c.CreatedAt.HasValue && c.CreatedAt >= last30Days && c.CreatedAt < now)
            .CountAsync();

        var pendingOrderCount = await _context.Orders.AsNoTracking()
            .Where(o => o.BranchId == branchId && o.Status == "PENDING"
                        && o.CreatedAt.HasValue && o.CreatedAt >= last30Days && o.CreatedAt < now)
            .CountAsync();

        var pendingOrderAmount = await _context.Orders.AsNoTracking()
            .Where(o => o.BranchId == branchId && o.Status == "PENDING"
                        && o.CreatedAt.HasValue && o.CreatedAt >= last30Days && o.CreatedAt < now)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        var activeSupplierIds = await _context.Suppliers.AsNoTracking()
            .Where(s => s.BranchId == branchId && (s.Status == 1 || s.Status == null))
            .Select(s => s.SupplierId)
            .ToListAsync();

        var supplierDebtMap = await _supplierDebtService.CalculateCurrentDebtBatchAsync(activeSupplierIds);
        var totalSupplierDebt = supplierDebtMap.Values.Sum(x => x.CurrentDebt);

        var supplierDebtPaidLast30Days = await _context.SupplierPayments.AsNoTracking()
            .Where(sp => sp.Supplier.BranchId == branchId && sp.CreatedAt >= last30Days && sp.CreatedAt < now && sp.IsDeleted == 0)
            .SumAsync(sp => (decimal?)sp.Amount) ?? 0m;

        var refundTrend = await BuildRefundTrendAsync(branchId, twelveMonthsAgo, now);

        var recentEvents = await _context.SystemLogs.AsNoTracking()
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

        var lowStockProducts = await _context.BranchProducts.AsNoTracking()
            .Where(bp => bp.BranchId == branchId && bp.IsActive == 1
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

        var categoryPerformance = await _context.InvoiceItems
            .AsNoTracking()
            .Where(ii => ii.Invoice.Order.BranchId == branchId
                         && CompletedStatuses.Contains(ii.Invoice.Status)
                         && ii.Invoice.CreatedAt.HasValue && ii.Invoice.CreatedAt >= last30Days && ii.Invoice.CreatedAt < now)
            .GroupBy(ii => ii.BranchProduct.Product.CategoryName)
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

        var customerGrowthTrend = await BuildCustomerGrowthTrendAsync(branchId, twelveMonthsAgo, now);
        var revenueTrendLast30Days = await BuildDailyRevenueTrendAsync(branchId, last30Days, now);

        var abnormalShifts = await _context.Shifts.AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.ClosedBy)
            .Where(s => s.BranchId == branchId && s.Status == "CLOSED" && s.Variance.HasValue && s.Variance.Value != 0)
            .OrderByDescending(s => s.EndedAt)
            .Take(10)
            .Select(s => new DashboardAbnormalShiftDto
            {
                ShiftId = s.ShiftId,
                ShiftCode = s.ShiftCode,
                UserName = s.User != null ? s.User.FullName : null,
                ClosedByUserName = s.ClosedBy != null ? s.ClosedBy.FullName : null,
                EndedAt = s.EndedAt,
                Variance = s.Variance ?? 0m,
                Note = s.Note
            })
            .ToListAsync();

        var branchRevenueTrend = await BuildTrendAsync(_context.Invoices.AsNoTracking()
            .Where(i => i.Order.BranchId == branchId
                        && CompletedStatuses.Contains(i.Status)
                        && i.CreatedAt.HasValue && i.CreatedAt >= twelveMonthsAgo && i.CreatedAt < now),
            "CreatedAt", "TotalAmount", twelveMonthsAgo, now, 12);

        var branchExpenseTrend = await BuildTrendAsync(_context.ExpenseVouchers.AsNoTracking()
            .Where(ev => ev.BranchId == branchId
                         && CompletedStatuses.Contains(ev.Status)
                         && ev.CreatedAt.HasValue && ev.CreatedAt >= twelveMonthsAgo && ev.CreatedAt < now),
            "CreatedAt", "Amount", twelveMonthsAgo, now, 12);

        return new DashboardOverviewDto
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
            PaymentBreakdownLast30Days = revenueLast30Days.paymentBreakdown,
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
                .Where(ev => ev.BranchId == branchId && ev.Status == "PENDING" && ev.CreatedAt.HasValue && ev.CreatedAt >= last30Days && ev.CreatedAt < now)
                .CountAsync(),
            PendingExpenseAmount = await _context.ExpenseVouchers.AsNoTracking()
                .Where(ev => ev.BranchId == branchId && ev.Status == "PENDING" && ev.CreatedAt.HasValue && ev.CreatedAt >= last30Days && ev.CreatedAt < now)
                .SumAsync(ev => (decimal?)ev.Amount) ?? 0m,
            PendingOrderCount = pendingOrderCount,
            PendingOrderAmount = pendingOrderAmount,
            TotalSupplierDebt = totalSupplierDebt,
            SupplierDebtPaidLast30Days = supplierDebtPaidLast30Days,
            RepaymentCoverageRatio = totalSupplierDebt > 0 ? Math.Round(activeCustomerTotalDebt / totalSupplierDebt * 100m, 2) : 0m,
            RevenueTrendLast12Months = branchRevenueTrend,
            RevenueTrendLast30Days = revenueTrendLast30Days,
            ExpenseTrendLast12Months = branchExpenseTrend,
            RefundTrendLast12Months = refundTrend,
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
            AbnormalShifts = abnormalShifts,
            RecentSystemEvents = recentEvents
        };
    }

    public async Task<object> GetCustomerDebtListAsync(Guid branchId, bool? isOverdue, int page, int pageSize, string? search)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status == 1);

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

        var items = new List<CustomerDebtListItemDto>();
        foreach (var c in customers)
        {
            var debt = await _customerDebtService.CalculateCurrentDebtAsync(c.CustomerId);
            var lastOrder = await _context.Orders
                .Where(o => o.CustomerId == c.CustomerId)
                .MaxAsync(o => (DateTime?)o.CreatedAt);
            var lastPayment = await _context.Payments
                .Where(p => p.Status == "Confirmed" && (p.Invoice != null && p.Invoice.Order.CustomerId == c.CustomerId || p.CustomerId == c.CustomerId))
                .MaxAsync(p => (DateTime?)p.CreatedAt);

            items.Add(new CustomerDebtListItemDto
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

        return new
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
        };
    }

    public async Task<CustomerDebtOverviewDto> GetCustomerDebtOverviewAsync(Guid branchId)
    {
        var customers = await _context.Customers.AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Status == 1)
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
        var avgDebt = totalCustomers > 0 ? Math.Round(totalDebt / totalCustomers, 2) : 0m;

        return new CustomerDebtOverviewDto
        {
            TotalDebt = totalDebt,
            OverdueDebt = overdueDebt,
            AverageDebtPerCustomer = avgDebt,
            OverdueCount = overdueCount,
            ActiveCount = totalCustomers,
            TotalCustomers = totalCustomers
        };
    }

    public async Task<object> GetTopProductsAsync(Guid branchId, DateTime? from, DateTime? to, int limit, string sortBy)
    {
        var now = VietnamTime.Now;
        var start = from ?? now.AddDays(-30);
        var end = to ?? now;
        limit = Math.Clamp(limit, 1, 50);

        var products = await _context.InvoiceItems
            .AsNoTracking()
            .Where(ii => ii.Invoice.Order.BranchId == branchId
                         && CompletedStatuses.Contains(ii.Invoice.Status)
                         && ii.Invoice.CreatedAt.HasValue && ii.Invoice.CreatedAt >= start && ii.Invoice.CreatedAt < end)
            .GroupBy(ii => new { ii.BranchProduct.ProductId, ii.BranchProduct.Product.ProductName, ii.BranchProduct.Product.ProductCode })
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
            .Select(p => new TopProductDto
            {
                ProductId = p.ProductId,
                ProductName = p.ProductName,
                ProductCode = p.ProductCode,
                Revenue = p.Revenue,
                QuantitySold = (int)p.QuantitySold,
                Profit = p.Revenue - p.Cost
            })
            .ToList();

        return new { success = true, data = sorted };
    }

    public async Task<object> DebugPaymentsAsync(Guid branchId, int days)
    {
        var now = VietnamTime.Now;
        var from = now.AddDays(-days);

        var payments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.BranchId == branchId && p.Status == "Confirmed"
                        && CompletedStatuses.Contains(p.Invoice.Status)
                        && p.PaidAt.HasValue && p.PaidAt >= from && p.PaidAt < now)
            .OrderByDescending(p => p.PaidAt)
            .Select(p => new
            {
                p.PaymentId,
                p.Method,
                p.Amount,
                p.PaidAt,
                p.CreatedAt,
                InvoiceCode = p.Invoice != null ? p.Invoice.InvoiceCode : null,
                OrderCode = p.Invoice != null && p.Invoice.Order != null ? p.Invoice.Order.OrderCode : null,
                OrderStatus = p.Invoice != null && p.Invoice.Order != null ? p.Invoice.Order.Status : null,
                InvoiceStatus = p.Invoice != null ? p.Invoice.Status : null
            })
            .ToListAsync();

        return new
        {
            success = true,
            total = payments.Count,
            byMethod = payments.GroupBy(p => p.Method).ToDictionary(g => g.Key, g => new { count = g.Count(), amount = g.Sum(p => p.Amount) }),
            payments
        };
    }

    // --- Private helper methods ---

    private async Task<(decimal revenue, int orderCount, decimal averageOrderValue, decimal expense, decimal refund, decimal cogs, decimal profit, decimal grossMargin, DashboardPaymentBreakdownDto paymentBreakdown)>
        GetBranchMetricsAsync(Guid branch, DateTime start, DateTime end)
    {
        var confirmedPayments = await _context.Payments
            .AsNoTracking()
            .Where(p => p.BranchId == branch && p.Status == "Confirmed"
                        && CompletedStatuses.Contains(p.Invoice.Status)
                        && p.PaidAt.HasValue && p.PaidAt >= start && p.PaidAt < end)
            .Select(p => new { p.InvoiceId, p.Amount, p.Method })
            .ToListAsync();

        var revenue = confirmedPayments
            .GroupBy(p => p.InvoiceId)
            .Sum(g => g.Sum(p => p.Amount));

        var orderCount = await _context.Payments
            .AsNoTracking()
            .Where(p => p.BranchId == branch && p.Status == "Confirmed"
                        && CompletedStatuses.Contains(p.Invoice.Status)
                        && p.PaidAt.HasValue && p.PaidAt >= start && p.PaidAt < end)
            .Select(p => p.Invoice.Order.OrderId)
            .Distinct()
            .CountAsync();

        var expense = await _context.ExpenseVouchers
            .AsNoTracking()
            .Where(ev => ev.BranchId == branch
                         && CompletedStatuses.Contains(ev.Status)
                         && ev.CreatedAt.HasValue && ev.CreatedAt >= start && ev.CreatedAt < end)
            .SumAsync(ev => (decimal?)ev.Amount) ?? 0m;

        var refund = await _context.ReturnOrders
            .AsNoTracking()
            .Where(ro => ro.Order.BranchId == branch
                         && CompletedStatuses.Contains(ro.Status)
                         && ro.CreatedAt.HasValue && ro.CreatedAt >= start && ro.CreatedAt < end)
            .SumAsync(ro => (decimal?)ro.RefundAmount) ?? 0m;

        var cogs = await _context.InvoiceItems
            .AsNoTracking()
            .Where(ii => ii.Invoice.Order.BranchId == branch
                         && CompletedStatuses.Contains(ii.Invoice.Status)
                         && ii.Invoice.Payments.Any(p => p.Status == "Confirmed" && p.PaidAt.HasValue && p.PaidAt >= start && p.PaidAt < end))
            .SumAsync(ii => (decimal?)ii.Quantity * ii.BranchProduct.CostPrice) ?? 0m;

        var paymentBreakdownData = await _context.Payments
            .AsNoTracking()
            .Where(p => p.BranchId == branch && p.Status == "Confirmed"
                        && CompletedStatuses.Contains(p.Invoice.Status)
                        && p.PaidAt.HasValue && p.PaidAt >= start && p.PaidAt < end)
            .GroupBy(p => p.Method)
            .Select(g => new { g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync();

        var breakdown = new DashboardPaymentBreakdownDto();
        foreach (var item in paymentBreakdownData)
        {
            if (item.Key == "Cash") { breakdown.CashAmount = item.Amount; breakdown.CashCount = item.Count; }
            else if (item.Key == "Transfer") { breakdown.TransferAmount = item.Amount; breakdown.TransferCount = item.Count; }
            else if (item.Key == "Combined") { breakdown.CombinedAmount = item.Amount; breakdown.CombinedCount = item.Count; }
            else if (item.Key == "Debt") { breakdown.DebtAmount = item.Amount; breakdown.DebtCount = item.Count; }
        }

        var profit = revenue - cogs - expense - refund;
        var grossMargin = revenue != 0 ? Math.Round((revenue - cogs) / revenue * 100m, 2) : 0m;
        var averageOrderValue = orderCount > 0 ? Math.Round(revenue / orderCount, 2) : 0m;
        return (revenue, orderCount, averageOrderValue, expense, refund, cogs, profit, grossMargin, breakdown);
    }

    private async Task<List<DashboardTrendPointDto>> BuildTrendAsync<T>(IQueryable<T> source, string createdAtProperty, string amountProperty, DateTime from, DateTime to, int months)
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

    private async Task<List<DashboardTrendPointDto>> BuildRefundTrendAsync(Guid branch, DateTime from, DateTime to)
    {
        var data = await _context.ReturnOrders.AsNoTracking()
            .Where(ro => ro.Order.BranchId == branch
                         && CompletedStatuses.Contains(ro.Status)
                         && ro.CreatedAt.HasValue && ro.CreatedAt >= from && ro.CreatedAt < to)
            .Select(ro => new { ro.CreatedAt, ro.RefundAmount })
            .ToListAsync();

        var grouped = data
            .Where(x => x.CreatedAt.HasValue)
            .GroupBy(x => new { Year = x.CreatedAt!.Value.Year, Month = x.CreatedAt!.Value.Month })
            .ToDictionary(g => new { g.Key.Year, g.Key.Month }, g => g.Sum(x => x.RefundAmount));

        return Enumerable.Range(0, 12)
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

    private async Task<List<DashboardTrendPointDto>> BuildDailyRevenueTrendAsync(Guid branch, DateTime from, DateTime to)
    {
        var data = await _context.Payments
            .AsNoTracking()
            .Where(p => p.Invoice != null
                        && p.Invoice.Order.BranchId == branch
                        && p.Status == "Confirmed"
                        && p.PaidAt.HasValue && p.PaidAt >= from && p.PaidAt < to)
            .Select(p => new { p.PaidAt, p.Invoice.TotalAmount, p.InvoiceId })
            .ToListAsync();

        var grouped = data
            .Where(x => x.PaidAt.HasValue)
            .GroupBy(x => x.PaidAt!.Value.Date)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new { x.InvoiceId, x.TotalAmount }).Distinct().Sum(x => x.TotalAmount)
            );

        var days = (to.Date - from.Date).Days;
        return Enumerable.Range(0, days)
            .Select(offset => from.Date.AddDays(offset))
            .Select(date =>
            {
                grouped.TryGetValue(date, out var amount);
                return new DashboardTrendPointDto
                {
                    Period = date.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture),
                    Amount = amount,
                    Count = 0
                };
            })
            .ToList();
    }

    private async Task<List<DashboardTrendPointDto>> BuildCustomerGrowthTrendAsync(Guid branch, DateTime from, DateTime to)
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
}
