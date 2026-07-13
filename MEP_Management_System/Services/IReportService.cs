using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.InventoryDTOs;

namespace MEP_Management_System.Services;

/// <summary>
/// Interface cho Report Service - chứa các method tính toán báo cáo.
/// Logic tính toán được tách rời từ Controller để dùng chung cho Web UI và Chatbot.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Lấy báo cáo cuối ngày: tổng doanh thu, đơn hàng, breakdown thanh toán, phiếu kho.
    /// </summary>
    Task<DailyEndReportDto> GetDailyEndReportAsync(DailyEndReportRequestDto request, ClaimsPrincipal currentUser);

    /// <summary>
    /// Lấy báo cáo xuất nhập tồn: opening/inward/outward/closing với giá trị tồn kho.
    /// </summary>
    Task<StockMovementReportDto> GetStockMovementReportAsync(StockMovementReportRequestDto request, ClaimsPrincipal currentUser);

    /// <summary>
    /// Lấy báo cáo doanh thu theo thời gian (day/week/month) - biểu đồ + bảng.
    /// </summary>
    Task<RevenueByTimeReportDto> GetRevenueByTimeReportAsync(RevenueByTimeRequestDto request, ClaimsPrincipal currentUser);

    /// <summary>
    /// Lấy báo cáo tồn kho sắp hết - sản phẩm dưới MinStock với mức độ tộc.
    /// </summary>
    Task<LowStockReportDto> GetLowStockReportAsync(LowStockReportRequestDto request, ClaimsPrincipal currentUser);

    /// <summary>
    /// Lấy báo cáo doanh thu/lợi nhuận theo sản phẩm.
    /// </summary>
    Task<ProductProfitReportDto> GetProductProfitReportAsync(ProductProfitReportRequestDto request, ClaimsPrincipal currentUser);

    /// <summary>
    /// Lấy trang chi tiết nhà cung cấp - tổng hợp công nợ, mua hàng, thanh toán
    /// </summary>
    Task<SupplierDetailReportDto> GetSupplierDetailReportAsync(SupplierDetailRequestDto request, ClaimsPrincipal currentUser);
}
