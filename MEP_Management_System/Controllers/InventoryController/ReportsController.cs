using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.InventoryController;

[ApiController]
[Route("api/reports")]
[Tags("Báo cáo (Reports)")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly ICurrentBranchAccessor _currentBranch;
    private readonly ReportService _reportService;

    public ReportsController(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch, ReportService reportService)
    {
        _context = context;
        _currentBranch = currentBranch;
        _reportService = reportService;
    }

    private Guid? GetUserId()
    {
        var userClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userClaim, out var userId) ? userId : null;
    }

    private bool IsOwner() => User.IsInRole("Owner");

    /// <summary>
    /// Lấy branchId từ DefaultBranchId của user hiện tại (ưu tiên cao nhất).
    /// Nếu không có thì lấy từ JWT token.
    /// </summary>
    private async Task<Guid?> ResolveBranchIdAsync(Guid? requestedBranchId = null)
    {
        var userId = GetUserId();
        if (userId == null) return null;

        // Ưu tiên 1: DefaultBranchId từ User (nếu có)
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId.Value);

        if (user?.DefaultBranchId != null && user.DefaultBranchId != Guid.Empty)
        {
            return user.DefaultBranchId;
        }

        // Ưu tiên 2: BranchId từ JWT token (ICurrentBranchAccessor)
        var jwtBranchId = _currentBranch.GetBranchId();
        if (jwtBranchId.HasValue)
        {
            return jwtBranchId.Value;
        }

        // Ưu tiên 3: Lấy chi nhánh active duy nhất trong hệ thống
        var activeBranch = await _context.Branches
            .AsNoTracking()
            .Where(b => b.IsActive == 1)
            .FirstOrDefaultAsync();

        return activeBranch?.BranchId;
    }

    #region Nhóm 1: Báo cáo ưu tiên

    /// <summary>
    /// Báo cáo cuối ngày - tổng doanh thu, đơn hàng, breakdown thanh toán, phiếu kho
    /// REFACTORED: Logic di chuyển sang ReportService.GetDailyEndReportAsync()
    /// </summary>
    [HttpPost("daily-end")]
    public async Task<IActionResult> GetDailyEndReport([FromBody] DailyEndReportRequestDto request)
    {
        try
        {
            var result = await _reportService.GetDailyEndReportAsync(request, User);

            return Ok(new ApiResponse<DailyEndReportDto>
            {
                Success = true,
                Message = "Báo cáo cuối ngày đã được tải.",
                Data = result
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh.",
                Errors = new() { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi tạo báo cáo cuối ngày.",
                Errors = new() { ex.Message }
            });
        }
    }

    /// <summary>
    /// Báo cáo xuất nhập tồn - tồn đầu kỳ, nhập, xuất, tồn cuối
    /// </summary>
    [HttpPost("stock-movement")]
    public async Task<IActionResult> GetStockMovementReport([FromBody] StockMovementReportRequestDto request)
    {
        try
        {
            var result = await _reportService.GetStockMovementReportAsync(request, User);

            return Ok(new ApiResponse<StockMovementReportDto>
            {
                Success = true,
                Message = "Báo cáo xuất nhập tồn đã được tải.",
                Data = result
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh.",
                Errors = new() { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi tạo báo cáo xuất nhập tồn.",
                Errors = new() { ex.Message }
            });
        }
    }

    /// <summary>
    /// Báo cáo doanh thu theo thời gian - biểu đồ + bảng
    /// REFACTORED: Logic di chuyển sang ReportService.GetRevenueByTimeReportAsync()
    /// </summary>
    [HttpPost("revenue-by-time")]
    public async Task<IActionResult> GetRevenueByTimeReport([FromBody] RevenueByTimeRequestDto request)
    {
        try
        {
            var result = await _reportService.GetRevenueByTimeReportAsync(request, User);

            return Ok(new ApiResponse<RevenueByTimeReportDto>
            {
                Success = true,
                Message = "Báo cáo doanh thu theo thời gian đã được tải.",
                Data = result
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh.",
                Errors = new() { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi tạo báo cáo doanh thu.",
                Errors = new() { ex.Message }
            });
        }
    }

    #endregion

    #region Nhóm 2: Báo cáo tiếp theo

    /// <summary>
    /// Báo cáo tồn kho sắp hết - sản phẩm dưới MinStock
    /// REFACTORED: Logic di chuyển sang ReportService.GetLowStockReportAsync()
    /// </summary>
    [HttpPost("low-stock")]
    public async Task<IActionResult> GetLowStockReport([FromBody] LowStockReportRequestDto request)
    {
        try
        {
            var result = await _reportService.GetLowStockReportAsync(request, User);

            return Ok(new ApiResponse<LowStockReportDto>
            {
                Success = true,
                Message = "Báo cáo tồn kho sắp hết đã được tải.",
                Data = result
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh.",
                Errors = new() { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi tạo báo cáo tồn kho sắp hết.",
                Errors = new() { ex.Message }
            });
        }
    }

    /// <summary>
    /// Báo cáo doanh thu/lợi nhuận theo sản phẩm
    /// </summary>
    [HttpPost("product-profit")]
    public async Task<IActionResult> GetProductProfitReport([FromBody] ProductProfitReportRequestDto request)
    {
        try
        {
            var result = await _reportService.GetProductProfitReportAsync(request, User);

            return Ok(new ApiResponse<ProductProfitReportDto>
            {
                Success = true,
                Message = "Báo cáo doanh thu/lợi nhuận theo sản phẩm đã được tải.",
                Data = result
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh.",
                Errors = new() { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi tạo báo cáo lợi nhuận theo sản phẩm.",
                Errors = new() { ex.Message }
            });
        }
    }

    /// <summary>
    /// Trang chi tiết nhà cung cấp - tổng hợp công nợ, mua hàng, thanh toán
    /// </summary>
    [HttpPost("supplier-detail")]
    public async Task<IActionResult> GetSupplierDetailReport([FromBody] SupplierDetailRequestDto request)
    {
        try
        {
            var result = await _reportService.GetSupplierDetailReportAsync(request, User);

            return Ok(new ApiResponse<SupplierDetailReportDto>
            {
                Success = true,
                Message = "Chi tiết nhà cung cấp đã được tải.",
                Data = result
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Nhà cung cấp không tồn tại.",
                Errors = new() { ex.Message }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh.",
                Errors = new() { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi tải chi tiết nhà cung cấp.",
                Errors = new() { ex.Message }
            });
        }
    }

    #endregion

    #region Helper Methods

    private int GetWeekNumber(DateTime date)
    {
        var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
        return cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }

    private string GetSeverity(decimal currentStock, decimal minimumStock)
    {
        if (currentStock == 0)
            return "Critical";
        if (currentStock <= minimumStock * 0.5m)
            return "Critical";
        if (currentStock <= minimumStock * 0.75m)
            return "Warning";
        return "Low";
    }

    #endregion
}
