using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.InventoryDTOs;

#region Nhóm 1: Báo cáo ưu tiên

/// <summary>
/// Báo cáo cuối ngày
/// </summary>
public class DailyEndReportDto
{
    public DateTime ReportDate { get; set; }
    public Guid BranchId { get; set; }
    public string? BranchName { get; set; }

    // Tổng quan
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageOrderValue { get; set; }

    // Breakdown theo phương thức thanh toán
    public PaymentMethodBreakdown PaymentBreakdown { get; set; } = new();

    // Phiếu kho phát sinh trong ngày
    public int InwardTicketCount { get; set; }
    public int OutwardTicketCount { get; set; }

    // Hoàn tiền (chỉ áp dụng cho đơn Trả hàng / REFUND, không bao gồm đổi ngang giá / EXCHANGE)
    public decimal RefundCashAmount { get; set; }
    public decimal RefundTransferAmount { get; set; }
    public int RefundCount { get; set; }
}

public class PaymentMethodBreakdown
{
    public decimal CashAmount { get; set; }
    public int CashCount { get; set; }
    public decimal TransferAmount { get; set; }
    public int TransferCount { get; set; }
    public decimal DebtAmount { get; set; }
    public int DebtCount { get; set; }
    public decimal CombinedAmount { get; set; }
    public int CombinedCount { get; set; }
}

/// <summary>
/// Báo cáo xuất nhập tồn
/// </summary>
public class StockMovementReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid BranchId { get; set; }
    public string? BranchName { get; set; }

    public List<StockMovementItemDto> Items { get; set; } = new();

    // Tổng cộng
    public decimal TotalOpeningValue { get; set; }
    public decimal TotalInwardValue { get; set; }
    public decimal TotalOutwardValue { get; set; }
    public decimal TotalClosingValue { get; set; }
}

public class StockMovementItemDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public string BaseUnit { get; set; } = null!;

    // Số lượng
    public decimal OpeningStock { get; set; }
    public decimal InwardQuantity { get; set; }
    public decimal OutwardQuantity { get; set; }
    public decimal ClosingStock { get; set; }

    // Giá trị (theo giá vốn)
    public decimal OpeningValue { get; set; }
    public decimal InwardValue { get; set; }
    public decimal OutwardValue { get; set; }
    public decimal ClosingValue { get; set; }
}

/// <summary>
/// Báo cáo doanh thu theo thời gian
/// </summary>
public class RevenueByTimeReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid BranchId { get; set; }
    public string? BranchName { get; set; }
    public string TimeGrouping { get; set; } = "day"; // day, week, month

    // Dữ liệu cho biểu đồ
    public List<ReportRevenueChartPointDto> ChartData { get; set; } = new();

    // Dữ liệu chi tiết cho bảng
    public List<RevenueTimeItemDto> TableData { get; set; } = new();

    // Tổng cộng
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
}

public class ReportRevenueChartPointDto
{
    public string TimeKey { get; set; } = null!; // "2026-06-30" or "Week 1" or "June"
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
}

public class RevenueTimeItemDto
{
    public DateTime Date { get; set; }
    public string TimeKey { get; set; } = null!;
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
    public decimal AverageValue { get; set; }
    public decimal GrowthPercent { get; set; } // So với kỳ trước
}

#endregion

#region Nhóm 2: Báo cáo tiếp theo

/// <summary>
/// Báo cáo tồn kho sắp hết
/// </summary>
public class LowStockReportDto
{
    public Guid BranchId { get; set; }
    public string? BranchName { get; set; }

    public List<LowStockItemDto> Items { get; set; } = new();

    public int TotalItems { get; set; }
}

public class LowStockItemDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
    public decimal CurrentStock { get; set; }
    public decimal MinimumStock { get; set; }
    public decimal Shortage { get; set; } // CurrentStock - MinimumStock (âm nếu thiếu)
    public string Severity { get; set; } = null!; // Critical, Warning, Low
}

/// <summary>
/// Báo cáo doanh thu/lợi nhuận theo sản phẩm
/// </summary>
public class ProductProfitReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid BranchId { get; set; }
    public string? BranchName { get; set; }
    public string SortBy { get; set; } = "revenue"; // revenue, profit, quantity

    public List<ProductProfitItemDto> Items { get; set; } = new();

    // Tổng cộng
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal AverageProfitMargin { get; set; }
    public int TotalQuantitySold { get; set; }
}

public class ProductProfitItemDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public string CategoryName { get; set; } = null!;

    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; } // Revenue - Cost
    public decimal ProfitMargin { get; set; } // (Profit / Revenue) * 100
}

/// <summary>
/// Trang chi tiết nhà cung cấp - Tổng hợp
/// </summary>
public class SupplierDetailReportDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = null!;
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? GroupName { get; set; }

    // Thông tin công nợ tổng cộng (tất cả thời gian)
    public SupplierDebtInfoDto DebtInfo { get; set; } = new();

    // Thông tin công nợ theo tháng (nếu có lọc)
    public List<MonthlyDebtInfoDto> MonthlyDebts { get; set; } = new();

    // Lịch sử mua hàng (phiếu nhập)
    public List<PurchaseHistoryItemDto> PurchaseHistory { get; set; } = new();
    public int TotalPurchaseOrders { get; set; }
    public decimal TotalPurchaseAmount { get; set; }

    // Lịch sử thanh toán
    public List<SupplierPaymentHistoryItemDto> PaymentHistory { get; set; } = new();
    public int TotalPayments { get; set; }
    public decimal TotalPaidAmount { get; set; }
}

/// <summary>
/// Thông tin công nợ theo tháng
/// </summary>
public class MonthlyDebtInfoDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = null!;
    public decimal TotalPurchase { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal RemainingDebt { get; set; }
    public int OrderCount { get; set; }
}

public class SupplierDebtInfoDto
{
    public decimal TotalDebt { get; set; } // Tổng nợ hiện tại
    public decimal TotalPaid { get; set; } // Tổng đã thanh toán
    public decimal RemainingDebt { get; set; } // Còn lại
    public int PendingOrders { get; set; } // Số đơn chưa thanh toán
}

public class PurchaseHistoryItemDto
{
    public Guid PurchaseOrderId { get; set; }
    public string OrderCode { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = null!;
}

public class SupplierPaymentHistoryItemDto
{
    public Guid PaymentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

#endregion

#region Request DTOs

public class DailyEndReportRequestDto
{
    public DateTime Date { get; set; } = DateTime.UtcNow.Date;
}

public class StockMovementReportRequestDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid? CategoryId { get; set; } // Lọc theo nhóm sản phẩm (giữ tương thích cũ)
    public string? CategoryName { get; set; } // Lọc theo tên nhóm sản phẩm từ UI
    public Guid? ProductId { get; set; } // Lọc theo sản phẩm
}

public class RevenueByTimeRequestDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string TimeGrouping { get; set; } = "day"; // day, week, month
}

public class LowStockReportRequestDto
{
    public bool IncludeZeroStock { get; set; } = true; // Bao gồm cả sản phẩm tồn = 0
}

public class ProductProfitReportRequestDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public Guid? CategoryId { get; set; } // Ưu tiên filter theo categoryId (Guid)
    public string? CategoryName { get; set; } // Fallback filter theo tên nhóm sản phẩm từ UI
    public string SortBy { get; set; } = "revenue"; // revenue, profit, quantity
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class SupplierDetailRequestDto
{
    public Guid SupplierId { get; set; }
    public int? Year { get; set; } // Năm để lọc (null = tất cả)
    public int? Month { get; set; } // Tháng để lọc (1-12, null = tất cả)
    // Date range filters (thêm theo plan)
    public DateTime? PurchaseFromDate { get; set; }
    public DateTime? PurchaseToDate { get; set; }
    public DateTime? PaymentFromDate { get; set; }
    public DateTime? PaymentToDate { get; set; }
}

#endregion
