using System.Security.Claims;
using MEP_Management_System.DTOs;
using MEP_Management_System.Services;
using MEP_Management_System.Services.OwnerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.OwnerController;

[ApiController]
[Route("api/owner/dashboard")]
[Authorize(Roles = "Owner")]
[Tags("Owner Dashboard")]
public class OwnerDashboardController : ControllerBase
{
    private readonly IOwnerDashboardService _ownerDashboardService;
    private readonly ICurrentBranchAccessor _currentBranch;

    public OwnerDashboardController(
        IOwnerDashboardService ownerDashboardService,
        ICurrentBranchAccessor currentBranch)
    {
        _ownerDashboardService = ownerDashboardService;
        _currentBranch = currentBranch;
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var userId) ? userId : Guid.Empty;
    }

    private Guid? GetRequiredBranchId()
    {
        return _currentBranch.GetBranchId();
    }

    /// <summary>
    /// GET /api/owner/dashboard/overview
    /// Returns the owner dashboard overview for the current default branch.
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var branchId = GetRequiredBranchId();
        if (branchId == null)
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy chi nhánh mặc định của Owner.",
                Errors = new() { "Default branch is required for owner dashboard." }
            });

        var ownerUserId = GetCurrentUserId();
        var result = await _ownerDashboardService.GetOverviewAsync(ownerUserId, branchId.Value);
        return Ok(result);
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
        var branchId = GetRequiredBranchId();
        if (branchId == null)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Branch required." });

        var result = await _ownerDashboardService.GetCustomerDebtListAsync(branchId.Value, isOverdue, page, pageSize, search);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/owner/dashboard/customer-debt/overview
    /// Returns 6-field overview: TotalDebt, OverdueDebt, AverageDebtPerCustomer, OverdueCount, ActiveCount, TotalCustomers.
    /// </summary>
    [HttpGet("customer-debt/overview")]
    public async Task<IActionResult> GetCustomerDebtOverview()
    {
        var branchId = GetRequiredBranchId();
        if (branchId == null)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Branch required." });

        var result = await _ownerDashboardService.GetCustomerDebtOverviewAsync(branchId.Value);
        return Ok(result);
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
        var branchId = GetRequiredBranchId();
        if (branchId == null)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Branch required." });

        var result = await _ownerDashboardService.GetTopProductsAsync(branchId.Value, from, to, limit, sortBy);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/owner/dashboard/debug-payments?days=30
    /// Debug: liệt kê chi tiết từng payment để kiểm tra số liệu.
    /// </summary>
    [HttpGet("debug-payments")]
    public async Task<IActionResult> DebugPayments([FromQuery] int days = 30)
    {
        var branchId = GetRequiredBranchId();
        if (branchId == null)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Branch required." });

        var result = await _ownerDashboardService.DebugPaymentsAsync(branchId.Value, days);
        return Ok(result);
    }
}
