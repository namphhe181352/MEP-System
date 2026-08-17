using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using InventoryServiceNS = MEP_Management_System.Services.InventoryService;

namespace MEP_Management_System.Controllers.InventoryController
{
    /// <summary>
    /// Inventory Dashboard & Reports.
    /// Route gốc [Route("inventory")] được override bằng template tuyệt đối (bắt đầu bằng "/")
    /// để khớp đúng URL FE:
    ///   - /inventory/dashboard            (KHÔNG có /api)
    ///   - /api/inventory/low-stock-alerts (CÓ /api)
    ///   - /api/inventory/stock-report
    ///   - /api/inventory/movement-report
    ///   - /api/inventory/import-suggestions
    /// </summary>
    [ApiController]
    [Route("inventory")]
    [Tags("Tổng kho - Dashboard & Báo cáo")]
    [Authorize]
    [HasPermission(PermissionConstants.STOCK_VIEW)]
    public class InventoryDashboardController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly ICurrentBranchAccessor _currentBranch;
        private readonly IReportService _reportService;
        private readonly InventoryServiceNS.IInventoryReportService _inventoryReportService;

        public InventoryDashboardController(
            MEPDatabaseContext context,
            ICurrentBranchAccessor currentBranch,
            IReportService reportService,
            InventoryServiceNS.IInventoryReportService inventoryReportService)
        {
            _context = context;
            _currentBranch = currentBranch;
            _reportService = reportService;
            _inventoryReportService = inventoryReportService;
        }

        private Guid? ResolveBranchId(Guid? requested) => requested ?? _currentBranch.GetBranchId();

        // ===== /inventory/dashboard =====
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard([FromQuery] Guid? branchId)
        {
            var resolvedBranchId = ResolveBranchId(branchId);

            var items = await _context.BranchProducts
                .Include(bp => bp.Product)
                .Where(bp => resolvedBranchId == null || bp.BranchId == resolvedBranchId)
                .Select(bp => new
                {
                    bp.BranchId,
                    bp.ProductId,
                    bp.CostPrice,
                    bp.ActualStock,
                    bp.MinimumStock,
                    ProductCode = bp.Product.ProductCode,
                    ProductName = bp.Product.ProductName,
                    CategoryName = bp.Product.CategoryName,
                    Unit = bp.Product.BaseUnit,
                    bp.IsActive
                })
                .ToListAsync();

            var activeItems = items.Where(i => i.IsActive != 0).ToList();
            var activeProducts = activeItems.GroupBy(i => i.ProductId).ToList();

            var totalQuantity = activeProducts.Sum(g => g.Sum(i => i.ActualStock));
            var totalValue = activeProducts.Sum(g => g.Sum(i => i.ActualStock * i.CostPrice));

            var lowStockGroups = activeProducts
                .Where(g => g.Any(i => i.MinimumStock.HasValue && i.ActualStock <= i.MinimumStock.Value))
                .ToList();
            var outOfStockGroups = activeProducts.Where(g => g.Sum(i => i.ActualStock) <= 0).ToList();

            // Biểu đồ biến động kho 14 ngày gần nhất (theo InventoryLedger)
            var today = DateTime.Today;
            var fromChart = today.AddDays(-13);

            var ledgerQuery = _context.InventoryLedgers.AsQueryable();
            if (resolvedBranchId.HasValue)
                ledgerQuery = ledgerQuery.Where(l => l.BranchId == resolvedBranchId.Value);

            var ledgerGroups = await ledgerQuery
                .Where(l => l.TransactionTime >= fromChart)
                .GroupBy(l => l.TransactionTime.Date)
                .Select(g => new { Date = g.Key, Inward = g.Sum(x => x.QtyChange > 0 ? x.QtyChange : 0), Outward = g.Sum(x => x.QtyChange < 0 ? -x.QtyChange : 0) })
                .ToDictionaryAsync(k => k.Date, v => v);

            var chart = new List<InventoryMovementChartPointDto>();
            for (var d = fromChart; d <= today; d = d.AddDays(1))
            {
                ledgerGroups.TryGetValue(d, out var point);
                chart.Add(new InventoryMovementChartPointDto
                {
                    Date = d.ToString("yyyy-MM-dd"),
                    Inward = point?.Inward ?? 0,
                    Outward = point?.Outward ?? 0
                });
            }

            // Top sản phẩm tồn thấp nổi bật (lấy 10, sắp theo thiếu hụt tăng dần)
            var topLow = lowStockGroups
                .SelectMany(g => g)
                .GroupBy(i => i.ProductId)
                .Select(g => new
                {
                    g.Key,
                    ProductCode = g.First().ProductCode,
                    ProductName = g.First().ProductName,
                    CategoryName = g.First().CategoryName,
                    Unit = g.First().Unit,
                    CurrentStock = g.Sum(i => i.ActualStock),
                    MinimumStock = g.Max(i => i.MinimumStock ?? 0)
                })
                .OrderBy(x => x.CurrentStock - x.MinimumStock)
                .Take(10)
                .ToList();

            var result = new InventoryDashboardDto
            {
                BranchId = resolvedBranchId,
                TotalProducts = activeProducts.Count,
                TotalStockQuantity = totalQuantity,
                TotalStockValue = totalValue,
                LowStockCount = lowStockGroups.Count,
                OutOfStockCount = outOfStockGroups.Count,
                AsOfDate = DateTime.UtcNow,
                MovementChart = chart,
                LowStockAlerts = topLow.Select(x => new LowStockAlertDto
                {
                    ProductId = x.Key,
                    ProductCode = x.ProductCode ?? "",
                    ProductName = x.ProductName ?? "",
                    CategoryName = x.CategoryName,
                    Unit = x.Unit,
                    CurrentStock = x.CurrentStock,
                    MinimumStock = x.MinimumStock,
                    Shortage = x.CurrentStock - x.MinimumStock,
                    Severity = x.CurrentStock == 0 ? "Critical" : (x.CurrentStock <= x.MinimumStock * 0.5m ? "Critical" : "Warning")
                }).ToList()
            };

            return Ok(new ApiResponse<InventoryDashboardDto> { Success = true, Message = "Dashboard tổng kho đã được tải.", Data = result });
        }

        // ===== /api/inventory/low-stock-alerts =====
        [HttpGet("/api/inventory/low-stock-alerts")]
        [HasPermission(PermissionConstants.STOCK_VIEW)]
        public async Task<IActionResult> GetLowStockAlerts()
        {
            var report = await _reportService.GetLowStockReportAsync(new LowStockReportRequestDto { IncludeZeroStock = true }, User);

            List<Guid> productIds = report.Items.Select(i => i.ProductId).ToList();
            var units = await _context.Products
                .Where(p => productIds.Contains(p.ProductId))
                .ToDictionaryAsync(p => p.ProductId, p => p.BaseUnit);

            var alerts = report.Items.Select(i => new LowStockAlertDto
            {
                ProductId = i.ProductId,
                ProductCode = i.ProductCode,
                ProductName = i.ProductName,
                CategoryName = i.CategoryName,
                Unit = units.TryGetValue(i.ProductId, out var u) ? u : null,
                CurrentStock = i.CurrentStock,
                MinimumStock = i.MinimumStock,
                Shortage = i.Shortage,
                Severity = i.Severity
            }).ToList();

            return Ok(new ApiResponse<List<LowStockAlertDto>> { Success = true, Message = "Danh sách sản phẩm tồn thấp đã được tải.", Data = alerts });
        }

        // ===== /api/inventory/stock-report =====
        [HttpGet("/api/inventory/stock-report")]
        [HasPermission(PermissionConstants.STOCK_VIEW)]
        public async Task<IActionResult> GetStockReport(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] Guid? branchId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50)
        {
            var from = fromDate ?? DateTime.Today.AddDays(-30);
            var to = toDate ?? DateTime.Today.AddDays(1);
            var resolvedBranchId = ResolveBranchId(branchId);

            var summary = await _inventoryReportService.GetInventorySummaryAsync(new InventorySummaryRequest
            {
                FromDate = from,
                ToDate = to,
                BranchId = resolvedBranchId,
                AggregateBranches = true,
                OnlyHasTransactions = false
            });

            var items = summary.Items
                .Select(i => new StockReportItemDto
                {
                    ProductCode = i.ProductCode,
                    ProductName = i.ProductName,
                    UnitName = i.UnitName,
                    OpeningQuantity = i.OpeningQuantity,
                    OpeningValue = i.OpeningValue,
                    InQuantity = i.InQuantity,
                    InValue = i.InValue,
                    OutQuantity = i.OutQuantity,
                    OutValue = i.OutValue,
                    ClosingQuantity = i.ClosingQuantity,
                    ClosingValue = i.ClosingValue
                })
                .ToList();

            pageNumber = Math.Max(pageNumber, 1);
            pageSize = Math.Clamp(pageSize, 1, 500);

            var paged = items.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            var result = new StockReportDto
            {
                FromDate = from,
                ToDate = to,
                BranchId = resolvedBranchId,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = items.Count,
                Items = paged,
                TotalOpeningValue = summary.TotalOpeningValue,
                TotalInValue = summary.TotalInValue,
                TotalOutValue = summary.TotalOutValue,
                TotalClosingValue = summary.TotalClosingValue
            };

            return Ok(new ApiResponse<StockReportDto> { Success = true, Message = "Báo cáo tồn kho đã được tải.", Data = result });
        }

        // ===== /api/inventory/movement-report =====
        [HttpGet("/api/inventory/movement-report")]
        [HasPermission(PermissionConstants.STOCK_VIEW)]
        public async Task<IActionResult> GetMovementReport(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] Guid? categoryId,
            [FromQuery] Guid? productId,
            [FromQuery] string? categoryName)
        {
            var report = await _reportService.GetStockMovementFromLedgerAsync(new StockMovementReportRequestDto
            {
                FromDate = fromDate ?? DateTime.Today.AddDays(-30),
                ToDate = toDate ?? DateTime.Today.AddDays(1).AddTicks(-1),
                CategoryId = categoryId,
                CategoryName = categoryName,
                ProductId = productId
            }, User);

            return Ok(new ApiResponse<StockMovementReportDto> { Success = true, Message = "Báo cáo biến động kho đã được tải.", Data = report });
        }

        // ===== /api/inventory/import-suggestions =====
        [HttpGet("/api/inventory/import-suggestions")]
        [HasPermission(PermissionConstants.STOCK_VIEW)]
        public async Task<IActionResult> GetImportSuggestions([FromQuery] Guid? branchId, [FromQuery] int limit = 50)
        {
            var resolvedBranchId = ResolveBranchId(branchId);

            var rows = await _context.BranchProducts
                .Include(bp => bp.Product)
                    .ThenInclude(p => p.Supplier)
                .Where(bp => (resolvedBranchId == null || bp.BranchId == resolvedBranchId)
                    && bp.IsActive == 1
                    && (bp.Product.Status == null || bp.Product.Status != -1))
                .Select(bp => new
                {
                    bp.ProductId,
                    bp.ActualStock,
                    bp.MinimumStock,
                    bp.MaximumStock,
                    ProductCode = bp.Product.ProductCode,
                    ProductName = bp.Product.ProductName,
                    CategoryName = bp.Product.CategoryName,
                    Unit = bp.Product.BaseUnit,
                    bp.Product.SupplierId,
                    SupplierName = bp.Product.Supplier != null ? bp.Product.Supplier.SupplierName : null
                })
                .ToListAsync();

            var suggestions = new List<ImportSuggestionDto>();
            foreach (var group in rows.GroupBy(r => r.ProductId))
            {
                var first = group.First();
                var currentStock = group.Sum(r => r.ActualStock);
                var minStock = group.Max(r => r.MinimumStock) ?? 0;
                var maxStock = group.Max(r => r.MaximumStock);

                string reason;
                decimal suggested;

                if (currentStock == 0)
                {
                    reason = "Hết hàng";
                    suggested = maxStock.HasValue ? maxStock.Value : (minStock > 0 ? minStock : 0);
                }
                else if (minStock > 0 && currentStock <= minStock)
                {
                    reason = "Tồn dưới mức tối thiểu";
                    var target = maxStock.HasValue ? maxStock.Value : minStock * 2;
                    suggested = Math.Max(target - currentStock, minStock - currentStock);
                }
                else if (maxStock.HasValue && currentStock < maxStock.Value)
                {
                    reason = "Sắp chạm ngưỡng tồn tối đa";
                    suggested = maxStock.Value - currentStock;
                }
                else
                {
                    continue; // Không cần nhập
                }

                if (suggested <= 0) continue;

                suggestions.Add(new ImportSuggestionDto
                {
                    ProductId = group.Key,
                    ProductCode = first.ProductCode ?? "",
                    ProductName = first.ProductName ?? "",
                    CategoryName = first.CategoryName,
                    Unit = first.Unit,
                    SupplierId = first.SupplierId,
                    SupplierName = first.SupplierName,
                    CurrentStock = currentStock,
                    MinimumStock = minStock,
                    MaximumStock = maxStock ?? 0,
                    SuggestedQuantity = suggested,
                    Reason = reason
                });
            }

            var limited = suggestions
                .OrderByDescending(s => s.SuggestedQuantity)
                .Take(Math.Max(limit, 1))
                .ToList();

            return Ok(new ApiResponse<List<ImportSuggestionDto>> { Success = true, Message = "Gợi ý nhập hàng đã được tải.", Data = limited });
        }
    }
}