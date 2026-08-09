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

        var reportDateLocal = request.Date.Date;
        var nextDateLocal = reportDateLocal.AddDays(1);
        // Convert local date boundaries to UTC for DB comparison (CreatedAt stored as UTC)
        var reportDate = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(reportDateLocal, DateTimeKind.Unspecified), VietnamTime.Tz);
        var nextDate = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(nextDateLocal, DateTimeKind.Unspecified), VietnamTime.Tz);

        // Lấy thông tin chi nhánh
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);

        // 1. Lấy đơn hàng trong ngày
        var orders = await _context.Orders
            .Where(o => o.BranchId == branchId.Value
                && (o.Status == "COMPLETED" || o.Status == "Completed")
                && o.CreatedAt >= reportDate
                && o.CreatedAt < nextDate)
            .ToListAsync();

        // 2. Lấy thanh toán trong ngày - GroupBy để tự động bắt Cash, Transfer, Combined
        var paymentRaw = await _context.Payments
            .Where(p => p.BranchId == branchId.Value
                && p.Status == "Confirmed"
                && p.CreatedAt >= reportDate
                && p.CreatedAt < nextDate)
            .GroupBy(p => p.Method)
            .Select(g => new { g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync();

        var paymentBreakdown = new PaymentMethodBreakdown();
        foreach (var item in paymentRaw)
        {
            if (item.Key == "Cash") { paymentBreakdown.CashAmount = item.Amount; paymentBreakdown.CashCount = item.Count; }
            else if (item.Key == "Transfer") { paymentBreakdown.TransferAmount = item.Amount; paymentBreakdown.TransferCount = item.Count; }
            else if (item.Key == "Combined") { paymentBreakdown.CombinedAmount = item.Amount; paymentBreakdown.CombinedCount = item.Count; }
        }

        // 3. Phiếu nhập kho PURCHASE trong ngày -> nợ nhà cung cấp
        var purchaseTickets = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && st.TicketType == "PURCHASE"
                && st.CreatedAt.HasValue
                && st.CreatedAt >= reportDate
                && st.CreatedAt < nextDate)
            .ToListAsync();
        paymentBreakdown.DebtAmount = purchaseTickets.Sum(st => st.TotalAmount);
        paymentBreakdown.DebtCount = purchaseTickets.Count;

        // 4. Tổng phiếu nhập kho trong ngày (PURCHASE + CUSTOMER_RETURN + BALANCE_ADJUST)
        var inwardTickets = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST")
                && st.CreatedAt >= reportDate
                && st.CreatedAt < nextDate)
            .CountAsync();

        // 5. Lấy phiếu xuất kho trong ngày (RETURN_SUPPLIER, WRITE_OFF, TRANSFER)
        var outwardTickets = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER")
                && st.CreatedAt >= reportDate
                && st.CreatedAt < nextDate)
            .CountAsync();

        // 6. Hoàn tiền từ phiếu trả hàng (chỉ tính cho type=REFUND, bỏ qua EXCHANGE đổi ngang giá)
        var refundCash = 0m;
        var refundTransfer = 0m;
        var refundCount = 0;
        var refunds = await _context.ReturnOrders
            .Where(r => r.Order.BranchId == branchId.Value
                && r.ReturnType == "REFUND"
                && (r.Status == "COMPLETED" || r.Status == "Completed")
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
                && (ev.Status == "COMPLETED" || ev.Status == "Completed")
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
    /// Báo cáo xuất nhập tồn từ inventory_ledger (single source of truth).
    /// Nếu ledger chưa có dữ liệu → fallback về phương thức cũ.
    /// </summary>
    public async Task<StockMovementReportDto> GetStockMovementFromLedgerAsync(StockMovementReportRequestDto request, ClaimsPrincipal currentUser)
    {
        var branchId = await ResolveBranchIdAsync(currentUser);
        if (branchId == null) throw new UnauthorizedAccessException("Không xác định được chi nhánh.");
        if (request.FromDate > request.ToDate) throw new ArgumentException("Ngày bắt đầu không thể lớn hơn ngày kết thúc.");

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);
        var fromDate = request.FromDate.Date;
        var toDate = request.ToDate.Date.AddDays(1).AddTicks(-1);

        // Lấy danh sách sản phẩm trong kho
        var productsQuery = _context.BranchProducts
            .Include(bp => bp.Product)
            .Where(bp => bp.BranchId == branchId.Value && bp.IsActive != -1
                && (bp.Product.Status == null || bp.Product.Status != -1)
                && (bp.Product.BranchId == null || bp.Product.BranchId == branchId.Value));

        if (request.CategoryId.HasValue && request.CategoryId != Guid.Empty)
        {
            var catName = await _context.ProductTaxonomies.Where(t => t.TaxonomyId == request.CategoryId.Value && t.Type == "Category").Select(t => t.Name).FirstOrDefaultAsync();
            if (!string.IsNullOrEmpty(catName)) productsQuery = productsQuery.Where(bp => bp.Product.CategoryName != null && bp.Product.CategoryName.ToLower() == catName.ToLower());
        }
        if (request.CategoryName != null) productsQuery = productsQuery.Where(bp => bp.Product.CategoryName != null && bp.Product.CategoryName.ToLower() == request.CategoryName.Trim().ToLower());
        if (request.ProductId.HasValue && request.ProductId != Guid.Empty) productsQuery = productsQuery.Where(bp => bp.ProductId == request.ProductId.Value);

        var products = await productsQuery.ToListAsync();

        // Kiểm tra inventory_ledger có dữ liệu không
        var ledgerCount = await _context.InventoryLedgers.Where(l => l.BranchId == branchId.Value).CountAsync();

        // === Query từ inventory_ledger (nếu có dữ liệu) ===
        Dictionary<Guid, decimal>? openingStock = null, inwardQty = null, inwardValue = null, outwardQty = null, outwardValue = null, adjustQty = null, adjustValue = null;

        if (ledgerCount > 0)
        {
            // Tồn đầu kỳ: SUM(qty_change) trước fromDate
            var openingData = await _context.InventoryLedgers
                .Where(l => l.BranchId == branchId.Value && l.TransactionTime < fromDate)
                .GroupBy(l => l.BranchProductId)
                .Select(g => new { BranchProductId = g.Key, Qty = g.Sum(l => l.QtyChange), Value = g.Sum(l => l.TotalCostValue) })
                .ToListAsync();
            openingStock = openingData.ToDictionary(x => x.BranchProductId, x => x.Qty);
            var openingValues = openingData.ToDictionary(x => x.BranchProductId, x => x.Value);

            // Nhập trong kỳ: PURCHASE + CUSTOMER_RETURN
            var inData = await _context.InventoryLedgers
                .Where(l => l.BranchId == branchId.Value && l.TransactionTime >= fromDate && l.TransactionTime <= toDate
                    && (l.DocType == "PURCHASE" || l.DocType == "CUSTOMER_RETURN"))
                .GroupBy(l => l.BranchProductId)
                .Select(g => new { BranchProductId = g.Key, Qty = g.Sum(l => l.QtyChange), Value = g.Sum(l => l.TotalCostValue) })
                .ToListAsync();
            inwardQty = inData.ToDictionary(x => x.BranchProductId, x => x.Qty);
            inwardValue = inData.ToDictionary(x => x.BranchProductId, x => x.Value);

            // Xuất trong kỳ: SALE + RETURN_SUPPLIER + WRITE_OFF + TRANSFER (dùng ABS cho qty)
            var outData = await _context.InventoryLedgers
                .Where(l => l.BranchId == branchId.Value && l.TransactionTime >= fromDate && l.TransactionTime <= toDate
                    && (l.DocType == "SALE" || l.DocType == "RETURN_SUPPLIER" || l.DocType == "WRITE_OFF" || l.DocType == "TRANSFER"))
                .GroupBy(l => l.BranchProductId)
                .Select(g => new { BranchProductId = g.Key, Qty = g.Sum(l => -l.QtyChange), Value = g.Sum(l => -l.TotalCostValue) })
                .ToListAsync();
            outwardQty = outData.ToDictionary(x => x.BranchProductId, x => x.Qty);
            outwardValue = outData.ToDictionary(x => x.BranchProductId, x => x.Value);

            // Điều chỉnh: BALANCE_ADJUST
            var adjData = await _context.InventoryLedgers
                .Where(l => l.BranchId == branchId.Value && l.TransactionTime >= fromDate && l.TransactionTime <= toDate && l.DocType == "BALANCE_ADJUST")
                .GroupBy(l => l.BranchProductId)
                .Select(g => new { BranchProductId = g.Key, Qty = g.Sum(l => l.QtyChange), Value = g.Sum(l => l.TotalCostValue) })
                .ToListAsync();
            adjustQty = adjData.ToDictionary(x => x.BranchProductId, x => x.Qty);
            adjustValue = adjData.ToDictionary(x => x.BranchProductId, x => x.Value);

            // Build result
            var items = new List<StockMovementItemDto>();
            decimal totalOpeningV = 0, totalInwardV = 0, totalOutwardV = 0, totalAdjustV = 0, totalClosingV = 0;

            foreach (var bp in products)
            {
                var bpid = bp.BranchProductId;
                var openQ = openingStock.GetValueOrDefault(bpid, 0);
                var openV = openingValues.GetValueOrDefault(bpid, 0);
                var inQ = inwardQty.GetValueOrDefault(bpid, 0);
                var inV = inwardValue.GetValueOrDefault(bpid, 0);
                var outQ = outwardQty.GetValueOrDefault(bpid, 0);
                var outV = outwardValue.GetValueOrDefault(bpid, 0);
                var adjQ = adjustQty.GetValueOrDefault(bpid, 0);
                var adjV = adjustValue.GetValueOrDefault(bpid, 0);
                var closingQ = openQ + inQ - outQ + adjQ;
                var closingV = Math.Round(closingQ * bp.CostPrice, 2); // GT_Cuối = SL_Cuối × Giá vốn BQ hiện tại

                items.Add(new StockMovementItemDto
                {
                    ProductId = bp.ProductId,
                    BranchProductId = bp.BranchProductId,
                    ProductCode = bp.Product?.ProductCode ?? "",
                    ProductName = bp.Product?.ProductName ?? "",
                    CategoryName = bp.Product?.CategoryName ?? "",
                    BaseUnit = bp.Product?.BaseUnit ?? "",
                    OpeningStock = openQ, OpeningValue = openV,
                    InwardQuantity = inQ, InwardValue = inV,
                    OutwardQuantity = outQ, OutwardValue = outV,
                    AdjustmentQuantity = adjQ, AdjustmentValue = adjV,
                    ClosingStock = closingQ, ClosingValue = closingV
                });
                totalOpeningV += openV; totalInwardV += inV; totalOutwardV += outV; totalAdjustV += adjV; totalClosingV += closingV;
            }

            return new StockMovementReportDto
            {
                FromDate = request.FromDate, ToDate = request.ToDate, BranchId = branchId.Value,
                BranchName = branch?.BranchName, Items = items,
                TotalOpeningValue = totalOpeningV, TotalInwardValue = totalInwardV,
                TotalOutwardValue = totalOutwardV, TotalAdjustmentValue = totalAdjustV,
                TotalClosingValue = totalClosingV
            };
        }

        // === FALLBACK: dùng phương thức cũ cho dữ liệu chưa có ledger ===
        return await GetStockMovementReportLegacyAsync(request, branchId.Value, branch, fromDate, toDate, products);
    }

    private async Task<StockMovementReportDto> GetStockMovementReportLegacyAsync(
        StockMovementReportRequestDto request, Guid branchId, Branch? branch, DateTime fromDate, DateTime toDate, List<BranchProduct> products)
    {
        // Copy logic cũ nhưng fix các lỗi:
        // 1. CUSTOMER_RETURN có giá trị (dùng unit_price snapshot)
        // 2. Sales dùng invoice_items cho cả SL + Giá trị
        // 3. BALANCE_ADJUST dùng TicketType, không dùng Notes LIKE
        // 4. closingValue = closingStock × CostPrice

        // Pre-period queries
        var inwardPurchaseBefore = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && st.TicketType == "PURCHASE" && st.CreatedAt < fromDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        var inwardReturnBefore = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && st.TicketType == "CUSTOMER_RETURN" && st.CreatedAt < fromDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        var outwardTicketsBefore = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER") && st.CreatedAt < fromDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        var adjustmentBefore = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && st.TicketType == "BALANCE_ADJUST" && st.CreatedAt < fromDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);

        // Sales trước kỳ - dùng invoice_items cho cả SL và giá trị
        var salesBefore = await _context.InvoiceItems.Where(ii => ii.Invoice.Order.BranchId == branchId && ii.Invoice.Status == "Completed" && ii.Invoice.CreatedAt < fromDate).GroupBy(ii => ii.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(ii => ii.Quantity * (ii.ConvertValue ?? 1m)), Value = g.Sum(ii => (ii.Quantity * (ii.ConvertValue ?? 1m)) * (ii.CostPrice > 0 ? ii.CostPrice : ii.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => new { x.Qty, x.Value });

        // In-period queries
        var inwardPurchaseIn = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && st.TicketType == "PURCHASE" && st.CreatedAt >= fromDate && st.CreatedAt <= toDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        var inwardReturnIn = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && st.TicketType == "CUSTOMER_RETURN" && st.CreatedAt >= fromDate && st.CreatedAt <= toDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        var outwardTicketsIn = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER") && st.CreatedAt >= fromDate && st.CreatedAt <= toDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        var adjustmentIn = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && st.TicketType == "BALANCE_ADJUST" && st.CreatedAt >= fromDate && st.CreatedAt <= toDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);

        // Sales trong kỳ - dùng invoice_items
        var salesIn = await _context.InvoiceItems.Where(ii => ii.Invoice.Order.BranchId == branchId && ii.Invoice.Status == "Completed" && ii.Invoice.CreatedAt >= fromDate && ii.Invoice.CreatedAt <= toDate).GroupBy(ii => ii.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(ii => ii.Quantity * (ii.ConvertValue ?? 1m)), Value = g.Sum(ii => (ii.Quantity * (ii.ConvertValue ?? 1m)) * (ii.CostPrice > 0 ? ii.CostPrice : ii.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => new { x.Qty, x.Value });

        // Giá trị nhập - PURCHASE + CUSTOMER_RETURN
        var purchaseValueIn = await _context.StockTicketItems.Where(sti => sti.StockTicket.BranchId == branchId && sti.StockTicket.Status == "COMPLETED" && sti.StockTicket.TicketType == "PURCHASE" && sti.StockTicket.CreatedAt >= fromDate && sti.StockTicket.CreatedAt <= toDate).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Value = g.Sum(s => s.Discrepancy * (s.UnitPrice ?? s.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => x.Value);
        var returnValueIn = await _context.StockTicketItems.Where(sti => sti.StockTicket.BranchId == branchId && sti.StockTicket.Status == "COMPLETED" && sti.StockTicket.TicketType == "CUSTOMER_RETURN" && sti.StockTicket.CreatedAt >= fromDate && sti.StockTicket.CreatedAt <= toDate).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Value = g.Sum(s => s.Discrepancy * (s.UnitPrice ?? s.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => x.Value);
        var outwardValueIn = await _context.StockTicketItems.Where(sti => sti.StockTicket.BranchId == branchId && sti.StockTicket.Status == "COMPLETED" && (sti.StockTicket.TicketType == "RETURN_SUPPLIER" || sti.StockTicket.TicketType == "WRITE_OFF" || sti.StockTicket.TicketType == "TRANSFER") && sti.StockTicket.CreatedAt >= fromDate && sti.StockTicket.CreatedAt <= toDate).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Value = g.Sum(s => s.Discrepancy * (s.UnitPrice ?? s.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => x.Value);

        // Giá trị trước kỳ
        var purchaseValueBefore = await _context.StockTicketItems.Where(sti => sti.StockTicket.BranchId == branchId && sti.StockTicket.Status == "COMPLETED" && sti.StockTicket.TicketType == "PURCHASE" && sti.StockTicket.CreatedAt < fromDate).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Value = g.Sum(s => s.Discrepancy * (s.UnitPrice ?? s.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => x.Value);
        var returnValueBefore = await _context.StockTicketItems.Where(sti => sti.StockTicket.BranchId == branchId && sti.StockTicket.Status == "COMPLETED" && sti.StockTicket.TicketType == "CUSTOMER_RETURN" && sti.StockTicket.CreatedAt < fromDate).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Value = g.Sum(s => s.Discrepancy * (s.UnitPrice ?? s.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => x.Value);
        var outwardValueBefore = await _context.StockTicketItems.Where(sti => sti.StockTicket.BranchId == branchId && sti.StockTicket.Status == "COMPLETED" && (sti.StockTicket.TicketType == "RETURN_SUPPLIER" || sti.StockTicket.TicketType == "WRITE_OFF" || sti.StockTicket.TicketType == "TRANSFER") && sti.StockTicket.CreatedAt < fromDate).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Value = g.Sum(s => s.Discrepancy * (s.UnitPrice ?? s.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => x.Value);

        // Build result
        var items = new List<StockMovementItemDto>();
        decimal tOpenV = 0, tInV = 0, tOutV = 0, tAdjV = 0, tCloseV = 0;

        foreach (var bp in products)
        {
            var bpid = bp.BranchProductId;
            var openQ = inwardPurchaseBefore.GetValueOrDefault(bpid, 0) + inwardReturnBefore.GetValueOrDefault(bpid, 0) - outwardTicketsBefore.GetValueOrDefault(bpid, 0) - salesBefore.GetValueOrDefault(bpid, new { Qty = 0m, Value = 0m }).Qty + adjustmentBefore.GetValueOrDefault(bpid, 0);
            var inQ = inwardPurchaseIn.GetValueOrDefault(bpid, 0) + inwardReturnIn.GetValueOrDefault(bpid, 0);
            var outQ = outwardTicketsIn.GetValueOrDefault(bpid, 0) + salesIn.GetValueOrDefault(bpid, new { Qty = 0m, Value = 0m }).Qty;
            var adjQ = adjustmentIn.GetValueOrDefault(bpid, 0);

            var openV = purchaseValueBefore.GetValueOrDefault(bpid, 0) + returnValueBefore.GetValueOrDefault(bpid, 0) - outwardValueBefore.GetValueOrDefault(bpid, 0) - salesBefore.GetValueOrDefault(bpid, new { Qty = 0m, Value = 0m }).Value + adjustmentBefore.GetValueOrDefault(bpid, 0) * bp.CostPrice;
            var inV = purchaseValueIn.GetValueOrDefault(bpid, 0) + returnValueIn.GetValueOrDefault(bpid, 0);
            var outV = outwardValueIn.GetValueOrDefault(bpid, 0) + salesIn.GetValueOrDefault(bpid, new { Qty = 0m, Value = 0m }).Value;
            var adjV = adjustmentIn.GetValueOrDefault(bpid, 0) * bp.CostPrice;

            var closeQ = openQ + inQ - outQ + adjQ;
            var closeV = Math.Round(closeQ * bp.CostPrice, 2); // GT_Cuối = SL_Cuối × CostPrice hiện tại

            items.Add(new StockMovementItemDto
            {
                ProductId = bp.ProductId, BranchProductId = bpid,
                ProductCode = bp.Product?.ProductCode ?? "", ProductName = bp.Product?.ProductName ?? "",
                CategoryName = bp.Product?.CategoryName ?? "", BaseUnit = bp.Product?.BaseUnit ?? "",
                OpeningStock = openQ, OpeningValue = Math.Round(openV, 2),
                InwardQuantity = inQ, InwardValue = Math.Round(inV, 2),
                OutwardQuantity = outQ, OutwardValue = Math.Round(outV, 2),
                AdjustmentQuantity = adjQ, AdjustmentValue = Math.Round(adjV, 2),
                ClosingStock = closeQ, ClosingValue = closeV
            });
            tOpenV += openV; tInV += inV; tOutV += outV; tAdjV += adjV; tCloseV += closeV;
        }

        return new StockMovementReportDto
        {
            FromDate = request.FromDate, ToDate = request.ToDate, BranchId = branchId,
            BranchName = branch?.BranchName, Items = items,
            TotalOpeningValue = Math.Round(tOpenV, 2), TotalInwardValue = Math.Round(tInV, 2),
            TotalOutwardValue = Math.Round(tOutV, 2), TotalAdjustmentValue = Math.Round(tAdjV, 2),
            TotalClosingValue = Math.Round(tCloseV, 2)
        };
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
        // Filter IsActive != -1 và Status != -1 để loại sản phẩm bị xóa mềm (không hiển thị mã _DEL_xxx)
        // Enforce SaaS tenant isolation (BranchId == null cho sản phẩm cũ, BranchId == branchId cho sp mới)
        var productsQuery = _context.BranchProducts
            .Include(bp => bp.Product)
            .Where(bp => bp.BranchId == branchId.Value && 
                         bp.IsActive != -1 && 
                         (bp.Product.Status == null || bp.Product.Status != -1) &&
                         (bp.Product.BranchId == null || bp.Product.BranchId == branchId.Value));

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

        // Nhập mua TRƯỚC fromDate (PURCHASE + BALANCE_ADJUST, không bao gồm CUSTOMER_RETURN để tính giá trị)
        var inwardPurchaseBeforePeriod = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && (st.TicketType == "PURCHASE" || st.TicketType == "BALANCE_ADJUST")
                && !(st.Notes != null && st.Notes.Contains("INVENTORY_ADJUSTMENT"))
                && st.CreatedAt < fromDate)
            .SelectMany(st => st.StockTicketItems)
            .GroupBy(sti => sti.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, TotalInward = g.Sum(sti => sti.Discrepancy) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalInward);

        // Nhập CUSTOMER_RETURN TRƯỚC fromDate (tăng tồn kho vật lý nhưng không tính vào giá trị nhập)
        var inwardReturnBeforePeriod = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && st.TicketType == "CUSTOMER_RETURN"
                && st.CreatedAt < fromDate)
            .SelectMany(st => st.StockTicketItems)
            .GroupBy(sti => sti.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, TotalInward = g.Sum(sti => sti.Discrepancy) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalInward);

        // Xuất (bán hàng) TRƯỚC fromDate
        var salesBeforePeriod = await _context.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Order.BranchId == branchId.Value
                && (oi.Order.Status == "COMPLETED" || oi.Order.Status == "Completed")
                && oi.Order.CreatedAt < fromDate)
            .GroupBy(oi => oi.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, TotalOutward = g.Sum(oi => oi.Quantity) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalOutward);

        // Xuất kho (phiếu xuất) TRƯỚC fromDate
        var outwardTicketsBeforePeriod = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER")
                && st.CreatedAt < fromDate)
            .SelectMany(st => st.StockTicketItems)
            .GroupBy(sti => sti.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, TotalOutward = g.Sum(sti => sti.Discrepancy) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalOutward);

        // Điều chỉnh kiểm kê TRƯỚC fromDate (để tính tồn đầu kỳ theo đúng ERP)
        var adjustmentBeforePeriod = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && st.Notes != null
                && st.Notes.Contains("INVENTORY_ADJUSTMENT")
                && st.CreatedAt < fromDate)
            .SelectMany(st => st.StockTicketItems.Select(sti => new
            {
                BranchProductId = sti.BranchProductId,
                TicketType = st.TicketType,
                Discrepancy = sti.Discrepancy
            }))
            .GroupBy(x => x.BranchProductId)
            .Select(g => new
            {
                BranchProductId = g.Key,
                TotalAdjustment = g.Sum(x => (x.TicketType == "BALANCE_ADJUST" ? 1m : -1m) * x.Discrepancy)
            })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalAdjustment);

        // ===== TÍNH NHẬP/XUẤT TRONG KỲ =====
        // Nhập mua trong kỳ (PURCHASE only; KHÔNG tính điều chỉnh kiểm kê vào cột NHẬP)
        var inwardPurchaseInPeriod = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && (st.TicketType == "PURCHASE" || st.TicketType == "BALANCE_ADJUST")
                && !(st.Notes != null && st.Notes.Contains("INVENTORY_ADJUSTMENT"))
                && st.CreatedAt >= fromDate
                && st.CreatedAt <= toDate)
            .SelectMany(st => st.StockTicketItems)
            .GroupBy(sti => sti.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, TotalInward = g.Sum(sti => sti.Discrepancy) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalInward);

        // Nhập CUSTOMER_RETURN trong kỳ (tăng tồn kho vật lý nhưng không tính vào giá trị nhập)
        var inwardReturnInPeriod = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && st.TicketType == "CUSTOMER_RETURN"
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
                && (oi.Order.Status == "COMPLETED" || oi.Order.Status == "Completed")
                && oi.Order.CreatedAt >= fromDate
                && oi.Order.CreatedAt <= toDate)
            .GroupBy(oi => oi.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, TotalOutward = g.Sum(oi => oi.Quantity) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalOutward);

        // Xuất kho trong kỳ (phân loại khỏi phiếu điều chỉnh kiểm kê)
        var outwardTicketsInPeriod = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER")
                && !(st.Notes != null && st.Notes.Contains("INVENTORY_ADJUSTMENT"))
                && st.CreatedAt >= fromDate
                && st.CreatedAt <= toDate)
            .SelectMany(st => st.StockTicketItems)
            .GroupBy(sti => sti.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, TotalOutward = g.Sum(sti => sti.Discrepancy) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalOutward);

        // Điều chỉnh kiểm kê trong kỳ (chỉ cộng vào cột riêng, không đẩy vào NHẬP/XUẤT thương mại)
        var adjustmentInPeriod = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && st.Notes != null
                && st.Notes.Contains("INVENTORY_ADJUSTMENT")
                && st.CreatedAt >= fromDate
                && st.CreatedAt <= toDate)
            .SelectMany(st => st.StockTicketItems.Select(sti => new
            {
                BranchProductId = sti.BranchProductId,
                TicketType = st.TicketType,
                Discrepancy = sti.Discrepancy,
                UnitPrice = sti.UnitPrice,
                CostPrice = sti.BranchProduct.CostPrice
            }))
            .GroupBy(x => x.BranchProductId)
            .Select(g => new
            {
                BranchProductId = g.Key,
                TotalAdjustment = g.Sum(x => (x.TicketType == "BALANCE_ADJUST" ? 1m : -1m) * x.Discrepancy),
                TotalAdjustmentValue = g.Sum(x => (x.TicketType == "BALANCE_ADJUST" ? 1m : -1m) * x.Discrepancy * (
                    x.UnitPrice.HasValue && x.UnitPrice.Value > 0
                        ? x.UnitPrice.Value
                        : x.CostPrice > 0
                            ? x.CostPrice
                            : 0m))
            })
            .ToDictionaryAsync(x => x.BranchProductId, x => new { x.TotalAdjustment, x.TotalAdjustmentValue });

        // Giá trị xuất kho thực tế theo giá vốn (CostPrice) với fallback an toàn nếu UnitPrice cũ = 0/null.
        var outwardValueMap = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER")
                && !(st.Notes != null && st.Notes.Contains("INVENTORY_ADJUSTMENT"))
                && st.CreatedAt >= fromDate
                && st.CreatedAt <= toDate)
            .SelectMany(st => st.StockTicketItems)
            .GroupBy(sti => sti.BranchProductId)
            .Select(g => new
            {
                BranchProductId = g.Key,
                Value = g.Sum(sti => sti.Discrepancy * (
                    sti.UnitPrice.HasValue && sti.UnitPrice.Value > 0
                        ? sti.UnitPrice.Value
                        : sti.BranchProduct.CostPrice > 0
                            ? sti.BranchProduct.CostPrice
                            : 0m))
            })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.Value);

        // === Giá trị nhập mua thực tế (từ unit_price đã snapshot) ===
        var inwardPurchaseValueInPeriod = await _context.StockTicketItems
            .Where(sti => sti.StockTicket.BranchId == branchId.Value
                && sti.StockTicket.Status == "COMPLETED"
                && sti.StockTicket.TicketType == "PURCHASE"
                && sti.StockTicket.CompletedAt >= fromDate
                && sti.StockTicket.CompletedAt <= toDate)
            .GroupBy(sti => sti.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, Value = g.Sum(sti => sti.Discrepancy * (sti.UnitPrice ?? sti.BranchProduct.CostPrice)) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.Value);

        var inwardPurchaseValueBeforePeriod = await _context.StockTicketItems
            .Where(sti => sti.StockTicket.BranchId == branchId.Value
                && sti.StockTicket.Status == "COMPLETED"
                && sti.StockTicket.TicketType == "PURCHASE"
                && sti.StockTicket.CompletedAt < fromDate)
            .GroupBy(sti => sti.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, Value = g.Sum(sti => sti.Discrepancy * (sti.UnitPrice ?? sti.BranchProduct.CostPrice)) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.Value);

        // === Giá trị bán hàng (từ invoice_items.cost_price đã snapshot) ===
        var salesValueInPeriod = await _context.InvoiceItems
            .Where(ii => ii.Invoice.Order.BranchId == branchId.Value
                && ii.Invoice.Status == "Completed"
                && ii.Invoice.CreatedAt >= fromDate
                && ii.Invoice.CreatedAt <= toDate)
            .GroupBy(ii => ii.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, Value = g.Sum(ii => ii.Quantity * (ii.CostPrice > 0 ? ii.CostPrice : ii.BranchProduct.CostPrice)) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.Value);

        var salesValueBeforePeriod2 = await _context.InvoiceItems
            .Where(ii => ii.Invoice.Order.BranchId == branchId.Value
                && ii.Invoice.Status == "Completed"
                && ii.Invoice.CreatedAt < fromDate)
            .GroupBy(ii => ii.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, Value = g.Sum(ii => ii.Quantity * (ii.CostPrice > 0 ? ii.CostPrice : ii.BranchProduct.CostPrice)) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.Value);

        // === Giá trị xuất kho trước kỳ (từ unit_price đã snapshot) ===
        var outwardValueBeforePeriod2 = await _context.StockTicketItems
            .Where(sti => sti.StockTicket.BranchId == branchId.Value
                && sti.StockTicket.Status == "COMPLETED"
                && (sti.StockTicket.TicketType == "RETURN_SUPPLIER" || sti.StockTicket.TicketType == "WRITE_OFF" || sti.StockTicket.TicketType == "TRANSFER")
                && !(sti.StockTicket.Notes != null && sti.StockTicket.Notes.Contains("INVENTORY_ADJUSTMENT"))
                && sti.StockTicket.CompletedAt < fromDate)
            .GroupBy(sti => sti.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, Value = g.Sum(sti => sti.Discrepancy * (sti.UnitPrice ?? sti.BranchProduct.CostPrice)) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.Value);

        var items = new List<StockMovementItemDto>();
        decimal totalOpeningValue = 0, totalInwardValue = 0, totalOutwardValue = 0, totalAdjustmentValue = 0, totalClosingValue = 0;

        foreach (var bp in products)
        {
            var branchProductId = bp.BranchProductId;

            // Tính tồn đầu kỳ đúng cách (số lượng vật lý bao gồm cả CUSTOMER_RETURN)
            var inwardPurchaseBefore = inwardPurchaseBeforePeriod.GetValueOrDefault(branchProductId, 0);
            var inwardReturnBefore = inwardReturnBeforePeriod.GetValueOrDefault(branchProductId, 0);
            var inwardBefore = inwardPurchaseBefore + inwardReturnBefore;
            var salesBefore = salesBeforePeriod.GetValueOrDefault(branchProductId, 0);
            var outwardBefore = outwardTicketsBeforePeriod.GetValueOrDefault(branchProductId, 0);
            var openingStock = inwardBefore - (salesBefore + outwardBefore);

            // Tính nhập/xuất trong kỳ
            var inwardPurchaseIn = inwardPurchaseInPeriod.GetValueOrDefault(branchProductId, 0);
            var inwardReturnIn = inwardReturnInPeriod.GetValueOrDefault(branchProductId, 0);
            var inwardIn = inwardPurchaseIn + inwardReturnIn;
            var salesOut = salesInPeriod.GetValueOrDefault(branchProductId, 0);
            var outwardOut = outwardTicketsInPeriod.GetValueOrDefault(branchProductId, 0);
            var inwardInPeriod2 = inwardIn;
            var outwardInPeriod2 = salesOut + outwardOut;
            var adjustmentBefore = adjustmentBeforePeriod.GetValueOrDefault(branchProductId, 0m);
            var adjustmentIn = adjustmentInPeriod.GetValueOrDefault(branchProductId, new { TotalAdjustment = 0m, TotalAdjustmentValue = 0m }).TotalAdjustment;
            var adjustmentValue = adjustmentInPeriod.GetValueOrDefault(branchProductId, new { TotalAdjustment = 0m, TotalAdjustmentValue = 0m }).TotalAdjustmentValue;

            // Tồn đầu kỳ = dư lượng trước kỳ + điều chỉnh kiểm kê trước kỳ
            openingStock = openingStock + adjustmentBefore;

            // Tồn cuối = Đầu kỳ + Nhập - Xuất + Điều chỉnh kiểm kê trong kỳ
            var closingStock = openingStock + inwardInPeriod2 - outwardInPeriod2 + adjustmentIn;

            // Giá trị: dùng giá đã snapshot (unit_price / cost_price) thay vì bp.CostPrice hiện tại
            // Inward value từ stock_ticket_items.unit_price (giá nhập thực tế)
            var inwardPurchaseValueIn = inwardPurchaseValueInPeriod.GetValueOrDefault(branchProductId, 0);
            var inwardValue = inwardPurchaseValueIn;

            // Outward value: sales từ invoice_items.cost_price (đã snapshot) + outward tickets từ unit_price
            var salesValueIn = salesValueInPeriod.GetValueOrDefault(branchProductId, 0);
            var outwardValue = salesValueIn + outwardValueMap.GetValueOrDefault(branchProductId, 0);

            // Opening value = Giá trị nhập trước kỳ (thực tế) - Giá trị xuất trước kỳ (thực tế)
            var inwardPurchaseValueBefore = inwardPurchaseValueBeforePeriod.GetValueOrDefault(branchProductId, 0);
            var salesValueBefore = salesValueBeforePeriod2.GetValueOrDefault(branchProductId, 0);
            var outwardValueBefore = outwardValueBeforePeriod2.GetValueOrDefault(branchProductId, 0);
            var openingValue = inwardPurchaseValueBefore - salesValueBefore - outwardValueBefore + (adjustmentBefore * bp.CostPrice);
            var adjustmentValueForItem = adjustmentValue;
            var closingValue = openingValue + inwardValue - outwardValue + adjustmentValueForItem;

            items.Add(new StockMovementItemDto
            {
                ProductId = bp.ProductId,
                BranchProductId = bp.BranchProductId,
                ProductCode = bp.Product?.ProductCode ?? "",
                ProductName = bp.Product?.ProductName ?? "",
                CategoryName = bp.Product?.CategoryName ?? "",
                BaseUnit = bp.Product?.BaseUnit ?? "",
                OpeningStock = openingStock,
                InwardQuantity = inwardInPeriod2,
                OutwardQuantity = outwardInPeriod2,
                AdjustmentQuantity = adjustmentIn,
                ClosingStock = closingStock,
                OpeningValue = openingValue,
                InwardValue = inwardValue,
                OutwardValue = outwardValue,
                AdjustmentValue = adjustmentValueForItem,
                ClosingValue = closingValue
            });

            totalOpeningValue += openingValue;
            totalInwardValue += inwardValue;
            totalOutwardValue += outwardValue;
            totalAdjustmentValue += adjustmentValueForItem;
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
            TotalAdjustmentValue = totalAdjustmentValue,
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
            .Select(i => new { CreatedAt = i.CreatedAt!.Value, Revenue = i.TotalAmount, InvoiceId = i.InvoiceId })
            .ToListAsync();

        var invoiceItems = await _context.InvoiceItems
            .Include(ii => ii.BranchProduct)
            .Include(ii => ii.Invoice)
            .Where(ii => ii.Invoice != null
                && ii.Invoice.Order != null
                && ii.Invoice.Order.BranchId == branchId.Value
                && (ii.Invoice.Status == "Completed" || ii.Invoice.Status == "COMPLETED")
                && ii.Invoice.CreatedAt.HasValue
                && ii.Invoice.CreatedAt >= fromDate
                && ii.Invoice.CreatedAt <= toDate)
            .Select(ii => new
            {
                Date = ii.Invoice.CreatedAt!.Value.Date,
                Cost = ii.Quantity * (ii.BranchProduct.CostPrice > 0 ? ii.BranchProduct.CostPrice : 0m)
            })
            .ToListAsync();

        var costByDate = invoiceItems
            .GroupBy(x => x.Date)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Cost));

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

            foreach (var day in dailyData)
            {
                var dailyCost = costByDate.GetValueOrDefault(day.Date, 0m);
                var grossProfit = day.Revenue - dailyCost;
                var profitMargin = day.Revenue > 0 ? (grossProfit / day.Revenue) * 100 : 0m;

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
                    TotalCost = dailyCost,
                    GrossProfit = grossProfit,
                    ProfitMargin = profitMargin
                });
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

            foreach (var week in weeklyData)
            {
                var weekStart = week.StartDate.Date;
                var weeklyCost = invoiceItems
                    .Where(x => x.Date >= weekStart && x.Date <= weekStart.AddDays(6))
                    .Sum(x => x.Cost);
                var grossProfit = week.Revenue - weeklyCost;
                var profitMargin = week.Revenue > 0 ? (grossProfit / week.Revenue) * 100 : 0m;

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
                    TotalCost = weeklyCost,
                    GrossProfit = grossProfit,
                    ProfitMargin = profitMargin
                });
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

            foreach (var month in monthlyData)
            {
                var monthName = new DateTime(month.Year, month.Month, 1).ToString("MMMM", new System.Globalization.CultureInfo("vi-VN"));
                var monthStart = new DateTime(month.Year, month.Month, 1);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                var monthlyCost = invoiceItems
                    .Where(x => x.Date >= monthStart.Date && x.Date <= monthEnd.Date)
                    .Sum(x => x.Cost);
                var grossProfit = month.Revenue - monthlyCost;
                var profitMargin = month.Revenue > 0 ? (grossProfit / month.Revenue) * 100 : 0m;

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
                    TotalCost = monthlyCost,
                    GrossProfit = grossProfit,
                    ProfitMargin = profitMargin
                });
            }
        }

        var totalRevenue = orders.Sum(o => o.Revenue);
        var totalCost = invoiceItems.Sum(x => x.Cost);
        var totalGrossProfit = totalRevenue - totalCost;
        var averageProfitMargin = totalRevenue > 0 ? (totalGrossProfit / totalRevenue) * 100 : 0m;

        var result = new RevenueByTimeReportDto
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            BranchId = branchId.Value,
            BranchName = branch?.BranchName,
            TimeGrouping = request.TimeGrouping,
            ChartData = chartData,
            TableData = tableData,
            TotalRevenue = totalRevenue,
            TotalOrders = orders.Count,
            TotalGrossProfit = totalGrossProfit,
            AverageProfitMargin = averageProfitMargin,
            AverageOrderValue = orders.Count > 0 ? totalRevenue / orders.Count : 0m
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

        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == request.SupplierId && s.BranchId == branchId);
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
            .Where(st => st.SupplierId == request.SupplierId && st.BranchId == branchId && st.TicketType == "PURCHASE");
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
    /// <summary>
    /// Báo cáo Thẻ kho chi tiết cho một sản phẩm (Stock Ledger)
    /// </summary>
    public async Task<StockLedgerDto> GetStockLedgerAsync(Guid branchProductId, StockLedgerRequestDto request, ClaimsPrincipal currentUser)
    {
        var branchId = await ResolveBranchIdAsync(currentUser);
        if (branchId == null) throw new UnauthorizedAccessException("Không xác định được chi nhánh.");

        var bp = await _context.BranchProducts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => (x.BranchProductId == branchProductId || x.ProductId == branchProductId) && x.BranchId == branchId.Value);

        if (bp == null) throw new KeyNotFoundException("Sản phẩm không thuộc cửa hàng.");

        var fromDate = request.FromDate?.Date ?? DateTime.MinValue;
        var toDate = request.ToDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue;

        var allEntries = new List<StockLedgerEntryDto>();

        // 1. Stock Tickets
        var tickets = await _context.StockTicketItems
            .Include(sti => sti.StockTicket)
            .Where(sti => sti.BranchProductId == bp.BranchProductId
                && (sti.StockTicket.Status == "COMPLETED" || sti.StockTicket.Status == "Completed")
                && sti.StockTicket.CreatedAt != null)
            .Select(sti => new
            {
                CreatedAt = sti.StockTicket.CreatedAt!.Value,
                TicketCode = sti.StockTicket.TicketCode,
                TicketType = sti.StockTicket.TicketType,
                UnitPrice = sti.UnitPrice,
                Discrepancy = sti.Discrepancy
            })
            .ToListAsync();

        foreach (var t in tickets)
        {
            var isOut = t.TicketType == "RETURN_SUPPLIER" || t.TicketType == "WRITE_OFF" || t.TicketType == "TRANSFER" || (t.TicketType == "BALANCE_ADJUST" && t.Discrepancy < 0);
            var isIn = t.TicketType == "PURCHASE" || t.TicketType == "CUSTOMER_RETURN" || (t.TicketType == "BALANCE_ADJUST" && t.Discrepancy > 0);

            decimal qtyIn = isIn ? Math.Abs(t.Discrepancy) : 0;
            decimal qtyOut = isOut ? Math.Abs(t.Discrepancy) : 0;

            allEntries.Add(new StockLedgerEntryDto
            {
                TransactionTime = t.CreatedAt,
                DocCode = t.TicketCode ?? "",
                DocType = t.TicketType ?? "",
                UnitCostPrice = t.UnitPrice,
                QtyIn = qtyIn,
                QtyOut = qtyOut
            });
        }

        // 2. Orders (Sales)
        var orders = await _context.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.BranchProductId == bp.BranchProductId
                && (oi.Order.Status == "COMPLETED" || oi.Order.Status == "Completed")
                && oi.Order.CreatedAt != null)
            .Select(oi => new
            {
                CreatedAt = oi.Order.CreatedAt!.Value,
                TicketCode = oi.Order.OrderCode,
                Quantity = oi.Quantity
            })
            .ToListAsync();

        foreach (var o in orders)
        {
            allEntries.Add(new StockLedgerEntryDto
            {
                TransactionTime = o.CreatedAt,
                DocCode = o.TicketCode ?? "",
                DocType = "SALE",
                UnitCostPrice = bp.CostPrice,
                QtyIn = 0,
                QtyOut = o.Quantity
            });
        }

        // Sort ASC
        allEntries = allEntries.OrderBy(x => x.TransactionTime).ToList();

        decimal currentRunningStock = 0;
        decimal openingStock = 0;
        var periodEntries = new List<StockLedgerEntryDto>();
        decimal totalInQty = 0;
        decimal totalOutQty = 0;

        foreach (var entry in allEntries)
        {
            currentRunningStock += entry.QtyIn;
            currentRunningStock -= entry.QtyOut;
            entry.StockAfter = currentRunningStock;

            if (entry.TransactionTime < fromDate)
            {
                openingStock = currentRunningStock;
            }
            else if (entry.TransactionTime <= toDate)
            {
                periodEntries.Add(entry);
                totalInQty += entry.QtyIn;
                totalOutQty += entry.QtyOut;
            }
        }

        return new StockLedgerDto
        {
            OpeningStock = openingStock,
            ClosingStock = openingStock + totalInQty - totalOutQty,
            TotalInQty = totalInQty,
            TotalOutQty = totalOutQty,
            Entries = periodEntries.OrderBy(x => x.TransactionTime).Take(request.PageSize).ToList()
        };
    }
}
