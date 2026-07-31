using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

/// <summary>
/// Report Service - chứa logic tính toán báo cáo.
/// Logic tính toán được tách rời từ Controller để dùng chung cho Web UI và Chatbot.
/// Refactored từ ReportsController - KHÔNG thay đổi logic, chỉ DI CHUYỂN code.
/// </summary>
public class ReportService
{
    private readonly MEPDatabaseContext _context;
    private readonly ICurrentBranchAccessor _currentBranch;

    public ReportService(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch)
    {
        _context = context;
        _currentBranch = currentBranch;
    }

    /// <summary>
    /// Helper: Lấy UserId từ ClaimsPrincipal.
    /// </summary>
    private Guid? GetUserId(ClaimsPrincipal user)
    {
        var userClaim = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;
        return Guid.TryParse(userClaim, out var userId) ? userId : null;
    }

    /// <summary>
    /// Helper: Kiểm tra user có role Owner không.
    /// </summary>
    private bool IsOwner(ClaimsPrincipal user)
    {
        return user?.IsInRole("Owner") ?? false;
    }

    /// <summary>
    /// Helper: Lấy branchId từ DefaultBranchId của user hiện tại (ưu tiên cao nhất).
    /// Nếu không có thì lấy từ JWT token.
    /// COPY từ ReportsController.ResolveBranchIdAsync() - GIỮ NGUYÊN LOGIC 100%.
    /// </summary>
    private async Task<Guid?> ResolveBranchIdAsync(ClaimsPrincipal user)
    {
        var userId = GetUserId(user);
        if (userId == null) return null;

        // Ưu tiên 1: DefaultBranchId từ User (nếu có)
        var userEntity = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId.Value);

        if (userEntity?.DefaultBranchId != null && userEntity.DefaultBranchId != Guid.Empty)
        {
            return userEntity.DefaultBranchId;
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

    /// <summary>
    /// Lấy báo cáo cuối ngày: tổng doanh thu, đơn hàng, breakdown thanh toán, phiếu kho.
    /// COPY từ ReportsController.GetDailyEndReport() - GIỮ NGUYÊN LOGIC 100%, chỉ THAY ĐỔI CÁCH LẤY USER.
    /// </summary>
    public async Task<DailyEndReportDto> GetDailyEndReportAsync(DailyEndReportRequestDto request, ClaimsPrincipal currentUser)
    {
        var branchId = await ResolveBranchIdAsync(currentUser);
        if (branchId == null)
        {
            throw new UnauthorizedAccessException("Không xác định được chi nhánh từ user hiện tại.");
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

        // 5. Hoàn tiền từ phiếu trả hàng (chỉ tính cho type=REFUND, bỏ qua EXCHANGE đổi ngang giá)
        var refundCash = 0m;
        var refundTransfer = 0m;
        var refundCount = 0;
        var refunds = await _context.ReturnOrders
            .Where(r => r.Order.BranchId == branchId.Value
                && r.ReturnType == "REFUND"
                && r.Status == "Completed"
                && r.CreatedAt >= reportDate
                && r.CreatedAt < nextDate)
            .ToListAsync();
        if (refunds.Count > 0)
        {
            refundCash = refunds.Where(r => r.RefundMethod == "CASH").Sum(r => r.RefundAmount);
            refundTransfer = refunds.Where(r => r.RefundMethod == "TRANSFER").Sum(r => r.RefundAmount);
            refundCount = refunds.Count;
            paymentBreakdown.CashAmount -= refundCash;
            paymentBreakdown.TransferAmount -= refundTransfer;
        }

        // 6. Lấy chi phí (phiếu chi tiền) trong ngày - chỉ tính CONFIRMED
        var totalExpense = await _context.ExpenseVouchers
            .Where(ev => ev.BranchId == branchId.Value
                && ev.Status == "CONFIRMED"
                && ev.CreatedAt >= reportDate
                && ev.CreatedAt < nextDate)
            .SumAsync(ev => ev.Amount);

        // Tính lợi nhuận ròng = Doanh thu - Chi phí
        var totalRevenue = orders.Sum(o => o.TotalAmount);
        var netProfit = totalRevenue - totalExpense;

        var result = new DailyEndReportDto
        {
            ReportDate = reportDate,
            BranchId = branchId.Value,
            BranchName = branch?.BranchName,
            TotalRevenue = totalRevenue,
            TotalOrders = orders.Count,
            AverageOrderValue = orders.Count > 0 ? totalRevenue / orders.Count : 0m,
            PaymentBreakdown = paymentBreakdown,
            InwardTicketCount = inwardTickets,
            OutwardTicketCount = outwardTickets,
            RefundCashAmount = refundCash,
            RefundTransferAmount = refundTransfer,
            RefundCount = refundCount,
            TotalExpense = totalExpense,
            NetProfit = netProfit
        };

        return result;
    }

    /// <summary>
    /// Lấy báo cáo xuất nhập tồn: opening/inward/outward/closing với giá trị tồn kho.
    /// COPY từ ReportsController.GetStockMovementReport() - GIỮ NGUYÊN LOGIC 100%.
    /// </summary>
    public async Task<StockMovementReportDto> GetStockMovementReportAsync(StockMovementReportRequestDto request, ClaimsPrincipal currentUser)
    {
        var branchId = await ResolveBranchIdAsync(currentUser);
        if (branchId == null)
        {
            throw new UnauthorizedAccessException("Không xác định được chi nhánh từ user hiện tại.");
        }

        // Validate date range: fromDate must be <= toDate
        if (request.FromDate > request.ToDate)
        {
            throw new ArgumentException("Ngày bắt đầu không thể lớn hơn ngày kết thúc.");
        }

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);

        var fromDate = request.FromDate.Date;
        var toDate = request.ToDate.Date.AddDays(1).AddTicks(-1);

        // Lấy danh sách sản phẩm trong kho (có filter category/product)
        // Filter IsActive != -1 để loại sản phẩm bị xóa mềm (không hiển thị mã _DEL_xxx)
        var productsQuery = _context.BranchProducts
            .Include(bp => bp.Product)
            .Where(bp => bp.BranchId == branchId.Value && bp.IsActive != -1);

        // Ưu tiên filter theo CategoryId (Guid) - chính xác hơn
        if (request.CategoryId.HasValue && request.CategoryId != Guid.Empty)
        {
            // lấy tên taxonomy để match với Product.CategoryName
            var categoryName = await _context.ProductTaxonomies
                .AsNoTracking()
                .Where(t => t.TaxonomyId == request.CategoryId.Value && t.Type == "Category")
                .Select(t => t.Name)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrEmpty(categoryName))
            {
                productsQuery = productsQuery.Where(bp => bp.Product != null
                    && bp.Product.CategoryName == categoryName);
            }
        }
        // Fallback: filter theo CategoryName (string) cho backward compat
        else
        {
            var requestedCategoryName = request.CategoryName?.Trim();
            if (!string.IsNullOrWhiteSpace(requestedCategoryName))
            {
                productsQuery = productsQuery.Where(bp => bp.Product != null
                    && bp.Product.CategoryName != null
                    && bp.Product.CategoryName.ToLower() == requestedCategoryName.ToLower());
            }
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

        return result;
    }

    /// <summary>
    /// Lấy báo cáo doanh thu theo thời gian (day/week/month) - biểu đồ + bảng.
    /// COPY từ ReportsController.GetRevenueByTimeReport() - GIỮ NGUYÊN LOGIC 100%.
    /// </summary>
    public async Task<RevenueByTimeReportDto> GetRevenueByTimeReportAsync(RevenueByTimeRequestDto request, ClaimsPrincipal currentUser)
    {
        var branchId = await ResolveBranchIdAsync(currentUser);
        if (branchId == null)
        {
            throw new UnauthorizedAccessException("Không xác định được chi nhánh từ user hiện tại.");
        }

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);

        var fromDate = request.FromDate.Date;
        var toDate = request.ToDate.Date.AddDays(1).AddTicks(-1);
        var fromDateUtc = DateTime.SpecifyKind(fromDate, DateTimeKind.Utc);
        var toDateUtc = DateTime.SpecifyKind(toDate, DateTimeKind.Utc);
        var grouping = string.IsNullOrWhiteSpace(request.TimeGrouping)
            ? "day"
            : request.TimeGrouping.Trim().ToLowerInvariant();

        var normalizedGrouping = grouping switch
        {
            "day" or "daily" => "day",
            "week" or "weekly" => "week",
            "month" or "monthly" => "month",
            _ => "day"
        };

        var orders = await _context.Invoices
            .Where(i => i.Order != null
                && i.Order.BranchId == branchId.Value
                && (i.Status == "Completed" || i.Status == "COMPLETED")
                && i.CreatedAt.HasValue
                && i.CreatedAt >= fromDate
                && i.CreatedAt <= toDate)
            .Select(i => new { CreatedAt = i.CreatedAt!.Value, Revenue = i.TotalAmount })
            .ToListAsync();

        var chartData = new List<ReportRevenueChartPointDto>();
        var tableData = new List<RevenueTimeItemDto>();

        if (normalizedGrouping == "day")
        {
            // Group by ngày
            var dailyData = orders
                .GroupBy(o => o.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Sum(x => x.Revenue),
                    OrderCount = g.Count()
                })
                .ToList();

            decimal? prevRevenue = null;
            foreach (var day in dailyData)
            {
                var growth = prevRevenue.HasValue && prevRevenue.Value > 0
                    ? ((day.Revenue - prevRevenue.Value) / prevRevenue.Value) * 100
                    : 0m;

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
                    AverageValue = day.OrderCount > 0 ? day.Revenue / day.OrderCount : 0m,
                    GrowthPercent = growth
                });

                prevRevenue = day.Revenue;
            }
        }
        else if (normalizedGrouping == "week")
        {
            // Group by tuần
            var weeklyData = orders
                .GroupBy(o => GetWeekNumber(o.CreatedAt))
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Week = g.Key,
                    Revenue = g.Sum(x => x.Revenue),
                    OrderCount = g.Count(),
                    StartDate = g.Min(x => x.CreatedAt)
                })
                .ToList();

            decimal? prevRevenue = null;
            foreach (var week in weeklyData)
            {
                var growth = prevRevenue.HasValue && prevRevenue.Value > 0
                    ? ((week.Revenue - prevRevenue.Value) / prevRevenue.Value) * 100
                    : 0m;

                chartData.Add(new ReportRevenueChartPointDto
                {
                    TimeKey = $"Week {week.Week}",
                    Revenue = week.Revenue,
                    OrderCount = week.OrderCount
                });

                tableData.Add(new RevenueTimeItemDto
                {
                    Date = week.StartDate,
                    TimeKey = $"Tuần {week.Week}",
                    Revenue = week.Revenue,
                    Orders = week.OrderCount,
                    AverageValue = week.OrderCount > 0 ? week.Revenue / week.OrderCount : 0m,
                    GrowthPercent = growth
                });

                prevRevenue = week.Revenue;
            }
        }
        else if (normalizedGrouping == "month")
        {
            // Group by tháng
            var monthlyData = orders
                .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Revenue = g.Sum(x => x.Revenue),
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
                    : 0m;

                chartData.Add(new ReportRevenueChartPointDto
                {
                    TimeKey = $"{monthName} {month.Year}",
                    Revenue = month.Revenue,
                    OrderCount = month.OrderCount
                });

                tableData.Add(new RevenueTimeItemDto
                {
                    Date = month.StartDate,
                    TimeKey = $"{monthName} {month.Year}",
                    Revenue = month.Revenue,
                    Orders = month.OrderCount,
                    AverageValue = month.OrderCount > 0 ? month.Revenue / month.OrderCount : 0m,
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
            TotalRevenue = orders.Sum(o => o.Revenue),
            TotalOrders = orders.Count,
            AverageOrderValue = orders.Count > 0 ? orders.Sum(o => o.Revenue) / orders.Count : 0m
        };

        return result;
    }

    /// <summary>
    /// Helper: Tính số tuần của năm từ DateTime.
    /// Copy từ ReportsController.GetWeekNumber().
    /// </summary>
    private int GetWeekNumber(DateTime date)
    {
        var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
        return cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }

    /// <summary>
    /// Lấy báo cáo tồn kho sắp hết - sản phẩm dưới MinStock với mức độ tốc (Critical/Warning/Low).
    /// COPY từ ReportsController.GetLowStockReport() - GIỮ NGUYÊN LOGIC 100%.
    /// </summary>
    public async Task<LowStockReportDto> GetLowStockReportAsync(LowStockReportRequestDto request, ClaimsPrincipal currentUser)
    {
        var branchId = await ResolveBranchIdAsync(currentUser);
        if (branchId == null)
        {
            throw new UnauthorizedAccessException("Không xác định được chi nhánh từ user hiện tại.");
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
                Shortage = p.ActualStock - (p.MinimumStock ?? 0m),
                Severity = GetSeverity(p.ActualStock, p.MinimumStock ?? 0m)
            })
            .OrderBy(x => x.Shortage) // Ưu tiên thiếu nhiều nhất
            .ToList();

        return new LowStockReportDto
        {
            BranchId = branchId.Value,
            BranchName = branch?.BranchName,
            Items = result,
            TotalItems = result.Count
        };
    }

    /// <summary>
    /// Helper: Tính mức độ tốc của tồn kho (Critical/Warning/Low).
    /// Copy từ ReportsController.GetSeverity().
    /// </summary>
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
    /// Lấy báo cáo doanh thu/lợi nhuận theo sản phẩm.
    /// COPY từ ReportsController.GetProductProfitReport() - GIỮ NGUYÊN LOGIC 100%.
    /// </summary>
    public async Task<ProductProfitReportDto> GetProductProfitReportAsync(ProductProfitReportRequestDto request, ClaimsPrincipal currentUser)
    {
        var branchId = await ResolveBranchIdAsync(currentUser);
        if (branchId == null)
        {
            throw new UnauthorizedAccessException("Không xác định được chi nhánh từ user hiện tại.");
        }

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);

        var fromDate = request.FromDate.Date;
        var toDate = request.ToDate.Date.AddDays(1).AddTicks(-1);

        // Lấy chi tiết từ InvoiceItems (dữ liệu bán hàng thực tế)
        var invoiceItems = await _context.InvoiceItems
            .Include(ii => ii.Invoice)
            .Include(ii => ii.BranchProduct)
                .ThenInclude(bp => bp.Product)
            .Where(ii => ii.Invoice != null
                && ii.Invoice.Order != null
                && ii.Invoice.Order.BranchId == branchId.Value
                && (ii.Invoice.Status == "Completed" || ii.Invoice.Status == "COMPLETED")
                && ii.Invoice.CreatedAt.HasValue
                && ii.Invoice.CreatedAt.Value >= fromDate
                && ii.Invoice.CreatedAt.Value <= toDate)
            .ToListAsync();

        // Debug
        Console.WriteLine($"[ProductProfitReport] branchId={branchId.Value}, fromDate={fromDate}, toDate={toDate}, invoiceItems.Count={invoiceItems.Count}");

        // Lọc theo nhóm sản phẩm nếu có
        var filteredItems = invoiceItems;

        // Ưu tiên filter theo CategoryId (Guid)
        if (request.CategoryId.HasValue && request.CategoryId != Guid.Empty)
        {
            var categoryName = await _context.ProductTaxonomies
                .AsNoTracking()
                .Where(t => t.TaxonomyId == request.CategoryId.Value && t.Type == "Category")
                .Select(t => t.Name)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrEmpty(categoryName))
            {
                filteredItems = invoiceItems
                    .Where(ii => ii.BranchProduct != null
                        && ii.BranchProduct.Product != null
                        && ii.BranchProduct.Product.CategoryName == categoryName)
                    .ToList();
            }
        }
        else
        {
            var requestedCategoryName = request.CategoryName?.Trim();
            if (!string.IsNullOrWhiteSpace(requestedCategoryName))
            {
                var normalizedCategory = requestedCategoryName.Trim().ToLowerInvariant();
                filteredItems = invoiceItems
                    .Where(ii => ii.BranchProduct != null
                        && ii.BranchProduct.Product != null
                        && !string.IsNullOrWhiteSpace(ii.BranchProduct.Product.CategoryName)
                        && ii.BranchProduct.Product.CategoryName.Trim().ToLowerInvariant() == normalizedCategory)
                    .ToList();
            }
        }

        // Group by ProductId
        var productData = filteredItems
            .GroupBy(ii => new
            {
                ii.BranchProduct.ProductId,
                ii.BranchProduct.Product.ProductCode,
                ii.BranchProduct.Product.ProductName,
                ii.BranchProduct.Product.CategoryName,
                ii.BranchProduct.CostPrice
            })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductCode,
                g.Key.ProductName,
                g.Key.CategoryName,
                QuantitySold = g.Sum(ii => ii.Quantity),
                Revenue = g.Sum(ii => ii.Quantity * ii.SellPrice),
                Cost = g.Sum(ii => ii.Quantity * g.Key.CostPrice)
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
                ProfitMargin = p.Revenue > 0 ? ((p.Revenue - p.Cost) / p.Revenue) * 100m : 0m
            })
            .ToList();

        // Sort theo yêu cầu
        var sortBy = string.IsNullOrWhiteSpace(request.SortBy) ? "revenue" : request.SortBy.Trim().ToLowerInvariant();
        items = sortBy switch
        {
            "profit" => items.OrderByDescending(x => x.Profit).ToList(),
            "quantity" => items.OrderByDescending(x => x.QuantitySold).ToList(),
            _ => items.OrderByDescending(x => x.Revenue).ToList() // default: revenue
        };

        // Phân trang
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 50 : request.PageSize;
        var totalCount = items.Count;
        var pagedItems = items
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new ProductProfitReportDto
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            BranchId = branchId.Value,
            BranchName = branch?.BranchName,
            SortBy = sortBy,
            Items = pagedItems,
            TotalRevenue = items.Sum(x => x.Revenue),
            TotalCost = items.Sum(x => x.Cost),
            TotalProfit = items.Sum(x => x.Profit),
            AverageProfitMargin = items.Sum(x => x.Revenue) > 0
                    ? (items.Sum(x => x.Profit) / items.Sum(x => x.Revenue)) * 100m
                    : 0m,
            TotalQuantitySold = (int)items.Sum(x => x.QuantitySold)
        };
    }

    /// <summary>
    /// Lấy trang chi tiết nhà cung cấp - tổng hợp công nợ, mua hàng, thanh toán
    /// COPY nguyên logic từ ReportsController.GetSupplierDetailReport()
    /// </summary>
    public async Task<SupplierDetailReportDto> GetSupplierDetailReportAsync(SupplierDetailRequestDto request, ClaimsPrincipal currentUser)
    {
        var branchId = await ResolveBranchIdAsync(currentUser);
        if (branchId == null)
        {
            throw new UnauthorizedAccessException("Không xác định được chi nhánh từ user hiện tại.");
        }

        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == request.SupplierId);
        if (supplier == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy NCC với ID: {request.SupplierId}");
        }

        // Lay du lieu purchase orders
        var poQuery = _context.PurchaseOrders.Where(po => po.SupplierId == request.SupplierId);
        if (request.PurchaseFromDate.HasValue)
            poQuery = poQuery.Where(po => po.CreatedAt >= request.PurchaseFromDate.Value);
        if (request.PurchaseToDate.HasValue)
            poQuery = poQuery.Where(po => po.CreatedAt <= request.PurchaseToDate.Value);
        var purchaseOrders = await poQuery
            .Select(po => new { po.PurchaseOrderId, po.OrderCode, po.TotalAmount, po.Status, po.CreatedAt })
            .ToListAsync();

        // Lay du lieu stock tickets
        var stQuery0 = _context.StockTickets
            .Where(st => st.SupplierId == request.SupplierId && st.TicketType == "PURCHASE");
        if (request.PurchaseFromDate.HasValue)
            stQuery0 = stQuery0.Where(st => (st.CompletedAt ?? st.CreatedAt) >= request.PurchaseFromDate.Value);
        if (request.PurchaseToDate.HasValue)
            stQuery0 = stQuery0.Where(st => (st.CompletedAt ?? st.CreatedAt) <= request.PurchaseToDate.Value);
        var stockTickets = await stQuery0
            .Select(st => new { st.StockTicketId, st.TicketCode, TotalAmount = (decimal?)st.TotalAmount, st.Status, CreatedAt = (DateTime?)(st.CompletedAt ?? st.CreatedAt) })
            .ToListAsync();

        // FIX: Dùng StockTicket COMPLETED làm source of truth cho "đã mua"
        // (PO khi confirm sẽ tạo StockTicket nên cộng cả 2 sẽ double-count)
        var stDebt = stockTickets.Where(st => st.Status == "COMPLETED").Sum(st => st.TotalAmount ?? 0m);
        // PO APPROVED chưa được confirm thành StockTicket → vẫn tính nợ
        var poUnconfirmedDebt = purchaseOrders.Where(po => po.Status == "APPROVED" || po.Status == "PENDING")
            .Sum(po => po.TotalAmount ?? 0m);
        var totalDebt = stDebt + poUnconfirmedDebt;

        // Tong mua hang = chỉ StockTicket COMPLETED (đã confirmed)
        var totalPurchaseAllTime = stockTickets.Where(st => st.Status == "COMPLETED").Sum(st => st.TotalAmount ?? 0m);

        // So don hang cho = PO chưa confirm + StockTicket PENDING
        var poPending = purchaseOrders.Count(po => po.Status == "PENDING" || po.Status == "APPROVED");
        var stPending = stockTickets.Count(st => st.Status == "PENDING");
        int pendingOrders = poPending + stPending;

        // Tat ca thanh toan (áp dụng date filter cho PaymentFromDate/PaymentToDate)
        var paymentsQuery = _context.SupplierPayments.Where(sp => sp.SupplierId == request.SupplierId);
        if (request.PaymentFromDate.HasValue)
            paymentsQuery = paymentsQuery.Where(sp => sp.CreatedAt >= request.PaymentFromDate.Value);
        if (request.PaymentToDate.HasValue)
            paymentsQuery = paymentsQuery.Where(sp => sp.CreatedAt <= request.PaymentToDate.Value);
        var allPayments = await paymentsQuery.ToListAsync();
        var totalPaidAllTime = allPayments.Sum(sp => sp.Amount);

        // Tinh cong no theo thang
        var monthlyDebts = new List<MonthlyDebtInfoDto>();
        var culture = new System.Globalization.CultureInfo("vi-VN");

        if (request.Year.HasValue)
        {
            var year = request.Year.Value;
            var month = request.Month;

            // Loc purchase orders theo nam/thang
            var filteredPO = purchaseOrders
                .Where(po => po.CreatedAt.HasValue && po.CreatedAt.Value.Year == year)
                .ToList();
            if (month.HasValue)
                filteredPO = filteredPO.Where(po => po.CreatedAt.Value.Month == month.Value).ToList();

            // Loc stock tickets theo nam/thang
            var filteredST = stockTickets
                .Where(st => st.CreatedAt.HasValue && st.CreatedAt.Value.Year == year)
                .ToList();
            if (month.HasValue)
                filteredST = filteredST.Where(st => st.CreatedAt.Value.Month == month.Value).ToList();

            // Gop lai
            var allPurchases = filteredPO.Select(po => new { TotalAmount = po.TotalAmount ?? 0m, CreatedAt = po.CreatedAt })
                .Concat(filteredST.Select(st => new { TotalAmount = st.TotalAmount ?? 0m, CreatedAt = st.CreatedAt }))
                .Where(x => x.CreatedAt.HasValue)
                .ToList();

            // Group theo thang
            var purchasesByMonth = allPurchases
                .Where(x => x.CreatedAt.HasValue)
                .GroupBy(x => new { Year = x.CreatedAt!.Value.Year, Month = x.CreatedAt.Value.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .ToList();

            foreach (var group in purchasesByMonth)
            {
                var monthName = new DateTime(group.Key.Year, group.Key.Month, 1).ToString("MMMM yyyy", culture);
                var totalPurchase = group.Sum(x => x.TotalAmount);

                var paymentsInMonth = allPayments
                    .Where(sp => sp.CreatedAt.Year == group.Key.Year && sp.CreatedAt.Month == group.Key.Month)
                    .Sum(sp => sp.Amount);

                monthlyDebts.Add(new MonthlyDebtInfoDto
                {
                    Year = group.Key.Year,
                    Month = group.Key.Month,
                    MonthName = monthName,
                    TotalPurchase = totalPurchase,
                    TotalPaid = paymentsInMonth,
                    RemainingDebt = totalPurchase - paymentsInMonth,
                    OrderCount = group.Count()
                });
            }
        }

        // Ngay loc
        DateTime? fromDate = null;
        DateTime? toDate = null;
        if (request.Year.HasValue)
        {
            if (request.Month.HasValue)
            {
                fromDate = new DateTime(request.Year.Value, request.Month.Value, 1);
                toDate = fromDate.Value.AddMonths(1).AddTicks(-1);
            }
            else
            {
                fromDate = new DateTime(request.Year.Value, 1, 1);
                toDate = new DateTime(request.Year.Value, 12, 31, 23, 59, 59);
            }
        }

        // Lay lich su mua hang
        var poHistory = await _context.PurchaseOrders
            .Where(po => po.SupplierId == request.SupplierId)
            .Where(po => !fromDate.HasValue || po.CreatedAt >= fromDate.Value)
            .Where(po => !toDate.HasValue || po.CreatedAt <= toDate.Value)
            .OrderByDescending(po => po.CreatedAt)
            .Select(po => new { Id = po.PurchaseOrderId, Code = po.OrderCode, CreatedAt = po.CreatedAt, TotalAmount = po.TotalAmount, Status = po.Status })
            .ToListAsync();

        var stHistory = await _context.StockTickets
            .Where(st => st.SupplierId == request.SupplierId && st.TicketType == "PURCHASE")
            .Where(st => !fromDate.HasValue || (st.CompletedAt ?? st.CreatedAt) >= fromDate.Value)
            .Where(st => !toDate.HasValue || (st.CompletedAt ?? st.CreatedAt) <= toDate.Value)
            .OrderByDescending(st => st.CompletedAt ?? st.CreatedAt)
            .Select(st => new { Id = st.StockTicketId, Code = st.TicketCode, CreatedAt = (DateTime?)(st.CompletedAt ?? st.CreatedAt), TotalAmount = (decimal?)st.TotalAmount, Status = st.Status })
            .ToListAsync();

        var purchaseHistory = poHistory.Select(po => new PurchaseHistoryItemDto
        {
            PurchaseOrderId = po.Id,
            OrderCode = po.Code ?? "",
            CreatedAt = po.CreatedAt ?? DateTime.MinValue,
            TotalAmount = po.TotalAmount ?? 0m,
            Status = po.Status ?? ""
        }).ToList();

        var stHistoryDto = stHistory.Select(st => new PurchaseHistoryItemDto
        {
            PurchaseOrderId = st.Id,
            OrderCode = st.Code ?? "",
            CreatedAt = st.CreatedAt ?? DateTime.MinValue,
            TotalAmount = st.TotalAmount ?? 0m,
            Status = st.Status ?? ""
        }).ToList();

        purchaseHistory.AddRange(stHistoryDto);
        purchaseHistory = purchaseHistory.OrderByDescending(x => x.CreatedAt).ToList();

        var totalPurchaseAmount = purchaseHistory.Sum(x => x.TotalAmount);
        int totalPurchaseOrders = purchaseHistory.Count;

        // Lay lich su thanh toan
        var paymentHistory = await _context.SupplierPayments
            .Where(sp => sp.SupplierId == request.SupplierId)
            .Where(sp => !fromDate.HasValue || sp.CreatedAt >= fromDate.Value)
            .Where(sp => !toDate.HasValue || sp.CreatedAt <= toDate.Value)
            .OrderByDescending(sp => sp.CreatedAt)
            .Select(sp => new { sp.PaymentId, sp.CreatedAt, sp.Amount, sp.Note })
            .ToListAsync();

        var paymentHistoryDto = paymentHistory.Select(p => new SupplierPaymentHistoryItemDto
        {
            PaymentId = p.PaymentId,
            CreatedAt = p.CreatedAt,
            Amount = p.Amount,
            Note = p.Note
        }).ToList();

        var totalPaid = paymentHistoryDto.Sum(x => x.Amount);
        int totalPayments = paymentHistoryDto.Count;

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
                TotalPaid = totalPaidAllTime,
                RemainingDebt = totalDebt - totalPaidAllTime,
                PendingOrders = pendingOrders
            },
            MonthlyDebts = monthlyDebts,
            PurchaseHistory = purchaseHistory,
            TotalPurchaseOrders = totalPurchaseOrders,
            TotalPurchaseAmount = totalPurchaseAmount,
            PaymentHistory = paymentHistoryDto,
            TotalPayments = totalPayments,
            TotalPaidAmount = totalPaid
        };

        return result;
    }
}

