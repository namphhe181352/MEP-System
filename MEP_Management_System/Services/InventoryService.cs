using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.InventoryService
{
    public interface IInventoryReportService
    {
        Task<InventorySummaryResponse> GetInventorySummaryAsync(InventorySummaryRequest request);
    }

    public class InventoryReportService : IInventoryReportService
    {
        private readonly MEPDatabaseContext _context;

        public InventoryReportService(MEPDatabaseContext context)
        {
            _context = context;
        }

        public Task<InventorySummaryResponse> GetInventorySummaryAsync(InventorySummaryRequest request)
        {
            // Thiết lập mốc thời gian chuẩn hóa ngày giờ đầu/cuối ngày
            var fromDate = request.FromDate.Kind == DateTimeKind.Utc ? request.FromDate.ToLocalTime() : request.FromDate;
            var toDate = request.ToDate.Kind == DateTimeKind.Utc ? request.ToDate.ToLocalTime() : request.ToDate;

            // 1. Lấy dữ liệu Nhập kho từ PurchaseOrders (Trạng thái COMPLETED/RECEIVED)
            var inwardData = _context.PurchaseOrderItems
            .Where(poi => poi.PurchaseOrder.CreatedAt >= fromDate && poi.PurchaseOrder.CreatedAt <= toDate
                      && (poi.PurchaseOrder.Status == "COMPLETED" || poi.PurchaseOrder.Status == "RECEIVED"))
            .Select(poi => new
            {
                poi.ProductId,
                poi.Quantity,
                Value = poi.Quantity * poi.UnitPrice
            })
            .GroupBy(x => x.ProductId)
             .ToDictionary(g => g.Key, g => new { Qty = g.Sum(x => x.Quantity), Val = g.Sum(x => x.Value) });

            // 2. Lấy dữ liệu Xuất kho từ Orders (Trạng thái PAID/COMPLETED)
            // 2. Lấy dữ liệu Xuất kho từ Orders
            var outwardData = _context.OrderItems
                .Where(oi => oi.Order.CreatedAt >= fromDate && oi.Order.CreatedAt <= toDate
                          && (oi.Order.Status == "PAID" || oi.Order.Status == "COMPLETED")
                          && (request.BranchId == null || oi.Order.BranchId == request.BranchId))
                .Select(oi => new
                {
                    oi.BranchProduct.ProductId,
                    oi.Quantity,
                    Value = oi.Quantity * oi.BranchProduct.CostPrice // <-- Bỏ ?? 0 ở đây
                })
                .GroupBy(x => x.ProductId)
                .ToDictionary(g => g.Key, g => new { Qty = g.Sum(x => x.Quantity), Val = g.Sum(x => x.Value) }); // <-- Bỏ ?? 0M ở đây

            // 3. Lấy danh sách sản phẩm và snapshot số lượng tồn kho hiện tại để đối chiếu làm mốc kết toán
            var productsQuery = _context.BranchProducts
            .Where(bp => request.BranchId == null || bp.BranchId == request.BranchId)
            .Include(bp => bp.Product)
            .AsQueryable();

            var branchProducts = productsQuery.ToList();

            var reportItems = branchProducts
            .GroupBy(bp => bp.ProductId)
            .Select(g =>
            {
                var firstBp = g.First();
                var prodId = g.Key;
                var costPrice = firstBp.CostPrice;

                // Tính toán lượng nhập/xuất trong kỳ cho sản phẩm này
                var inQty = inwardData.ContainsKey(prodId) ? inwardData[prodId].Qty : 0;
                var inVal = inwardData.ContainsKey(prodId) ? inwardData[prodId].Val : 0;

                var outQty = outwardData.ContainsKey(prodId) ? outwardData[prodId].Qty : 0;
                var outVal = outwardData.ContainsKey(prodId) ? outwardData[prodId].Val : 0;

                // Giả lập tính toán: Tồn cuối kỳ hiện tại lấy từ cơ sở dữ liệu làm mốc tham chiếu
                var closingQty = g.Sum(bp => bp.ActualStock);
                var closingVal = closingQty * costPrice;

                // Công thức tính ngược dòng: Tồn đầu kỳ = Tồn cuối kỳ - Nhập kho + Xuất kho
                var openingQty = closingQty - inQty + outQty;
                var openingValue = openingQty * costPrice;

                return new InventorySummaryItemDto
                {
                    ProductCode = firstBp.Product.ProductCode,
                    ProductName = firstBp.Product.ProductName,
                    UnitName = firstBp.Product.BaseUnit ?? "Cái",
                    OpeningQuantity = openingQty,
                    OpeningValue = openingValue,
                    InQuantity = inQty,
                    InValue = inVal,
                    OutQuantity = outQty,
                    OutValue = outVal,
                    ClosingQuantity = closingQty,
                    ClosingValue = closingVal
                };
            })
            .ToList();

            // Lọc nâng cao: "Chỉ hàng có phát sinh" (Nếu tích chọn checkbox)
            if (request.OnlyHasTransactions)
            {
                reportItems = reportItems.Where(x => x.InQuantity > 0 || x.OutQuantity > 0).ToList();
            }

            // Đóng gói kết quả trả về kèm theo tổng kết dòng cuối bảng báo cáo
            var response = new InventorySummaryResponse
            {
                Items = reportItems,
                TotalOpeningValue = reportItems.Sum(x => x.OpeningValue),
                TotalInValue = reportItems.Sum(x => x.InValue),
                TotalOutValue = reportItems.Sum(x => x.OutValue),
                TotalClosingValue = reportItems.Sum(x => x.ClosingValue)
            };

            return Task.FromResult(response);
        }
    }
}