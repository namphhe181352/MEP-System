using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
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

    public AdminDashboardController(MEPDatabaseContext context)
    {
        _context = context;
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

        // activeTenants = Owners with IsActive = 1 (via many-to-many UserRole)
        var activeTenants = await _context.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .Where(u => u.Roles.Any(r => r.RoleName == "Owner") && u.IsActive == 1)
            .CountAsync();

        // subscriptionRevenue = total COMPLETED invoice amounts in last 30 days
        var subscriptionRevenue = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.Status == "COMPLETED" && i.CreatedAt >= thirtyDaysAgo)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

        // b2bCommission = 1% of total TRANSFER payment orders
        var b2bCommissionRaw = await _context.Orders
            .AsNoTracking()
            .Where(o => o.PaymentMethod == "TRANSFER")
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        var b2bCommission = Math.Round(b2bCommissionRaw * 0.01m, 2);

        // overdueAlerts = customers with CurrentDebt > DebtLimit
        var overdueAlerts = await _context.Customers
            .AsNoTracking()
            .Where(c => c.CurrentDebt > c.DebtLimit)
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

        var branchId = await _context.Branches.Where(b => b.IsActive == 1).Select(b => (Guid?)b.BranchId).FirstOrDefaultAsync();
        if (branchId == null) return NotFound("No active branch found.");

        var totalOrdersLast30Days = await _context.Orders.CountAsync(o => o.BranchId == branchId && (o.Status == "COMPLETED" || o.Status == "Completed") && o.CreatedAt >= thirtyDaysAgo);
        
        var totalRevenueLast30Days = await _context.Invoices
            .Where(i => i.Order.BranchId == branchId && (i.Status == "COMPLETED" || i.Status == "Completed") && i.CreatedAt >= thirtyDaysAgo)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;
            
        var totalOrdersLast7Days = await _context.Orders.CountAsync(o => o.BranchId == branchId && (o.Status == "COMPLETED" || o.Status == "Completed") && o.CreatedAt >= last7Days);
        var totalRevenueLast7Days = await _context.Invoices
            .Where(i => i.Order.BranchId == branchId && (i.Status == "COMPLETED" || i.Status == "Completed") && i.CreatedAt >= last7Days)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;
        
        var paymentBreakdownRaw = await _context.Payments
            .AsNoTracking()
            .Where(p => p.BranchId == branchId && p.Status == "Confirmed" && p.CreatedAt >= thirtyDaysAgo)
            .GroupBy(p => p.Method)
            .Select(g => new { g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync();
        var paymentBreakdown = new DashboardPaymentBreakdownDto();
        foreach (var item in paymentBreakdownRaw) {
            if (item.Key == "Cash") { paymentBreakdown.CashAmount = item.Amount; paymentBreakdown.CashCount = item.Count; }
            else if (item.Key == "Transfer") { paymentBreakdown.TransferAmount = item.Amount; paymentBreakdown.TransferCount = item.Count; }
            else if (item.Key == "Combined") { paymentBreakdown.CombinedAmount = item.Amount; paymentBreakdown.CombinedCount = item.Count; }
            else if (item.Key == "Debt") { paymentBreakdown.DebtAmount = item.Amount; paymentBreakdown.DebtCount = item.Count; }
        }

        var topBranches = await _context.Branches
            .AsNoTracking()
            .Where(b => b.BranchId == branchId)
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

        var today = now.Date;
        var totalOrdersToday = await _context.Orders.CountAsync(o => o.BranchId == branchId && (o.Status == "COMPLETED" || o.Status == "Completed") && o.CreatedAt >= today);
        var totalRevenueToday = await _context.Invoices.Where(i => i.Order.BranchId == branchId && (i.Status == "COMPLETED" || i.Status == "Completed") && i.CreatedAt >= today).SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;
        var newCustomersToday = await _context.Customers.CountAsync(c => c.BranchId == branchId && c.CreatedAt >= today);

        var cogsLast30Days = await _context.OrderItems
            .Where(oi => oi.Order.BranchId == branchId && (oi.Order.Status == "COMPLETED" || oi.Order.Status == "Completed") && oi.Order.CreatedAt >= thirtyDaysAgo)
            .SumAsync(oi => (decimal?)oi.Quantity * oi.BranchProduct.CostPrice) ?? 0m;
            
        var cogsLast7Days = await _context.OrderItems
            .Where(oi => oi.Order.BranchId == branchId && (oi.Order.Status == "COMPLETED" || oi.Order.Status == "Completed") && oi.Order.CreatedAt >= last7Days)
            .SumAsync(oi => (decimal?)oi.Quantity * oi.BranchProduct.CostPrice) ?? 0m;
            
        var expenseLast30Days = await _context.ExpenseVouchers
            .Where(ev => ev.BranchId == branchId && (ev.Status == "COMPLETED" || ev.Status == "Completed") && ev.CreatedAt >= thirtyDaysAgo)
            .SumAsync(ev => (decimal?)ev.Amount) ?? 0m;
            
        var expenseLast7Days = await _context.ExpenseVouchers
            .Where(ev => ev.BranchId == branchId && (ev.Status == "COMPLETED" || ev.Status == "Completed") && ev.CreatedAt >= last7Days)
            .SumAsync(ev => (decimal?)ev.Amount) ?? 0m;
            
        var refundLast30Days = await _context.ReturnOrders
            .Where(ro => ro.Order.BranchId == branchId && (ro.Status == "COMPLETED" || ro.Status == "Completed") && ro.CreatedAt >= thirtyDaysAgo)
            .SumAsync(ro => (decimal?)ro.RefundAmount) ?? 0m;
            
        var refundLast7Days = await _context.ReturnOrders
            .Where(ro => ro.Order.BranchId == branchId && (ro.Status == "COMPLETED" || ro.Status == "Completed") && ro.CreatedAt >= last7Days)
            .SumAsync(ro => (decimal?)ro.RefundAmount) ?? 0m;

        var profitLast30Days = totalRevenueLast30Days - cogsLast30Days - expenseLast30Days - refundLast30Days;
        var profitLast7Days = totalRevenueLast7Days - cogsLast7Days - expenseLast7Days - refundLast7Days;
        var grossMargin = totalRevenueLast30Days != 0 ? Math.Round((totalRevenueLast30Days - cogsLast30Days) / totalRevenueLast30Days * 100m, 2) : 0m;

        var topProductsRaw = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order.BranchId == branchId && (oi.Order.Status == "COMPLETED" || oi.Order.Status == "Completed") && oi.Order.CreatedAt >= thirtyDaysAgo)
            .GroupBy(oi => new { oi.BranchProduct.ProductId, oi.BranchProduct.Product.ProductName, oi.BranchProduct.Product.CategoryName })
            .Select(g => new DashboardProductPerformanceDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                CategoryName = g.Key.CategoryName,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Quantity * x.SellPrice),
                Cost = g.Sum(x => x.Quantity * x.BranchProduct.CostPrice)
            })
            .ToListAsync();
        var topProducts = topProductsRaw.Select(x => { x.Profit = x.Revenue - x.Cost; return x; }).OrderByDescending(x => x.Revenue).Take(5).ToList();
            
        var topCustomers = await _context.Orders
            .AsNoTracking()
            .Where(o => o.BranchId == branchId && o.CustomerId != null && (o.Status == "COMPLETED" || o.Status == "Completed") && o.CreatedAt >= thirtyDaysAgo)
            .GroupBy(o => new { o.CustomerId, o.Customer.CustomerName, o.Customer.PhoneNumber })
            .Select(g => new DashboardCustomerPerformanceDto
            {
                CustomerId = g.Key.CustomerId.Value,
                CustomerName = g.Key.CustomerName,
                PhoneNumber = g.Key.PhoneNumber,
                OrderCount = g.Count(),
                TotalSpent = g.Sum(o => o.TotalAmount),
                TotalDebt = g.Sum(o => (decimal?)o.Customer.CurrentDebt) ?? 0m
            })
            .OrderByDescending(x => x.TotalSpent)
            .Take(5)
            .ToListAsync();

        var lowStock = await _context.BranchProducts
            .AsNoTracking()
            .Where(bp => bp.BranchId == branchId && bp.IsActive == 1 && bp.MinimumStock.HasValue && bp.AvailableStock <= bp.MinimumStock)
            .Select(bp => new DashboardLowStockProductDto
            {
                ProductId = bp.ProductId,
                ProductName = bp.Product.ProductName,
                AvailableStock = bp.AvailableStock,
                MinimumStock = bp.MinimumStock ?? 0,
                Shortage = Math.Max(0, (bp.MinimumStock ?? 0) - bp.AvailableStock)
            })
            .Take(10)
            .ToListAsync();

        var twelveMonthsAgo = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

        var invoiceTrends = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.Order.BranchId == branchId && (i.Status == "COMPLETED" || i.Status == "Completed") && i.CreatedAt >= twelveMonthsAgo)
            .Select(i => new { i.CreatedAt, i.TotalAmount })
            .ToListAsync();

        var revGrouped = invoiceTrends
            .Where(x => x.CreatedAt.HasValue)
            .GroupBy(x => new { x.CreatedAt.Value.Year, x.CreatedAt.Value.Month })
            .ToDictionary(g => new { g.Key.Year, g.Key.Month }, g => g.Sum(x => x.TotalAmount));

        var expenseTrendsList = await _context.ExpenseVouchers
            .AsNoTracking()
            .Where(ev => ev.BranchId == branchId && (ev.Status == "COMPLETED" || ev.Status == "Completed") && ev.CreatedAt >= twelveMonthsAgo)
            .Select(ev => new { ev.CreatedAt, ev.Amount })
            .ToListAsync();

        var expGrouped = expenseTrendsList
            .Where(x => x.CreatedAt.HasValue)
            .GroupBy(x => new { x.CreatedAt.Value.Year, x.CreatedAt.Value.Month })
            .ToDictionary(g => new { g.Key.Year, g.Key.Month }, g => g.Sum(x => x.Amount));

        var revenueTrend = Enumerable.Range(0, 12).Select(offset => twelveMonthsAgo.AddMonths(offset)).Select(date => {
            revGrouped.TryGetValue(new { date.Year, date.Month }, out var amount);
            return new DashboardTrendPointDto { Period = date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), Amount = amount, Count = 0 };
        }).ToList();

        var expenseTrend = Enumerable.Range(0, 12).Select(offset => twelveMonthsAgo.AddMonths(offset)).Select(date => {
            expGrouped.TryGetValue(new { date.Year, date.Month }, out var amount);
            return new DashboardTrendPointDto { Period = date.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), Amount = amount, Count = 0 };
        }).ToList();
        
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

        var overview = new DashboardOverviewDto
        {
            GeneratedAt = now,
            TotalOrdersToday = totalOrdersToday,
            TotalRevenueToday = totalRevenueToday,
            NewCustomersToday = newCustomersToday,
            TotalOrdersLast30Days = totalOrdersLast30Days,
            TotalRevenueLast30Days = totalRevenueLast30Days,
            TotalCostOfGoodsSoldLast30Days = cogsLast30Days,
            TotalExpenseLast30Days = expenseLast30Days,
            TotalProfitLast30Days = profitLast30Days,
            GrossMarginPercentLast30Days = grossMargin,
            PaymentBreakdownLast30Days = paymentBreakdown,
            TopBranchesByRevenue = topBranches,
            TopProductsByRevenue = topProducts,
            TopCustomers = topCustomers,
            LowStockProducts = lowStock,
            RevenueTrendLast12Months = revenueTrend,
            ExpenseTrendLast12Months = expenseTrend,
            ActiveBranches = await _context.Branches.CountAsync(b => b.IsActive == 1),
            ActiveProducts = await _context.BranchProducts.CountAsync(bp => bp.BranchId == branchId && bp.IsActive == 1),
            ActiveCustomerCount = await _context.Customers.CountAsync(c => c.BranchId == branchId && c.Status == 1),
            CustomerDebtSummary = new DashboardDebtSummaryDto
            {
                CustomerCount = await _context.Customers.CountAsync(c => c.BranchId == branchId && c.CurrentDebt > 0),
                TotalDebt = await _context.Customers.Where(c => c.BranchId == branchId).SumAsync(c => (decimal?)c.CurrentDebt) ?? 0m,
                OverdueCustomerCount = await _context.Customers.CountAsync(c => c.BranchId == branchId && c.CurrentDebt > c.DebtLimit),
                OverdueDebt = await _context.Customers.Where(c => c.BranchId == branchId && c.CurrentDebt > c.DebtLimit).SumAsync(c => (decimal?)c.CurrentDebt) ?? 0m
            },
            AverageDailyRevenueLast30Days = totalRevenueLast30Days / 30m,
            AverageOrderValueLast30Days = totalOrdersLast30Days > 0 ? totalRevenueLast30Days / totalOrdersLast30Days : 0,
            NewCustomersLast30Days = await _context.Customers.CountAsync(c => c.BranchId == branchId && c.CreatedAt >= thirtyDaysAgo),
            PendingOrderCount = await _context.Orders.CountAsync(o => o.BranchId == branchId && o.Status == "Pending" && o.CreatedAt >= thirtyDaysAgo),
            PendingExpenseCount = await _context.ExpenseVouchers.CountAsync(ev => ev.BranchId == branchId && ev.Status == "Pending" && ev.CreatedAt >= thirtyDaysAgo),
            PendingExpenseAmount = await _context.ExpenseVouchers.Where(ev => ev.BranchId == branchId && ev.Status == "Pending" && ev.CreatedAt >= thirtyDaysAgo).SumAsync(ev => (decimal?)ev.Amount) ?? 0m,
            AverageDailyRevenueLast7Days = Math.Round(totalRevenueLast7Days / 7m, 2),
            TotalRevenueLast7Days = totalRevenueLast7Days,
            TotalProfitLast7Days = profitLast7Days,
            TotalExpenseLast7Days = expenseLast7Days,
            ExpenseByCategory = expenseByCategory
        };

        return Ok(overview);
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
            .Where(c => c.CurrentDebt > c.DebtLimit)
            .CountAsync();

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
