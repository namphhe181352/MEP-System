using MEP_Management_System.DTOs;
using MEP_Management_System.Services;
using MEP_Management_System.DTOs.InventoryDTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace MEP_Management_System.Controllers.InventoryController;

[ApiController]
[Route("api/reports")]
[Tags("Báo cáo (Reports)")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Báo cáo cuối ngày - tổng doanh thu, đơn hàng, breakdown thanh toán, phiếu kho.
    /// </summary>
    [HttpPost("daily-end")]
    public async Task<IActionResult> GetDailyEndReport([FromBody] DailyEndReportRequestDto request)
    {
        var result = await _reportService.GetDailyEndReportAsync(request, User);
        return Ok(new ApiResponse<DailyEndReportDto>
        {
            Success = true,
            Message = "Báo cáo cuối ngày đã được tải.",
            Data = result
        });
    }

    /// <summary>
    /// Báo cáo xuất nhập tồn - tồn đầu kỳ, nhập, xuất, tồn cuối.
    /// </summary>
    [HttpPost("stock-movement")]
    public async Task<IActionResult> GetStockMovementReport([FromBody] StockMovementReportRequestDto request)
    {
        var result = await _reportService.GetStockMovementFromLedgerAsync(request, User);
        return Ok(new ApiResponse<StockMovementReportDto>
        {
            Success = true,
            Message = "Báo cáo xuất nhập tồn đã được tải.",
            Data = result
        });
    }

    /// <summary>
    /// Báo cáo doanh thu theo thời gian - biểu đồ + bảng.
    /// </summary>
    [HttpPost("revenue-by-time")]
    public async Task<IActionResult> GetRevenueByTimeReport([FromBody] RevenueByTimeRequestDto request)
    {
        var result = await _reportService.GetRevenueByTimeReportAsync(request, User);
        return Ok(new ApiResponse<RevenueByTimeReportDto>
        {
            Success = true,
            Message = "Báo cáo doanh thu theo thời gian đã được tải.",
            Data = result
        });
    }

    /// <summary>
    /// Báo cáo tồn kho sắp hết - sản phẩm dưới MinStock.
    /// </summary>
    [HttpPost("low-stock")]
    public async Task<IActionResult> GetLowStockReport([FromBody] LowStockReportRequestDto request)
    {
        var result = await _reportService.GetLowStockReportAsync(request, User);
        return Ok(new ApiResponse<LowStockReportDto>
        {
            Success = true,
            Message = "Báo cáo tồn kho sắp hết đã được tải.",
            Data = result
        });
    }

    /// <summary>
    /// Báo cáo doanh thu/lợi nhuận theo sản phẩm.
    /// </summary>
    [HttpPost("product-profit")]
    public async Task<IActionResult> GetProductProfitReport([FromBody] ProductProfitReportRequestDto request)
    {
        var result = await _reportService.GetProductProfitReportAsync(request, User);
        return Ok(new ApiResponse<ProductProfitReportDto>
        {
            Success = true,
            Message = "Báo cáo doanh thu/lợi nhuận theo sản phẩm đã được tải.",
            Data = result
        });
    }

    /// <summary>
    /// Trang chi tiết nhà cung cấp - tổng hợp công nợ, mua hàng, thanh toán.
    /// </summary>
    [HttpPost("supplier-detail")]
    public async Task<IActionResult> GetSupplierDetailReport([FromBody] SupplierDetailRequestDto request)
    {
        var result = await _reportService.GetSupplierDetailReportAsync(request, User);
        return Ok(new ApiResponse<SupplierDetailReportDto>
        {
            Success = true,
            Message = "Chi tiết nhà cung cấp đã được tải.",
            Data = result
        });
    }

    /// <summary>
    /// Thẻ kho chi tiết (Drill-down).
    /// </summary>
    [HttpGet("stock-ledger/{branchProductId:guid}")]
    public async Task<IActionResult> GetStockLedger(Guid branchProductId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var result = await _reportService.GetStockLedgerDetailAsync(branchProductId, fromDate, toDate, User);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Lấy thẻ kho thành công.",
            Data = result
        });
    }

    /// <summary>
    /// Chốt tồn kho cuối ngày - lưu closing_quantity, cost_price, closing_value cho toàn bộ sản phẩm.
    /// </summary>
    [HttpPost("snapshot-daily")]
    public async Task<IActionResult> CreateDailySnapshot([FromQuery] Guid? branchIdOverride)
    {
        var result = await _reportService.CreateDailySnapshotAsync(branchIdOverride, User);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = $"Đã chốt tồn kho ngày {DateTime.UtcNow:dd/MM/yyyy}.",
            Data = result
        });
    }
}
