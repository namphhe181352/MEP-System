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

    public ReportsController(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch)
    {
        _context = context;
        _currentBranch = currentBranch;
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
    /// </summary>
    [HttpPost("daily-end")]
    public async Task<IActionResult> GetDailyEndReport([FromBody] DailyEndReportRequestDto request)
    {
        try
        {
            var branchId = await ResolveBranchIdAsync(request.BranchId);
            if (branchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy thông tin chi nhánh.",
                    Errors = new() { "Không xác định được chi nhánh từ user hiện tại." }
                });
            }

            var reportDate = request.Date.Date;
            var nextDate = reportDate.AddDays(1);

            // Lấy thông tin chi nhánh
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);

            // 1. Lấy đơn hàng trong ngày
            var orders = await _context.Orders
                .Where(o => o.BranchId == branchId.Value
                    && o.Status == "Completed"
                    && o.CreatedAt >= reportDate
                    && o.CreatedAt < nextDate)
                .ToListAsync();

            // 2. Lấy thanh toán trong ngày
            var payments = await _context.Payments
                .Where(p => p.BranchId == branchId.Value
                    && p.Status == "Confirmed"
                    && p.CreatedAt >= reportDate
                    && p.CreatedAt < nextDate)
                .ToListAsync();

            // 3. Lấy phiếu nhập kho trong ngày (PURCHASE, CUSTOMER_RETURN, BALANCE_ADJUST)
            var inwardTickets = await _context.StockTickets
                .Where(st => st.BranchId == branchId.Value
                    && st.Status == "COMPLETED"
                    && (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST")
                    && st.CreatedAt >= reportDate
                    && st.CreatedAt < nextDate)
                .CountAsync();

            // 4. Lấy phiếu xuất kho trong ngày (RETURN_SUPPLIER, WRITE_OFF, TRANSFER)
            var outwardTickets = await _context.StockTickets
                .Where(st => st.BranchId == branchId.Value
                    && st.Status == "COMPLETED"
                    && (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER")
                    && st.CreatedAt >= reportDate
                    && st.CreatedAt < nextDate)
                .CountAsync();

            // Tính toán breakdown thanh toán
            var paymentBreakdown = new PaymentMethodBreakdown
            {
                CashAmount = payments.Where(p => p.Method == "Cash").Sum(p => p.Amount),
                CashCount = payments.Count(p => p.Method == "Cash"),
                TransferAmount = payments.Where(p => p.Method == "Transfer").Sum(p => p.Amount),
                TransferCount = payments.Count(p => p.Method == "Transfer"),
                DebtAmount = 0,
                DebtCount = 0,
                CombinedAmount = payments.Where(p => p.Method == "Combined").Sum(p => p.Amount),
                CombinedCount = payments.Count(p => p.Method == "Combined")
            };

            var result = new DailyEndReportDto
            {
                ReportDate = reportDate,
                BranchId = branchId.Value,
                BranchName = branch?.BranchName,
                TotalRevenue = orders.Sum(o => o.TotalAmount),
                TotalOrders = orders.Count,
                AverageOrderValue = orders.Count > 0 ? orders.Sum(o => o.TotalAmount) / orders.Count : 0,
                PaymentBreakdown = paymentBreakdown,
                InwardTicketCount = inwardTickets,
                OutwardTicketCount = outwardTickets
            };

            return Ok(new ApiResponse<DailyEndReportDto>
            {
                Success = true,
                Message = "Báo cáo cuối ngày đã được tải.",
                Data = result
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
            var branchId = await ResolveBranchIdAsync(request.BranchId);
            if (branchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy thông tin chi nhánh.",
                    Errors = new() { "Không xác định được chi nhánh từ user hiện tại." }
                });
            }

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);

            var fromDate = request.FromDate.Date;
            var toDate = request.ToDate.Date.AddDays(1).AddTicks(-1);

            // Lấy danh sách sản phẩm trong kho (có filter category/product)
            var productsQuery = _context.BranchProducts
                .Include(bp => bp.Product)
                .Where(bp => bp.BranchId == branchId.Value);

            // Lọc theo nhóm sản phẩm nếu có
            if (!string.IsNullOrEmpty(request.CategoryId?.ToString()))
            {
                productsQuery = productsQuery.Where(bp => bp.Product != null && bp.Product.CategoryName == request.CategoryId.ToString());
            }

            // Lọc theo sản phẩm nếu có
            if (request.ProductId.HasValue)
            {
                productsQuery = productsQuery.Where(bp => bp.ProductId == request.ProductId.Value);
            }

            var products = await productsQuery.ToListAsync();

            // ===== TÍNH TỒN ĐẦU KỲ ĐÚNG CÁCH =====
            // Tồn đầu kỳ = Tổng nhập TRƯỚC kỳ - Tổng xuất TRƯỚC kỳ

            // Nhập TRƯỚC fromDate (đầu kỳ)
            var inwardBeforePeriod = await _context.StockTickets
                .Where(st => st.BranchId == branchId.Value
                    && st.Status == "COMPLETED"
                    && (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST")
                    && st.CreatedAt < fromDate)
                .SelectMany(st => st.StockTicketItems)
                .GroupBy(sti => sti.BranchProductId)
                .Select(g => new { BranchProductId = g.Key, TotalInward = g.Sum(sti => sti.Discrepancy) })
                .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalInward);

            // Xuất (bán hàng) TRƯỚC fromDate
            var salesBeforePeriod = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.BranchId == branchId.Value
                    && oi.Order.Status == "Completed"
                    && oi.Order.CreatedAt < fromDate)
                .GroupBy(oi => oi.BranchProductId)
                .Select(g => new { BranchProductId = g.Key, TotalOutward = g.Sum(oi => oi.Quantity) })
                .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalOutward);

            // Xuất kho (phiếu xuất) TRƯỚC fromDate
            var outwardTicketsBeforePeriod = await _context.StockTickets
                .Where(st => st.BranchId == branchId.Value
                    && st.Status == "COMPLETED"
                    && (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER")
                    && st.CreatedAt < fromDate)
                .SelectMany(st => st.StockTicketItems)
                .GroupBy(sti => sti.BranchProductId)
                .Select(g => new { BranchProductId = g.Key, TotalOutward = g.Sum(sti => sti.Discrepancy) })
                .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalOutward);

            // ===== TÍNH NHẬP/XUẤT TRONG KỲ =====
            // Nhập trong kỳ
            var inwardInPeriod = await _context.StockTickets
                .Where(st => st.BranchId == branchId.Value
                    && st.Status == "COMPLETED"
                    && (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST")
                    && st.CreatedAt >= fromDate
                    && st.CreatedAt <= toDate)
                .SelectMany(st => st.StockTicketItems)
                .GroupBy(sti => sti.BranchProductId)
                .Select(g => new { BranchProductId = g.Key, TotalInward = g.Sum(sti => sti.Discrepancy) })
                .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalInward);

            // Xuất (bán hàng) trong kỳ
            var salesInPeriod = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.BranchId == branchId.Value
                    && oi.Order.Status == "Completed"
                    && oi.Order.CreatedAt >= fromDate
                    && oi.Order.CreatedAt <= toDate)
                .GroupBy(oi => oi.BranchProductId)
                .Select(g => new { BranchProductId = g.Key, TotalOutward = g.Sum(oi => oi.Quantity) })
                .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalOutward);

            // Xuất kho trong kỳ
            var outwardTicketsInPeriod = await _context.StockTickets
                .Where(st => st.BranchId == branchId.Value
                    && st.Status == "COMPLETED"
                    && (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER")
                    && st.CreatedAt >= fromDate
                    && st.CreatedAt <= toDate)
                .SelectMany(st => st.StockTicketItems)
                .GroupBy(sti => sti.BranchProductId)
                .Select(g => new { BranchProductId = g.Key, TotalOutward = g.Sum(sti => sti.Discrepancy) })
                .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalOutward);

            var items = new List<StockMovementItemDto>();
            decimal totalOpeningValue = 0, totalInwardValue = 0, totalOutwardValue = 0, totalClosingValue = 0;

            foreach (var bp in products)
            {
                var branchProductId = bp.BranchProductId;

                // Tính tồn đầu kỳ đúng cách
                var inwardBefore = inwardBeforePeriod.GetValueOrDefault(branchProductId, 0);
                var salesBefore = salesBeforePeriod.GetValueOrDefault(branchProductId, 0);
                var outwardBefore = outwardTicketsBeforePeriod.GetValueOrDefault(branchProductId, 0);
                var openingStock = inwardBefore - (salesBefore + outwardBefore);

                // Tính nhập/xuất trong kỳ
                var inwardIn = inwardInPeriod.GetValueOrDefault(branchProductId, 0);
                var salesOut = salesInPeriod.GetValueOrDefault(branchProductId, 0);
                var outwardOut = outwardTicketsInPeriod.GetValueOrDefault(branchProductId, 0);
                var inwardInPeriod2 = inwardIn;
                var outwardInPeriod2 = salesOut + outwardOut;

                // Tồn cuối = Tồn đầu kỳ + Nhập trong kỳ - Xuất trong kỳ
                var closingStock = openingStock + inwardInPeriod2 - outwardInPeriod2;

                var openingValue = openingStock * bp.CostPrice;
                var inwardValue = inwardInPeriod2 * bp.CostPrice;
                var outwardValue = outwardInPeriod2 * bp.CostPrice;
                var closingValue = closingStock * bp.CostPrice;

                items.Add(new StockMovementItemDto
                {
                    ProductId = bp.ProductId,
                    ProductCode = bp.Product?.ProductCode ?? "",
                    ProductName = bp.Product?.ProductName ?? "",
                    CategoryName = bp.Product?.CategoryName ?? "",
                    BaseUnit = bp.Product?.BaseUnit ?? "",
                    OpeningStock = openingStock,
                    InwardQuantity = inwardInPeriod2,
                    OutwardQuantity = outwardInPeriod2,
                    ClosingStock = closingStock,
                    OpeningValue = openingValue,
                    InwardValue = inwardValue,
                    OutwardValue = outwardValue,
                    ClosingValue = closingValue
                });

                totalOpeningValue += openingValue;
                totalInwardValue += inwardValue;
                totalOutwardValue += outwardValue;
                totalClosingValue += closingValue;
            }

            var result = new StockMovementReportDto
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                BranchId = branchId.Value,
                BranchName = branch?.BranchName,
                Items = items,
                TotalOpeningValue = totalOpeningValue,
                TotalInwardValue = totalInwardValue,
                TotalOutwardValue = totalOutwardValue,
                TotalClosingValue = totalClosingValue
            };

            return Ok(new ApiResponse<StockMovementReportDto>
            {
                Success = true,
                Message = "Báo cáo xuất nhập tồn đã được tải.",
                Data = result
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
    /// </summary>
    [HttpPost("revenue-by-time")]
    public async Task<IActionResult> GetRevenueByTimeReport([FromBody] RevenueByTimeRequestDto request)
    {
        try
        {
            var branchId = await ResolveBranchIdAsync(request.BranchId);
            if (branchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy thông tin chi nhánh.",
                    Errors = new() { "Không xác định được chi nhánh từ user hiện tại." }
                });
            }

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);

            var fromDate = request.FromDate.Date;
            var toDate = request.ToDate.Date.AddDays(1).AddTicks(-1);

            var orders = await _context.Orders
                .Where(o => o.BranchId == branchId.Value
                    && o.Status == "Completed"
                    && o.CreatedAt >= fromDate
                    && o.CreatedAt <= toDate)
                .Select(o => new { o.CreatedAt, o.TotalAmount })
                .ToListAsync();

            var chartData = new List<ReportRevenueChartPointDto>();
            var tableData = new List<RevenueTimeItemDto>();

            if (request.TimeGrouping.ToLower() == "day")
            {
                // Group by ngày
                var dailyData = orders
                    .GroupBy(o => o.CreatedAt!.Value.Date)
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        Date = g.Key,
                        Revenue = g.Sum(x => x.TotalAmount),
                        OrderCount = g.Count()
                    })
                    .ToList();

                decimal? prevRevenue = null;
                foreach (var day in dailyData)
                {
                    var growth = prevRevenue.HasValue && prevRevenue.Value > 0
                        ? ((day.Revenue - prevRevenue.Value) / prevRevenue.Value) * 100
                        : 0;

                    chartData.Add(new ReportRevenueChartPointDto
                    {
                        TimeKey = day.Date.ToString("yyyy-MM-dd"),
                        Revenue = day.Revenue,
                        OrderCount = day.OrderCount
                    });

                    tableData.Add(new RevenueTimeItemDto
                    {
                        Date = day.Date,
                        TimeKey = day.Date.ToString("dd/MM/yyyy"),
                        Revenue = day.Revenue,
                        Orders = day.OrderCount,
                        AverageValue = day.OrderCount > 0 ? day.Revenue / day.OrderCount : 0,
                        GrowthPercent = growth
                    });

                    prevRevenue = day.Revenue;
                }
            }
            else if (request.TimeGrouping.ToLower() == "week")
            {
                // Group by tuần
                var weeklyData = orders
                    .Where(o => o.CreatedAt.HasValue)
                    .GroupBy(o => GetWeekNumber(o.CreatedAt!.Value))
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        Week = g.Key,
                        Revenue = g.Sum(x => x.TotalAmount),
                        OrderCount = g.Count(),
                        StartDate = g.Min(x => x.CreatedAt)
                    })
                    .ToList();

                decimal? prevRevenue = null;
                foreach (var week in weeklyData)
                {
                    var growth = prevRevenue.HasValue && prevRevenue.Value > 0
                        ? ((week.Revenue - prevRevenue.Value) / prevRevenue.Value) * 100
                        : 0;

                    chartData.Add(new ReportRevenueChartPointDto
                    {
                        TimeKey = $"Week {week.Week}",
                        Revenue = week.Revenue,
                        OrderCount = week.OrderCount
                    });

                    tableData.Add(new RevenueTimeItemDto
                    {
                        Date = week.StartDate ?? DateTime.MinValue,
                        TimeKey = $"Tuần {week.Week}",
                        Revenue = week.Revenue,
                        Orders = week.OrderCount,
                        AverageValue = week.OrderCount > 0 ? week.Revenue / week.OrderCount : 0,
                        GrowthPercent = growth
                    });

                    prevRevenue = week.Revenue;
                }
            }
            else if (request.TimeGrouping.ToLower() == "month")
            {
                // Group by tháng
                var monthlyData = orders
                    .Where(o => o.CreatedAt.HasValue)
                    .GroupBy(o => new { o.CreatedAt!.Value.Year, o.CreatedAt!.Value.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Revenue = g.Sum(x => x.TotalAmount),
                        OrderCount = g.Count(),
                        StartDate = g.Min(x => x.CreatedAt)
                    })
                    .ToList();

                decimal? prevRevenue = null;
                foreach (var month in monthlyData)
                {
                    var monthName = new DateTime(month.Year, month.Month, 1).ToString("MMMM", new System.Globalization.CultureInfo("vi-VN"));
                    var growth = prevRevenue.HasValue && prevRevenue.Value > 0
                        ? ((month.Revenue - prevRevenue.Value) / prevRevenue.Value) * 100
                        : 0;

                    chartData.Add(new ReportRevenueChartPointDto
                    {
                        TimeKey = $"{monthName} {month.Year}",
                        Revenue = month.Revenue,
                        OrderCount = month.OrderCount
                    });

                    tableData.Add(new RevenueTimeItemDto
                    {
                        Date = month.StartDate ?? DateTime.MinValue,
                        TimeKey = $"{monthName} {month.Year}",
                        Revenue = month.Revenue,
                        Orders = month.OrderCount,
                        AverageValue = month.OrderCount > 0 ? month.Revenue / month.OrderCount : 0,
                        GrowthPercent = growth
                    });

                    prevRevenue = month.Revenue;
                }
            }

            var result = new RevenueByTimeReportDto
            {
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                BranchId = branchId.Value,
                BranchName = branch?.BranchName,
                TimeGrouping = request.TimeGrouping,
                ChartData = chartData,
                TableData = tableData,
                TotalRevenue = orders.Sum(o => o.TotalAmount),
                TotalOrders = orders.Count,
                AverageOrderValue = orders.Count > 0 ? orders.Sum(o => o.TotalAmount) / orders.Count : 0
            };

            return Ok(new ApiResponse<RevenueByTimeReportDto>
            {
                Success = true,
                Message = "Báo cáo doanh thu theo thời gian đã được tải.",
                Data = result
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
    /// </summary>
    [HttpPost("low-stock")]
    public async Task<IActionResult> GetLowStockReport([FromBody] LowStockReportRequestDto request)
    {
        try
        {
            var branchId = await ResolveBranchIdAsync(request.BranchId);
            if (branchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy thông tin chi nhánh.",
                    Errors = new() { "Không xác định được chi nhánh từ user hiện tại." }
                });
            }

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);

            var query = _context.BranchProducts
                .Include(bp => bp.Product)
                .Where(bp => bp.BranchId == branchId.Value
                    && bp.MinimumStock.HasValue
                    && (request.IncludeZeroStock
                        ? bp.ActualStock <= bp.MinimumStock
                        : bp.ActualStock < bp.MinimumStock));

            var items = await query
                .Select(bp => new
                {
                    bp.ProductId,
                    ProductCode = bp.Product.ProductCode,
                    ProductName = bp.Product.ProductName,
                    CategoryName = bp.Product.CategoryName,
                    bp.ActualStock,
                    bp.MinimumStock
                })
                .ToListAsync();

            var result = items
                .Select(p => new LowStockItemDto
                {
                    ProductId = p.ProductId,
                    ProductCode = p.ProductCode ?? "",
                    ProductName = p.ProductName ?? "",
                    CategoryName = p.CategoryName ?? "",
                    CurrentStock = p.ActualStock,
                    MinimumStock = p.MinimumStock ?? 0,
                    Shortage = p.ActualStock - (p.MinimumStock ?? 0),
                    Severity = GetSeverity(p.ActualStock, p.MinimumStock ?? 0)
                })
                .OrderBy(x => x.Shortage) // Ưu tiên thiếu nhiều nhất
                .ToList();

            return Ok(new ApiResponse<LowStockReportDto>
            {
                Success = true,
                Message = "Báo cáo tồn kho sắp hết đã được tải.",
                Data = new LowStockReportDto
                {
                    BranchId = branchId.Value,
                    BranchName = branch?.BranchName,
                    Items = result,
                    TotalItems = result.Count
                }
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
            var branchId = await ResolveBranchIdAsync(request.BranchId);
            if (branchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy thông tin chi nhánh.",
                    Errors = new() { "Không xác định được chi nhánh từ user hiện tại." }
                });
            }

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);

            var fromDate = request.FromDate.Date;
            var toDate = request.ToDate.Date.AddDays(1).AddTicks(-1);

            // Lấy chi tiết đơn hàng với thông tin BranchProduct để lấy CostPrice
            var orderItems = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.BranchProduct)
                    .ThenInclude(bp => bp.Product)
                .Where(oi => oi.Order.BranchId == branchId.Value
                    && oi.Order.Status == "Completed"
                    && oi.Order.CreatedAt >= fromDate
                    && oi.Order.CreatedAt <= toDate)
                .ToListAsync();

            // Lọc theo nhóm sản phẩm nếu có
            var filteredItems = orderItems;
            if (!string.IsNullOrEmpty(request.CategoryId?.ToString()))
            {
                filteredItems = orderItems
                    .Where(oi => oi.BranchProduct.Product?.CategoryName == request.CategoryId.ToString())
                    .ToList();
            }

            // Group by ProductId
            var productData = filteredItems
                .GroupBy(oi => new
                {
                    oi.BranchProduct.ProductId,
                    oi.BranchProduct.Product.ProductCode,
                    oi.BranchProduct.Product.ProductName,
                    oi.BranchProduct.Product.CategoryName,
                    oi.BranchProduct.CostPrice
                })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.ProductCode,
                    g.Key.ProductName,
                    g.Key.CategoryName,
                    QuantitySold = g.Sum(oi => oi.Quantity),
                    Revenue = g.Sum(oi => oi.Quantity * oi.SellPrice),
                    Cost = g.Sum(oi => oi.Quantity * g.Key.CostPrice)
                })
                .ToList();

            var items = productData
                .Select(p => new ProductProfitItemDto
                {
                    ProductId = p.ProductId,
                    ProductCode = p.ProductCode ?? "",
                    ProductName = p.ProductName ?? "",
                    CategoryName = p.CategoryName ?? "",
                    QuantitySold = p.QuantitySold,
                    Revenue = p.Revenue,
                    Cost = p.Cost,
                    Profit = p.Revenue - p.Cost,
                    ProfitMargin = p.Revenue > 0 ? ((p.Revenue - p.Cost) / p.Revenue) * 100 : 0
                })
                .ToList();

            // Sort theo yêu cầu
            items = request.SortBy.ToLower() switch
            {
                "profit" => items.OrderByDescending(x => x.Profit).ToList(),
                "quantity" => items.OrderByDescending(x => x.QuantitySold).ToList(),
                _ => items.OrderByDescending(x => x.Revenue).ToList() // default: revenue
            };

            // Phân trang
            var totalCount = items.Count;
            var pagedItems = items
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            return Ok(new ApiResponse<ProductProfitReportDto>
            {
                Success = true,
                Message = "Báo cáo doanh thu/lợi nhuận theo sản phẩm đã được tải.",
                Data = new ProductProfitReportDto
                {
                    FromDate = request.FromDate,
                    ToDate = request.ToDate,
                    BranchId = branchId.Value,
                    BranchName = branch?.BranchName,
                    SortBy = request.SortBy,
                    Items = pagedItems,
                    TotalRevenue = items.Sum(x => x.Revenue),
                    TotalCost = items.Sum(x => x.Cost),
                    TotalProfit = items.Sum(x => x.Profit),
                    AverageProfitMargin = items.Count > 0 ? items.Average(x => x.ProfitMargin) : 0,
                    TotalQuantitySold = (int)items.Sum(x => x.QuantitySold)
                }
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
            var branchId = await ResolveBranchIdAsync(null);
            if (branchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy thông tin chi nhánh.",
                    Errors = new() { "Không xác định được chi nhánh từ user hiện tại." }
                });
            }

            // Lấy thông tin NCC
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == request.SupplierId);

            if (supplier == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Nhà cung cấp không tồn tại.",
                    Errors = new() { $"Không tìm thấy NCC với ID: {request.SupplierId}" }
                });
            }

            // Lấy danh sách PurchaseOrder của NCC này (chưa thanh toán = debt)
            var purchaseQuery = _context.PurchaseOrders
                .Where(po => po.SupplierId == request.SupplierId);

            if (request.PurchaseFromDate.HasValue)
                purchaseQuery = purchaseQuery.Where(po => po.CreatedAt >= request.PurchaseFromDate.Value);
            if (request.PurchaseToDate.HasValue)
                purchaseQuery = purchaseQuery.Where(po => po.CreatedAt <= request.PurchaseToDate.Value.AddDays(1).AddTicks(-1));

            var purchaseOrders = await purchaseQuery
                .OrderByDescending(po => po.CreatedAt)
                .Select(po => new
                {
                    po.PurchaseOrderId,
                    po.OrderCode,
                    po.CreatedAt,
                    po.TotalAmount,
                    po.Status
                })
                .ToListAsync();

            // Tính công nợ
            var totalDebt = purchaseOrders
                .Where(po => po.Status == "APPROVED" || po.Status == "PENDING")
                .Sum(po => po.TotalAmount ?? 0);

            var totalPurchaseAmount = purchaseOrders.Sum(po => po.TotalAmount ?? 0);
            var pendingOrders = purchaseOrders.Count(po => po.Status == "PENDING" || po.Status == "APPROVED");

            // Lấy thanh toán cho NCC
            var paymentQuery = _context.SupplierPayments
                .Where(sp => sp.SupplierId == request.SupplierId);

            if (request.PaymentFromDate.HasValue)
                paymentQuery = paymentQuery.Where(sp => sp.CreatedAt >= request.PaymentFromDate.Value);
            if (request.PaymentToDate.HasValue)
                paymentQuery = paymentQuery.Where(sp => sp.CreatedAt <= request.PaymentToDate.Value.AddDays(1).AddTicks(-1));

            var payments = await paymentQuery
                .OrderByDescending(sp => sp.CreatedAt)
                .Select(sp => new
                {
                    sp.PaymentId,
                    sp.CreatedAt,
                    sp.Amount,
                    sp.Note
                })
                .ToListAsync();

            var totalPaid = payments.Sum(p => p.Amount);

            var result = new SupplierDetailReportDto
            {
                SupplierId = supplier.SupplierId,
                SupplierName = supplier.SupplierName,
                ContactPhone = supplier.ContactPhone,
                ContactEmail = supplier.ContactEmail,
                GroupName = supplier.GroupName,
                DebtInfo = new SupplierDebtInfoDto
                {
                    TotalDebt = totalDebt,
                    TotalPaid = totalPaid,
                    RemainingDebt = totalDebt - totalPaid,
                    PendingOrders = pendingOrders
                },
                PurchaseHistory = purchaseOrders.Select(po => new PurchaseHistoryItemDto
                {
                    PurchaseOrderId = po.PurchaseOrderId,
                    OrderCode = po.OrderCode ?? "",
                    CreatedAt = po.CreatedAt ?? DateTime.MinValue,
                    TotalAmount = po.TotalAmount ?? 0,
                    Status = po.Status ?? ""
                }).ToList(),
                TotalPurchaseOrders = purchaseOrders.Count,
                TotalPurchaseAmount = totalPurchaseAmount,
                PaymentHistory = payments.Select(p => new SupplierPaymentHistoryItemDto
                {
                    PaymentId = p.PaymentId,
                    CreatedAt = p.CreatedAt,
                    Amount = p.Amount,
                    Note = p.Note
                }).ToList(),
                TotalPayments = payments.Count,
                TotalPaidAmount = totalPaid
            };

            return Ok(new ApiResponse<SupplierDetailReportDto>
            {
                Success = true,
                Message = "Chi tiết nhà cung cấp đã được tải.",
                Data = result
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
