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
    private static readonly string[] CompletedStatuses = { "COMPLETED", "Completed", "PAID", "Paid", "DONE", "Done" };

    public AdminDashboardController(MEPDatabaseContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /api/admin/dashboard/overview
    /// Admin oversees the platform, not individual stores, so the payload is
    /// strictly system-level counts. No per-branch revenue, orders, customers,
    /// low stock, or top-branch ranking.
    /// Merged fields (previously /stats): activeTenants, subscriptionRevenue
    /// (last 30d), b2bCommission (1% of TRANSFER orders).
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var thirtyDaysAgo = now.AddDays(-30);

        var activeBranches = await _context.Branches
            .AsNoTracking()
            .Where(b => b.IsActive == 1)
            .CountAsync();

        var activeProducts = await _context.Products
            .AsNoTracking()
            .Where(p => p.Status == 1)
            .CountAsync();

        var activeTenants = await _context.Users
            .AsNoTracking()
            .Where(u => u.IsActive == 1)
            .CountAsync();

        var subscriptionRevenue = await _context.Invoices
            .AsNoTracking()
            .Where(i => CompletedStatuses.Contains(i.Status) && i.CreatedAt >= thirtyDaysAgo)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

        var b2bCommissionRaw = await _context.Orders
            .AsNoTracking()
            .Where(o => o.PaymentMethod == "TRANSFER")
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        var b2bCommission = Math.Round(b2bCommissionRaw * 0.01m, 2);

        return Ok(new DashboardOverviewDto
        {
            GeneratedAt = now,
            BranchId = Guid.Empty,
            BranchName = "Tất cả chi nhánh",
            BranchAddress = string.Empty,
            BranchManagerName = string.Empty,

            ActiveBranches = activeBranches,
            ActiveProducts = activeProducts,
            ActiveTenants = activeTenants,
            SubscriptionRevenue = subscriptionRevenue,
            B2bCommission = b2bCommission
        });
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
