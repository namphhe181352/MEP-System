using System.Threading.Tasks;
using MEP_Management_System.Services.AdminDashboardServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers;

/// <summary>
/// Thin controller for Admin dashboard.
/// All business logic moved to AdminDashboardService.
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/dashboard")]
[Tags("Admin (Admin)")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminDashboardService _service;

    public AdminDashboardController(IAdminDashboardService service)
    {
        _service = service;
    }

    /// <summary>
    /// GET /api/admin/dashboard/overview
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var result = await _service.GetOverviewAsync();
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/dashboard/revenue-chart?year=2026
    /// </summary>
    [HttpGet("revenue-chart")]
    public async Task<IActionResult> GetRevenueChart([FromQuery] int year = 2026)
    {
        var result = await _service.GetRevenueChartAsync(year);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/dashboard/recent-events?limit=20
    /// </summary>
    [HttpGet("recent-events")]
    public async Task<IActionResult> GetRecentEvents([FromQuery] int limit = 20)
    {
        var result = await _service.GetRecentEventsAsync(limit);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/dashboard/export
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string format = "csv")
    {
        var (content, fileName, contentType) = await _service.ExportDataAsync(format);
        return File(content, contentType, fileName);
    }
}
