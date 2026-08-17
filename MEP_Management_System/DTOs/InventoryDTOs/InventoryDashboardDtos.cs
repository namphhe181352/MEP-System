using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.InventoryDTOs
{
    /// <summary>Tổng quan kho - trả về cho GET /inventory/dashboard.</summary>
    public class InventoryDashboardDto
    {
        public Guid? BranchId { get; set; }
        public string? BranchName { get; set; }

        /// <summary>Tổng số sản phẩm đang kinh doanh trong kho.</summary>
        public int TotalProducts { get; set; }

        /// <summary>Tổng số lượng tồn kho (đơn vị tổng hợp).</summary>
        public decimal TotalStockQuantity { get; set; }

        /// <summary>Tổng giá trị tồn kho (theo giá vốn CostPrice).</summary>
        public decimal TotalStockValue { get; set; }

        /// <summary>Số sản phẩm đang dưới ngưỡng tồn tối thiểu.</summary>
        public int LowStockCount { get; set; }

        /// <summary>Số sản phẩm hết hàng (tồn = 0).</summary>
        public int OutOfStockCount { get; set; }

        public DateTime AsOfDate { get; set; } = DateTime.UtcNow;

        /// <summary>Biểu đồ biến động kho theo ngày (14 ngày gần nhất).</summary>
        public List<InventoryMovementChartPointDto> MovementChart { get; set; } = new();

        /// <summary>Danh sách (rút gọn) sản phẩm tồn thấp nổi bật.</summary>
        public List<LowStockAlertDto> LowStockAlerts { get; set; } = new();
    }

    public class InventoryMovementChartPointDto
    {
        /// <summary>Ngày dạng yyyy-MM-dd.</summary>
        public string Date { get; set; } = string.Empty;
        /// <summary>Tổng lượng nhập trong ngày.</summary>
        public decimal Inward { get; set; }
        /// <summary>Tổng lượng xuất trong ngày (số dương).</summary>
        public decimal Outward { get; set; }
    }

    /// <summary>Một sản phẩm tồn thấp - GET /api/inventory/low-stock-alerts.</summary>
    public class LowStockAlertDto
    {
        public Guid ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public string? Unit { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal MinimumStock { get; set; }
        /// <summary>CurrentStock - MinimumStock (âm nếu thiếu).</summary>
        public decimal Shortage { get; set; }
        /// <summary>Critical / Warning / Low.</summary>
        public string Severity { get; set; } = string.Empty;
    }

    /// <summary>Báo cáo tồn kho - GET /api/inventory/stock-report.</summary>
    public class StockReportDto
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public Guid? BranchId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalCount { get; set; }

        public List<StockReportItemDto> Items { get; set; } = new();

        // Tổng kết toàn bộ (không phụ thuộc phân trang)
        public decimal TotalOpeningValue { get; set; }
        public decimal TotalInValue { get; set; }
        public decimal TotalOutValue { get; set; }
        public decimal TotalClosingValue { get; set; }
    }

    public class StockReportItemDto
    {
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public decimal OpeningQuantity { get; set; }
        public decimal OpeningValue { get; set; }
        public decimal InQuantity { get; set; }
        public decimal InValue { get; set; }
        public decimal OutQuantity { get; set; }
        public decimal OutValue { get; set; }
        public decimal ClosingQuantity { get; set; }
        public decimal ClosingValue { get; set; }
    }

    /// <summary>Gợi ý nhập hàng - GET /api/inventory/import-suggestions.</summary>
    public class ImportSuggestionDto
    {
        public Guid ProductId { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public string? Unit { get; set; }
        public Guid? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal MinimumStock { get; set; }
        public decimal MaximumStock { get; set; }
        /// <summary>Số lượng đề xuất nên nhập thêm.</summary>
        public decimal SuggestedQuantity { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}