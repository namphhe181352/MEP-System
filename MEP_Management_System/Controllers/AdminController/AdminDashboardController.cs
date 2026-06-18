using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace MEP_Management_System.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/dashboard")]
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
    public async Task<IActionResult> Export()
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

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"mep-dashboard-{now:yyyyMMdd}.csv");
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
