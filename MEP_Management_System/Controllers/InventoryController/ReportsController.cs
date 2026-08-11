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
            var result = await _reportService.GetStockMovementFromLedgerAsync(request, User);

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
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = ex.Message,
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

    /// <summary>
    /// Thẻ kho chi tiết (Drill-down)
    /// </summary>
    [HttpGet("stock-ledger/{branchProductId}")]
    public async Task<IActionResult> GetStockLedger(Guid branchProductId, [FromQuery] StockLedgerRequestDto request)
    {
        try
        {
            var result = await _reportService.GetStockLedgerAsync(branchProductId, request, User);

            return Ok(new ApiResponse<StockLedgerDto>
            {
                Success = true,
                Message = "Thẻ kho chi tiết đã được tải.",
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
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = ex.Message,
                Errors = new() { ex.Message }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi tải thẻ kho.",
                Errors = new() { ex.Message }
            });
        }
    }

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

    /// <summary>
    /// GET /api/reports/stock-ledger/{branchProductId}
    /// Thẻ kho chi tiết - lịch sử biến động của 1 sản phẩm trong khoảng thời gian.
    /// </summary>
    [HttpGet("stock-ledger/{branchProductId:guid}")]
    public async Task<IActionResult> GetStockLedger(
        Guid branchProductId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        var branchId = await ResolveBranchIdAsync();
        if (branchId == null)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

        var from = fromDate?.Date ?? DateTime.UtcNow.AddDays(-30).Date;
        var to = (toDate?.Date ?? DateTime.UtcNow.Date).AddDays(1).AddTicks(-1);

        var bp = await _context.BranchProducts
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.BranchProductId == branchProductId && x.BranchId == branchId);

        if (bp == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy sản phẩm trong kho." });

        // === Bước 1: Tính Tồn đầu kỳ (trước fromDate) ===
        decimal openingStock = 0;
        var ledgerBefore = await _context.InventoryLedgers
            .Where(l => l.BranchProductId == branchProductId && l.BranchId == branchId && l.TransactionTime < from)
            .SumAsync(l => (decimal?)l.QtyChange) ?? 0;

        // === Bước 2: Lấy giao dịch TRONG KỲ (fromDate -> toDate) ===
        var entries = new List<(DateTime Time, string Doc, string Type, decimal Price, decimal Qty)>();
        DateTime safeDt(DateTime? d) => d ?? DateTime.UtcNow;

        var ledgerIn = await _context.InventoryLedgers
            .Where(l => l.BranchProductId == branchProductId && l.BranchId == branchId
                && l.TransactionTime >= from && l.TransactionTime <= to)
            .OrderBy(l => l.TransactionTime)
            .Select(l => new { l.TransactionTime, l.DocCode, l.DocType, l.UnitCostPrice, l.QtyChange })
            .ToListAsync();

        if (ledgerIn.Count > 0)
        {
            foreach (var e in ledgerIn)
                entries.Add((e.TransactionTime, e.DocCode ?? "", e.DocType, e.UnitCostPrice, e.QtyChange));

            // Ton tai thoi diem toDate = ton kho hien tai - bien dong SAU toDate
            var qtyAfterToDate = await _context.InventoryLedgers
                .Where(l => l.BranchProductId == branchProductId && l.BranchId == branchId && l.TransactionTime > to)
                .SumAsync(l => (decimal?)l.QtyChange) ?? 0;

            var stockAtToDate = bp.ActualStock - qtyAfterToDate;
            var inPeriodTotal = entries.Sum(e => e.Qty);
            openingStock = stockAtToDate - inPeriodTotal;
        }
        else
        {
            // Fallback: dung ledger truoc ky neu co, hoac tinh tu StockTicket + Invoice
            openingStock = ledgerBefore;

            if (ledgerBefore == 0 && await _context.InventoryLedgers.CountAsync() == 0)
            {
                var stBefore = await _context.StockTicketItems
                    .Where(sti => sti.BranchProductId == branchProductId && sti.StockTicket.BranchId == branchId
                        && sti.StockTicket.Status == "COMPLETED" && sti.StockTicket.CreatedAt < from)
                    .SumAsync(sti => (decimal?)(sti.StockTicket.TicketType == "PURCHASE" || sti.StockTicket.TicketType == "CUSTOMER_RETURN" ? sti.Discrepancy : -sti.Discrepancy)) ?? 0;

                var invBefore = await _context.InvoiceItems
                    .Where(ii => ii.BranchProductId == branchProductId && ii.Invoice.Order.BranchId == branchId
                        && ii.Invoice.Status == "Completed" && ii.Invoice.CreatedAt < from)
                    .SumAsync(ii => (decimal?)(-(ii.Quantity * (ii.ConvertValue ?? 1m)))) ?? 0;

                openingStock = stBefore + invBefore;
            }

            // Fallback entries tu StockTicket + Invoice
            var stIn = await _context.StockTicketItems
                .Where(sti => sti.BranchProductId == branchProductId && sti.StockTicket.BranchId == branchId
                    && sti.StockTicket.Status == "COMPLETED"
                    && sti.StockTicket.CreatedAt >= from && sti.StockTicket.CreatedAt <= to)
                .OrderBy(sti => sti.StockTicket.CreatedAt)
                .Select(sti => new { Time = sti.StockTicket.CreatedAt, Doc = sti.StockTicket.TicketCode ?? "", Type = sti.StockTicket.TicketType ?? "", Price = sti.UnitPrice ?? sti.BranchProduct.CostPrice, Qty = sti.StockTicket.TicketType == "PURCHASE" || sti.StockTicket.TicketType == "CUSTOMER_RETURN" ? sti.Discrepancy : -sti.Discrepancy })
                .ToListAsync();
            foreach (var e in stIn) entries.Add((safeDt(e.Time), e.Doc, e.Type, e.Price, e.Qty));

            var invIn = await _context.InvoiceItems
                .Where(ii => ii.BranchProductId == branchProductId && ii.Invoice.Order.BranchId == branchId
                    && ii.Invoice.Status == "Completed"
                    && ii.Invoice.CreatedAt >= from && ii.Invoice.CreatedAt <= to)
                .OrderBy(ii => ii.Invoice.CreatedAt)
                .Select(ii => new { Time = ii.Invoice.CreatedAt, Doc = ii.Invoice.InvoiceCode ?? "", Price = ii.CostPrice > 0 ? ii.CostPrice : ii.BranchProduct.CostPrice, Qty = -(ii.Quantity * (ii.ConvertValue ?? 1m)) })
                .ToListAsync();
            foreach (var e in invIn) entries.Add((safeDt(e.Time), e.Doc, "SALE", e.Price, e.Qty));

            entries = entries.OrderBy(e => e.Time).ToList();
        }

        // === Bước 3: Tính running stock + build response ===
        var running = openingStock;
        var resultEntries = entries.Select(e =>
        {
            running += e.Qty;
            return new
            {
                TransactionTime = e.Time,
                DocCode = e.Doc,
                DocType = e.Type,
                UnitCostPrice = e.Price,
                QtyIn = e.Qty > 0 ? e.Qty : 0m,
                QtyOut = e.Qty < 0 ? -e.Qty : 0m,
                StockAfter = running
            };
        }).ToList();

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Lấy thẻ kho thành công.",
            Data = new
            {
                productCode = bp.Product?.ProductCode ?? "",
                productName = bp.Product?.ProductName ?? "",
                unit = bp.Product?.BaseUnit ?? "",
                fromDate = from,
                toDate = to,
                openingStock,
                entries = resultEntries,
                totalInQty = resultEntries.Sum(e => e.QtyIn),
                totalOutQty = resultEntries.Sum(e => e.QtyOut),
                closingStock = running
            }
        });
    }

    /// <summary>
    /// POST /api/reports/snapshot-daily
    /// Chốt tồn kho cuối ngày - lưu closing_quantity, cost_price, closing_value cho toàn bộ sản phẩm.
    /// </summary>
    [HttpPost("snapshot-daily")]
    public async Task<IActionResult> CreateDailySnapshot([FromQuery] Guid? branchIdOverride)
    {
        var branchId = (IsOwner() ? branchIdOverride : null) ?? await ResolveBranchIdAsync();
        if (branchId == null)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Xóa snapshot cũ nếu có (cùng ngày + branch)
        var existing = await _context.DailyStockSnapshots
            .Where(s => s.SnapshotDate == today && s.BranchId == branchId.Value)
            .ToListAsync();
        _context.DailyStockSnapshots.RemoveRange(existing);

        var branchProducts = await _context.BranchProducts
            .Where(bp => bp.BranchId == branchId.Value && bp.IsActive != 0)
            .ToListAsync();

        var snapshots = branchProducts.Select(bp => new DailyStockSnapshot
        {
            SnapshotId = Guid.NewGuid(),
            SnapshotDate = today,
            BranchId = branchId.Value,
            BranchProductId = bp.BranchProductId,
            ClosingQuantity = bp.ActualStock,
            CostPrice = Math.Round(bp.CostPrice, 4),
            ClosingValue = Math.Round(bp.ActualStock * bp.CostPrice, 4),
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.DailyStockSnapshots.AddRange(snapshots);
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = $"Đã chốt tồn kho ngày {today:dd/MM/yyyy} - {snapshots.Count} sản phẩm.",
            Data = new { snapshotDate = today, totalProducts = snapshots.Count }
        });
    }

    #endregion
}
