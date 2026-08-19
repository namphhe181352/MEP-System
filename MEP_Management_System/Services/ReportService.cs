using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

/// <summary>
/// Report Service - chứa logic tính toán báo cáo.
/// Logic tính toán được tách rời từ Controller để dùng chung cho Web UI và Chatbot.
/// </summary>
public class ReportService : IReportService
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

        // 5. Lấy phiếu xuất kho trong ngày (RETURN_SUPPLIER, WRITE_OFF, TRANSFER, SALE)
        var outwardTickets = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER" || st.TicketType == "SALE")
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

        // 6b. Bổ sung: Chênh lệch thu thêm từ Đổi hàng (EXCHANGE)
        var exchangeReturns = await _context.ReturnOrders
            .Where(r => r.Order.BranchId == branchId.Value
                && r.ReturnType == "EXCHANGE"
                && (r.Status == "COMPLETED" || r.Status == "Completed")
                && r.CreatedAt >= reportDate
                && r.CreatedAt < nextDate)
            .ToListAsync();
        var exchangeDeltaIn = exchangeReturns.Sum(r => r.DeltaAmount ?? 0m);

        // 7. Lấy chi phí (phiếu chi tiền) trong ngày - chỉ tính CONFIRMED
        var expenses = await _context.ExpenseVouchers
            .Where(ev => ev.BranchId == branchId.Value
                && (ev.Status == "COMPLETED" || ev.Status == "Completed")
                && ev.CreatedAt >= reportDate
                && ev.CreatedAt < nextDate)
            .ToListAsync();
        var totalExpense = expenses.Sum(ev => ev.Amount);

        // Tính tổng doanh thu THUẦN (Gross - Refunds + ExchangeDelta) để khớp với POS Shift
        var grossRevenue = orders.Sum(o => o.TotalAmount);
        var totalRevenue = grossRevenue - (refundCash + refundTransfer) + exchangeDeltaIn;

        // Tính lợi nhuận ròng = Doanh thu thuần - Chi phí
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

        // Lấy danh sách sản phẩm trong kho (bao gồm cả sản phẩm đã xóa nếu có giao dịch trong kỳ)
        var productsQuery = _context.BranchProducts
            .Include(bp => bp.Product)
            .Where(bp => bp.BranchId == branchId.Value
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

            // Nhập trong kỳ: Mọi giao dịch làm tăng tồn kho (không phải Điều chỉnh/Kiểm kê)
            var inData = await _context.InventoryLedgers
                .Where(l => l.BranchId == branchId.Value && l.TransactionTime >= fromDate && l.TransactionTime <= toDate
                    && l.QtyChange > 0
                    && l.DocType != "BALANCE_ADJUST" && l.DocType != "INVENTORY_CHECK")
                .GroupBy(l => l.BranchProductId)
                .Select(g => new { BranchProductId = g.Key, Qty = g.Sum(l => l.QtyChange), Value = g.Sum(l => l.TotalCostValue) })
                .ToListAsync();
            inwardQty = inData.ToDictionary(x => x.BranchProductId, x => x.Qty);
            inwardValue = inData.ToDictionary(x => x.BranchProductId, x => x.Value);

            // Xuất trong kỳ: Mọi giao dịch làm giảm tồn kho (không phải Điều chỉnh/Kiểm kê)
            var outData = await _context.InventoryLedgers
                .Where(l => l.BranchId == branchId.Value && l.TransactionTime >= fromDate && l.TransactionTime <= toDate
                    && l.QtyChange < 0
                    && l.DocType != "BALANCE_ADJUST" && l.DocType != "INVENTORY_CHECK")
                .GroupBy(l => l.BranchProductId)
                .Select(g => new { BranchProductId = g.Key, Qty = g.Sum(l => -l.QtyChange), Value = g.Sum(l => -l.TotalCostValue) })
                .ToListAsync();
            outwardQty = outData.ToDictionary(x => x.BranchProductId, x => x.Qty);
            outwardValue = outData.ToDictionary(x => x.BranchProductId, x => x.Value);

            // Điều chỉnh: BALANCE_ADJUST + INVENTORY_CHECK
            var adjData = await _context.InventoryLedgers
                .Where(l => l.BranchId == branchId.Value && l.TransactionTime >= fromDate && l.TransactionTime <= toDate 
                    && (l.DocType == "BALANCE_ADJUST" || l.DocType == "INVENTORY_CHECK"))
                .GroupBy(l => l.BranchProductId)
                .Select(g => new { BranchProductId = g.Key, Qty = g.Sum(l => l.QtyChange), Value = g.Sum(l => l.TotalCostValue) })
                .ToListAsync();
            adjustQty = adjData.ToDictionary(x => x.BranchProductId, x => x.Qty);
            adjustValue = adjData.ToDictionary(x => x.BranchProductId, x => x.Value);

            // Tong bien dong SAU ngay ket thuc (toDate -> hien tai)
            var afterData = await _context.InventoryLedgers
                .Where(l => l.BranchId == branchId.Value && l.TransactionTime > toDate)
                .GroupBy(l => l.BranchProductId)
                .Select(g => new { BranchProductId = g.Key, Qty = g.Sum(l => l.QtyChange) })
                .ToListAsync();
            var afterQty = afterData.ToDictionary(x => x.BranchProductId, x => x.Qty);

            // Build result
            var items = new List<StockMovementItemDto>();
            decimal totalOpeningV = 0, totalInwardV = 0, totalOutwardV = 0, totalAdjustV = 0, totalClosingV = 0;

            foreach (var bp in products)
            {
                var bpid = bp.BranchProductId;
                var inQ = inwardQty.GetValueOrDefault(bpid, 0);
                var inV = inwardValue.GetValueOrDefault(bpid, 0);
                var outQ = outwardQty.GetValueOrDefault(bpid, 0);
                var outV = outwardValue.GetValueOrDefault(bpid, 0);
                var adjQ = adjustQty.GetValueOrDefault(bpid, 0);
                var adjV = adjustValue.GetValueOrDefault(bpid, 0);

                var isDeleted = bp.IsActive == -1 || bp.Product?.Status == -1;

                var effectiveStock = isDeleted ? 0 : bp.ActualStock;
                var closingQ = effectiveStock - afterQty.GetValueOrDefault(bpid, 0);
                var closingV = Math.Round(closingQ * bp.CostPrice, 2);
                var openQ = closingQ - inQ + outQ - adjQ;
                var openV = closingV - inV + outV - adjV;

                if (openQ == 0 && inQ == 0 && outQ == 0 && adjQ == 0 && closingQ == 0)
                    continue;

                items.Add(new StockMovementItemDto
                {
                    ProductId = bp.ProductId,
                    BranchProductId = bp.BranchProductId,
                    ProductCode = bp.Product?.ProductCode ?? "",
                    ProductName = bp.Product?.ProductName ?? "",
                    CategoryName = bp.Product?.CategoryName ?? "",
                    BaseUnit = bp.Product?.BaseUnit ?? "",
                    IsDeleted = isDeleted,
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
        // Pre-period queries
        var inwardPurchaseBefore = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && st.TicketType == "PURCHASE" && st.CreatedAt < fromDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        var inwardReturnBefore = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && st.TicketType == "CUSTOMER_RETURN" && st.CreatedAt < fromDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        var outwardTicketsBefore = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER") && !(st.Notes != null && st.Notes.Contains("INVENTORY_ADJUSTMENT")) && st.CreatedAt < fromDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        var adjustmentBefore = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && (st.TicketType == "BALANCE_ADJUST" || st.TicketType == "INVENTORY_CHECK" || (st.TicketType == "WRITE_OFF" && st.Notes != null && st.Notes.Contains("INVENTORY_ADJUSTMENT"))) && st.CreatedAt < fromDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.StockTicket != null && s.StockTicket.TicketType == "WRITE_OFF" ? -s.Discrepancy : s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);

        var salesBefore = await _context.InvoiceItems.Where(ii => ii.Invoice.Order.BranchId == branchId && ii.Invoice.Status == "Completed" && ii.Invoice.CreatedAt < fromDate).GroupBy(ii => ii.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(ii => ii.Quantity * (ii.ConvertValue ?? 1m)), Value = g.Sum(ii => (ii.Quantity * (ii.ConvertValue ?? 1m)) * (ii.CostPrice > 0 ? ii.CostPrice : ii.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => new { x.Qty, x.Value });

        // In-period queries
        var inwardPurchaseIn = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && st.TicketType == "PURCHASE" && st.CreatedAt >= fromDate && st.CreatedAt <= toDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        var inwardReturnIn = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && st.TicketType == "CUSTOMER_RETURN" && st.CreatedAt >= fromDate && st.CreatedAt <= toDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        var outwardTicketsIn = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER") && !(st.Notes != null && st.Notes.Contains("INVENTORY_ADJUSTMENT")) && st.CreatedAt >= fromDate && st.CreatedAt <= toDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);
        var adjustmentIn = await _context.StockTickets.Where(st => st.BranchId == branchId && st.Status == "COMPLETED" && (st.TicketType == "BALANCE_ADJUST" || st.TicketType == "INVENTORY_CHECK" || (st.TicketType == "WRITE_OFF" && st.Notes != null && st.Notes.Contains("INVENTORY_ADJUSTMENT"))) && st.CreatedAt >= fromDate && st.CreatedAt <= toDate).SelectMany(st => st.StockTicketItems).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(s => s.StockTicket != null && s.StockTicket.TicketType == "WRITE_OFF" ? -s.Discrepancy : s.Discrepancy) }).ToDictionaryAsync(x => x.Key, x => x.Qty);

        var salesIn = await _context.InvoiceItems.Where(ii => ii.Invoice.Order.BranchId == branchId && ii.Invoice.Status == "Completed" && ii.Invoice.CreatedAt >= fromDate && ii.Invoice.CreatedAt <= toDate).GroupBy(ii => ii.BranchProductId).Select(g => new { g.Key, Qty = g.Sum(ii => ii.Quantity * (ii.ConvertValue ?? 1m)), Value = g.Sum(ii => (ii.Quantity * (ii.ConvertValue ?? 1m)) * (ii.CostPrice > 0 ? ii.CostPrice : ii.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => new { x.Qty, x.Value });

        var purchaseValueIn = await _context.StockTicketItems.Where(sti => sti.StockTicket.BranchId == branchId && sti.StockTicket.Status == "COMPLETED" && sti.StockTicket.TicketType == "PURCHASE" && sti.StockTicket.CreatedAt >= fromDate && sti.StockTicket.CreatedAt <= toDate).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Value = g.Sum(s => s.Discrepancy * (s.UnitPrice ?? s.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => x.Value);
        var returnValueIn = await _context.StockTicketItems.Where(sti => sti.StockTicket.BranchId == branchId && sti.StockTicket.Status == "COMPLETED" && sti.StockTicket.TicketType == "CUSTOMER_RETURN" && sti.StockTicket.CreatedAt >= fromDate && sti.StockTicket.CreatedAt <= toDate).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Value = g.Sum(s => s.Discrepancy * (s.UnitPrice ?? s.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => x.Value);
        var outwardValueIn = await _context.StockTicketItems.Where(sti => sti.StockTicket.BranchId == branchId && sti.StockTicket.Status == "COMPLETED" && (sti.StockTicket.TicketType == "RETURN_SUPPLIER" || sti.StockTicket.TicketType == "WRITE_OFF" || sti.StockTicket.TicketType == "TRANSFER") && sti.StockTicket.CreatedAt >= fromDate && sti.StockTicket.CreatedAt <= toDate).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Value = g.Sum(s => s.Discrepancy * (s.UnitPrice ?? s.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => x.Value);

        var purchaseValueBefore = await _context.StockTicketItems.Where(sti => sti.StockTicket.BranchId == branchId && sti.StockTicket.Status == "COMPLETED" && sti.StockTicket.TicketType == "PURCHASE" && sti.StockTicket.CreatedAt < fromDate).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Value = g.Sum(s => s.Discrepancy * (s.UnitPrice ?? s.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => x.Value);
        var returnValueBefore = await _context.StockTicketItems.Where(sti => sti.StockTicket.BranchId == branchId && sti.StockTicket.Status == "COMPLETED" && sti.StockTicket.TicketType == "CUSTOMER_RETURN" && sti.StockTicket.CreatedAt < fromDate).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Value = g.Sum(s => s.Discrepancy * (s.UnitPrice ?? s.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => x.Value);
        var outwardValueBefore = await _context.StockTicketItems.Where(sti => sti.StockTicket.BranchId == branchId && sti.StockTicket.Status == "COMPLETED" && (sti.StockTicket.TicketType == "RETURN_SUPPLIER" || sti.StockTicket.TicketType == "WRITE_OFF" || sti.StockTicket.TicketType == "TRANSFER") && sti.StockTicket.CreatedAt < fromDate).GroupBy(sti => sti.BranchProductId).Select(g => new { g.Key, Value = g.Sum(s => s.Discrepancy * (s.UnitPrice ?? s.BranchProduct.CostPrice)) }).ToDictionaryAsync(x => x.Key, x => x.Value);

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
            var closeV = Math.Round(closeQ * bp.CostPrice, 2);

            var isDeletedLegacy = bp.IsActive == -1 || bp.Product?.Status == -1;

            var displayCloseQ = isDeletedLegacy ? 0 : closeQ;
            var displayCloseV = isDeletedLegacy ? 0 : closeV;

            if (openQ == 0 && inQ == 0 && outQ == 0 && adjQ == 0 && displayCloseQ == 0)
                continue;

            items.Add(new StockMovementItemDto
            {
                ProductId = bp.ProductId, BranchProductId = bpid,
                ProductCode = bp.Product?.ProductCode ?? "", ProductName = bp.Product?.ProductName ?? "",
                CategoryName = bp.Product?.CategoryName ?? "", BaseUnit = bp.Product?.BaseUnit ?? "",
                IsDeleted = isDeletedLegacy,
                OpeningStock = openQ, OpeningValue = Math.Round(openV, 2),
                InwardQuantity = inQ, InwardValue = Math.Round(inV, 2),
                OutwardQuantity = outQ, OutwardValue = Math.Round(outV, 2),
                AdjustmentQuantity = adjQ, AdjustmentValue = Math.Round(adjV, 2),
                ClosingStock = displayCloseQ, ClosingValue = displayCloseV
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
    /// </summary>
    public async Task<StockMovementReportDto> GetStockMovementReportAsync(StockMovementReportRequestDto request, ClaimsPrincipal currentUser)
    {
        var branchId = await ResolveBranchIdAsync(currentUser);
        if (branchId == null)
        {
            throw new UnauthorizedAccessException("Không xác định được chi nhánh từ user hiện tại.");
        }

        if (request.FromDate > request.ToDate)
        {
            throw new ArgumentException("Ngày bắt đầu không thể lớn hơn ngày kết thúc.");
        }

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);

        var fromDate = request.FromDate.Date;
        var toDate = request.ToDate.Date.AddDays(1).AddTicks(-1);

        var productsQuery = _context.BranchProducts
            .Include(bp => bp.Product)
            .Where(bp => bp.BranchId == branchId.Value &&
                         (bp.Product.BranchId == null || bp.Product.BranchId == branchId.Value));

        if (request.CategoryId.HasValue && request.CategoryId != Guid.Empty)
        {
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

        if (request.ProductId.HasValue)
        {
            productsQuery = productsQuery.Where(bp => bp.ProductId == request.ProductId.Value);
        }

        var products = await productsQuery.ToListAsync();

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

        var inwardReturnBeforePeriod = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && st.TicketType == "CUSTOMER_RETURN"
                && st.CreatedAt < fromDate)
            .SelectMany(st => st.StockTicketItems)
            .GroupBy(sti => sti.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, TotalInward = g.Sum(sti => sti.Discrepancy) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalInward);

        var salesBeforePeriod = await _context.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Order.BranchId == branchId.Value
                && (oi.Order.Status == "COMPLETED" || oi.Order.Status == "Completed")
                && oi.Order.CreatedAt < fromDate)
            .GroupBy(oi => oi.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, TotalOutward = g.Sum(oi => oi.Quantity) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalOutward);

        var outwardTicketsBeforePeriod = await _context.StockTickets
            .Where(st => st.BranchId == branchId.Value
                && (st.Status == "COMPLETED" || st.Status == "Completed")
                && (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER")
                && st.CreatedAt < fromDate)
            .SelectMany(st => st.StockTicketItems)
            .GroupBy(sti => sti.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, TotalOutward = g.Sum(sti => sti.Discrepancy) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalOutward);

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

        var salesInPeriod = await _context.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Order.BranchId == branchId.Value
                && (oi.Order.Status == "COMPLETED" || oi.Order.Status == "Completed")
                && oi.Order.CreatedAt >= fromDate
                && oi.Order.CreatedAt <= toDate)
            .GroupBy(oi => oi.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, TotalOutward = g.Sum(oi => oi.Quantity) })
            .ToDictionaryAsync(x => x.BranchProductId, x => x.TotalOutward);

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

            var inwardPurchaseBefore = inwardPurchaseBeforePeriod.GetValueOrDefault(branchProductId, 0);
            var inwardReturnBefore = inwardReturnBeforePeriod.GetValueOrDefault(branchProductId, 0);
            var inwardBefore = inwardPurchaseBefore + inwardReturnBefore;
            var salesBefore = salesBeforePeriod.GetValueOrDefault(branchProductId, 0);
            var outwardBefore = outwardTicketsBeforePeriod.GetValueOrDefault(branchProductId, 0);
            var openingStock = inwardBefore - (salesBefore + outwardBefore);

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

            openingStock = openingStock + adjustmentBefore;

            var closingStock = openingStock + inwardInPeriod2 - outwardInPeriod2 + adjustmentIn;

            var inwardPurchaseValueIn = inwardPurchaseValueInPeriod.GetValueOrDefault(branchProductId, 0);
            var inwardValue = inwardPurchaseValueIn;

            var salesValueIn = salesValueInPeriod.GetValueOrDefault(branchProductId, 0);
            var outwardValue = salesValueIn + outwardValueMap.GetValueOrDefault(branchProductId, 0);

            var inwardPurchaseValueBefore = inwardPurchaseValueBeforePeriod.GetValueOrDefault(branchProductId, 0);
            var salesValueBefore = salesValueBeforePeriod2.GetValueOrDefault(branchProductId, 0);
            var outwardValueBefore = outwardValueBeforePeriod2.GetValueOrDefault(branchProductId, 0);
            var openingValue = inwardPurchaseValueBefore - salesValueBefore - outwardValueBefore + (adjustmentBefore * bp.CostPrice);
            var adjustmentValueForItem = adjustmentValue;
            var closingValue = openingValue + inwardValue - outwardValue + adjustmentValueForItem;

            var isDeletedCopy = bp.IsActive == -1 || bp.Product?.Status == -1;

            var displayClosingStock = isDeletedCopy ? 0 : closingStock;
            var displayClosingValue = isDeletedCopy ? 0 : closingValue;

            if (openingStock == 0 && inwardInPeriod2 == 0 && outwardInPeriod2 == 0 && adjustmentIn == 0 && displayClosingStock == 0)
                continue;

            items.Add(new StockMovementItemDto
            {
                ProductId = bp.ProductId,
                BranchProductId = bp.BranchProductId,
                ProductCode = bp.Product?.ProductCode ?? "",
                ProductName = bp.Product?.ProductName ?? "",
                CategoryName = bp.Product?.CategoryName ?? "",
                BaseUnit = bp.Product?.BaseUnit ?? "",
                IsDeleted = isDeletedCopy,
                OpeningStock = openingStock,
                InwardQuantity = inwardInPeriod2,
                OutwardQuantity = outwardInPeriod2,
                AdjustmentQuantity = adjustmentIn,
                ClosingStock = displayClosingStock,
                OpeningValue = openingValue,
                InwardValue = inwardValue,
                OutwardValue = outwardValue,
                AdjustmentValue = adjustmentValueForItem,
                ClosingValue = displayClosingValue
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
                Cost = ii.Quantity * (ii.ConvertValue ?? 1m) * (ii.CostPrice > 0 ? ii.CostPrice : (ii.BranchProduct.CostPrice > 0 ? ii.BranchProduct.CostPrice : 0m))
            })
            .ToListAsync();

        var costByDate = invoiceItems
            .GroupBy(x => x.Date)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Cost));

        var chartData = new List<ReportRevenueChartPointDto>();
        var tableData = new List<RevenueTimeItemDto>();

        if (normalizedGrouping == "day")
        {
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

    private int GetWeekNumber(DateTime date)
    {
        var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
        return cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }

    /// <summary>
    /// Lấy báo cáo tồn kho sắp hết - sản phẩm dưới MinStock với mức độ tốc (Critical/Warning/Low).
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
                && bp.IsActive == 1
                && (bp.Product.Status == null || bp.Product.Status != -1)
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
            .OrderBy(x => x.Shortage)
            .ToList();

        return new LowStockReportDto
        {
            BranchId = branchId.Value,
            BranchName = branch?.BranchName,
            Items = result,
            TotalItems = result.Count
        };
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
    /// Lấy báo cáo doanh thu/lợi nhuận theo sản phẩm.
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

        var filteredItems = invoiceItems;

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

        var productData = filteredItems
            .GroupBy(ii => new
            {
                ii.BranchProduct.ProductId,
                ii.BranchProduct.Product.ProductCode,
                ii.BranchProduct.Product.ProductName,
                ii.BranchProduct.Product.CategoryName,
            })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductCode,
                g.Key.ProductName,
                g.Key.CategoryName,
                QuantitySold = g.Sum(ii => ii.Quantity * (ii.ConvertValue ?? 1m)),
                Revenue = g.Sum(ii => ii.Quantity * ii.SellPrice),
                Cost = g.Sum(ii => ii.Quantity * (ii.ConvertValue ?? 1m) * (ii.CostPrice > 0 ? ii.CostPrice : ii.BranchProduct.CostPrice))
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

        var sortBy = string.IsNullOrWhiteSpace(request.SortBy) ? "revenue" : request.SortBy.Trim().ToLowerInvariant();
        items = sortBy switch
        {
            "profit" => items.OrderByDescending(x => x.Profit).ToList(),
            "quantity" => items.OrderByDescending(x => x.QuantitySold).ToList(),
            _ => items.OrderByDescending(x => x.Revenue).ToList()
        };

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

        var poQuery = _context.PurchaseOrders.Where(po => po.SupplierId == request.SupplierId);
        if (request.PurchaseFromDate.HasValue)
            poQuery = poQuery.Where(po => po.CreatedAt >= request.PurchaseFromDate.Value);
        if (request.PurchaseToDate.HasValue)
            poQuery = poQuery.Where(po => po.CreatedAt <= request.PurchaseToDate.Value);
        var purchaseOrders = await poQuery
            .Select(po => new { po.PurchaseOrderId, po.OrderCode, po.TotalAmount, po.Status, po.CreatedAt })
            .ToListAsync();

        var stQuery0 = _context.StockTickets
            .Where(st => st.SupplierId == request.SupplierId && st.BranchId == branchId && st.TicketType == "PURCHASE");
        if (request.PurchaseFromDate.HasValue)
            stQuery0 = stQuery0.Where(st => (st.CompletedAt ?? st.CreatedAt) >= request.PurchaseFromDate.Value);
        if (request.PurchaseToDate.HasValue)
            stQuery0 = stQuery0.Where(st => (st.CompletedAt ?? st.CreatedAt) <= request.PurchaseToDate.Value);
        var stockTickets = await stQuery0
            .Select(st => new { st.StockTicketId, st.TicketCode, TotalAmount = (decimal?)st.TotalAmount, st.Status, CreatedAt = (DateTime?)(st.CompletedAt ?? st.CreatedAt) })
            .ToListAsync();

        var stDebt = stockTickets.Where(st => st.Status == "COMPLETED").Sum(st => st.TotalAmount ?? 0m);
        var poUnconfirmedDebt = purchaseOrders.Where(po => po.Status == "APPROVED" || po.Status == "PENDING")
            .Sum(po => po.TotalAmount ?? 0m);
        var totalDebt = stDebt + poUnconfirmedDebt;

        var totalPurchaseAllTime = stockTickets.Where(st => st.Status == "COMPLETED").Sum(st => st.TotalAmount ?? 0m);

        var poPending = purchaseOrders.Count(po => po.Status == "PENDING" || po.Status == "APPROVED");
        var stPending = stockTickets.Count(st => st.Status == "PENDING");
        int pendingOrders = poPending + stPending;

        var paymentsQuery = _context.SupplierPayments.Where(sp => sp.SupplierId == request.SupplierId);
        if (request.PaymentFromDate.HasValue)
            paymentsQuery = paymentsQuery.Where(sp => sp.CreatedAt >= request.PaymentFromDate.Value);
        if (request.PaymentToDate.HasValue)
            paymentsQuery = paymentsQuery.Where(sp => sp.CreatedAt <= request.PaymentToDate.Value);
        var allPayments = await paymentsQuery.ToListAsync();
        // Tổng đã thanh toán chỉ tính phiếu còn hiệu lực (loại phiếu chi đã hủy IsDeleted=1 -> khôi phục dư nợ).
        var totalPaidAllTime = allPayments.Where(sp => sp.IsDeleted == 0).Sum(sp => sp.Amount);

        var monthlyDebts = new List<MonthlyDebtInfoDto>();
        var culture = new System.Globalization.CultureInfo("vi-VN");

        if (request.Year.HasValue)
        {
            var year = request.Year.Value;
            var month = request.Month;

            var filteredPO = purchaseOrders
                .Where(po => po.CreatedAt.HasValue && po.CreatedAt.Value.Year == year)
                .ToList();
            if (month.HasValue)
                filteredPO = filteredPO.Where(po => po.CreatedAt.Value.Month == month.Value).ToList();

            var filteredST = stockTickets
                .Where(st => st.CreatedAt.HasValue && st.CreatedAt.Value.Year == year)
                .ToList();
            if (month.HasValue)
                filteredST = filteredST.Where(st => st.CreatedAt.Value.Month == month.Value).ToList();

            var allPurchases = filteredPO.Select(po => new { TotalAmount = po.TotalAmount ?? 0m, CreatedAt = po.CreatedAt })
                .Concat(filteredST.Select(st => new { TotalAmount = st.TotalAmount ?? 0m, CreatedAt = st.CreatedAt }))
                .Where(x => x.CreatedAt.HasValue)
                .ToList();

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
                    .Where(sp => sp.IsDeleted == 0 && sp.CreatedAt.Year == group.Key.Year && sp.CreatedAt.Month == group.Key.Month)
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

        var paymentHistory = await _context.SupplierPayments
            .Where(sp => sp.SupplierId == request.SupplierId)
            .Where(sp => !fromDate.HasValue || sp.CreatedAt >= fromDate.Value)
            .Where(sp => !toDate.HasValue || sp.CreatedAt <= toDate.Value)
            .OrderByDescending(sp => sp.CreatedAt)
            .Select(sp => new { sp.PaymentId, sp.CreatedAt, sp.Amount, sp.Note, sp.IsDeleted })
            .ToListAsync();

        var paymentHistoryDto = paymentHistory.Select(p => new SupplierPaymentHistoryItemDto
        {
            PaymentId = p.PaymentId,
            CreatedAt = p.CreatedAt,
            Amount = p.Amount,
            Note = p.Note,
            Status = p.IsDeleted == 1 ? "CANCELLED" : "ACTIVE"
        }).ToList();

        // Tổng đã thanh toán (trong kỳ) chỉ tính phiếu còn hiệu lực.
        var totalPaid = paymentHistoryDto.Where(x => x.Status != "CANCELLED").Sum(x => x.Amount);
        int totalPayments = paymentHistoryDto.Count(x => x.Status != "CANCELLED");

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

        // Bao gom ca phiếu CANCELLED de the kho hien thi dong giao dich goc (khi tung COMPLETED)
        // + dong but toan dao (Huy) voi ly do. Phiếu CANCELLED ma chua tung CompletedAt (pending->huy)
        // khong phat sinh dong nao (chua tinh ton kho).
        var tickets = await _context.StockTicketItems
            .Include(sti => sti.StockTicket)
            .Where(sti => sti.BranchProductId == bp.BranchProductId
                && (sti.StockTicket.Status == "COMPLETED" || sti.StockTicket.Status == "Completed"
                    || sti.StockTicket.Status == "CANCELLED" || sti.StockTicket.Status == "Cancelled")
                && sti.StockTicket.CreatedAt != null)
            .Select(sti => new
            {
                CreatedAt = sti.StockTicket.CreatedAt!.Value,
                CompletedAt = sti.StockTicket.CompletedAt,
                CancelledAt = sti.StockTicket.CancelledAt,
                CancelReason = sti.StockTicket.CancelReason,
                Status = sti.StockTicket.Status,
                TicketCode = sti.StockTicket.TicketCode,
                TicketType = sti.StockTicket.TicketType,
                UnitPrice = sti.UnitPrice,
                Discrepancy = sti.Discrepancy
            })
            .ToListAsync();

        foreach (var t in tickets)
        {
            var isOut = t.TicketType == "RETURN_SUPPLIER" || t.TicketType == "WRITE_OFF" || t.TicketType == "TRANSFER" || t.TicketType == "SALE" || ((t.TicketType == "BALANCE_ADJUST" || t.TicketType == "INVENTORY_CHECK") && t.Discrepancy < 0);
            var isIn = t.TicketType == "PURCHASE" || t.TicketType == "CUSTOMER_RETURN" || ((t.TicketType == "BALANCE_ADJUST" || t.TicketType == "INVENTORY_CHECK") && t.Discrepancy > 0);

            var statusUpper = (t.Status ?? "").ToUpperInvariant();
            var isCancelled = statusUpper == "CANCELLED";

            // SALE ticket item.UnitPrice = giá BÁN net (không phải giá vốn) -> cột "Đơn giá vốn"
            // phải dùng bp.CostPrice. Các loại khác dùng UnitPrice của ticket item.
            var unitCostForDisplay = (t.TicketType == "SALE") ? bp.CostPrice : t.UnitPrice;

            // Entry 1: giao dich goc — luon hien thi khi phiếu tung COMPLETED (CompletedAt != null).
            // Voi phiếu pending->huy (CompletedAt == null) thi bo qua (chua tinh ton kho).
            if (!isCancelled || t.CompletedAt != null)
            {
                decimal qtyIn = isIn ? Math.Abs(t.Discrepancy) : 0;
                decimal qtyOut = isOut ? Math.Abs(t.Discrepancy) : 0;
                allEntries.Add(new StockLedgerEntryDto
                {
                    TransactionTime = t.CompletedAt ?? t.CreatedAt,
                    DocCode = t.TicketCode ?? "",
                    DocType = t.TicketType ?? "",
                    UnitCostPrice = unitCostForDisplay,
                    QtyIn = qtyIn,
                    QtyOut = qtyOut
                });
            }

            // Entry 2: but toan DAO khi HUY phiếu da COMPLETED — dao chieu qty (nhap huy -> xuat,
            // xuat huy -> nhap) de ton chay dung trong suot giua CompletedAt va CancelledAt.
            // Giu nguyen DocType goc + IsCancellation=true de FE to mau rieng (cam) va label
            // "Hủy nhập NCC" / "Hủy xuất..." phan biet voi giao dich thuong.
            if (isCancelled && t.CancelledAt != null)
            {
                decimal qtyIn = isOut ? Math.Abs(t.Discrepancy) : 0;
                decimal qtyOut = isIn ? Math.Abs(t.Discrepancy) : 0;
                allEntries.Add(new StockLedgerEntryDto
                {
                    TransactionTime = t.CancelledAt!.Value,
                    DocCode = t.TicketCode ?? "",
                    DocType = t.TicketType ?? "",
                    UnitCostPrice = unitCostForDisplay,
                    QtyIn = qtyIn,
                    QtyOut = qtyOut,
                    Note = string.IsNullOrWhiteSpace(t.CancelReason) ? "Hủy phiếu" : ("Hủy: " + t.CancelReason),
                    IsCancellation = true
                });
            }
        }

        // SALE giờ đọc từ stock_ticket_items (TicketType=SALE, isOut) cùng logic Entry1/Entry2
        // với lý do hủy (CancelReason) — loại bỏ nguồn OrderItems cũ để tránh trùng lặp & dùng
        // đơn vị gốc (discrepancy = số lượng bán theo đơn vị cơ sở, khớp ledger).

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

    /// <summary>
    /// Lấy thẻ kho chi tiết với đầy đủ thông tin sản phẩm.
    /// </summary>
    public async Task<object> GetStockLedgerDetailAsync(Guid branchProductId, DateTime? fromDate, DateTime? toDate, ClaimsPrincipal currentUser)
    {
        var branchId = await ResolveBranchIdAsync(currentUser);
        if (branchId == null)
            throw new BusinessException("Không xác định được chi nhánh.");

        var from = fromDate?.Date ?? DateTime.UtcNow.AddDays(-30).Date;
        var to = (toDate?.Date ?? DateTime.UtcNow.Date).AddDays(1).AddTicks(-1);

        var bp = await _context.BranchProducts
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.BranchProductId == branchProductId && x.BranchId == branchId);

        if (bp == null)
            throw new KeyNotFoundException("Không tìm thấy sản phẩm trong kho.");

        decimal openingStock = 0;
        var ledgerBefore = await _context.InventoryLedgers
            .Where(l => l.BranchProductId == branchProductId && l.BranchId == branchId && l.TransactionTime < from)
            .SumAsync(l => (decimal?)l.QtyChange) ?? 0;

        var ledgerIn = await _context.InventoryLedgers
            .Where(l => l.BranchProductId == branchProductId && l.BranchId == branchId
                && l.TransactionTime >= from && l.TransactionTime <= to)
            .OrderBy(l => l.TransactionTime)
            .Select(l => new { l.TransactionTime, l.DocCode, l.DocType, l.UnitCostPrice, l.QtyChange })
            .ToListAsync();

        var entries = new List<(DateTime Time, string Doc, string Type, decimal Price, decimal Qty)>();
        DateTime safeDt(DateTime? d) => d ?? DateTime.UtcNow;

        if (ledgerIn.Count > 0)
        {
            foreach (var e in ledgerIn)
            {
                var price = (e.DocType == "SALE" || e.DocType == "CUSTOMER_RETURN" || e.DocType == "WRITE_OFF" || e.DocType == "TRANSFER" || e.DocType == "EXCHANGE_OUT" || e.DocType == "CUSTOMER_RETURN_DEFECTIVE")
                    ? (e.UnitCostPrice > 0 && e.UnitCostPrice <= bp.CostPrice * 1.5m ? e.UnitCostPrice : bp.CostPrice)
                    : e.UnitCostPrice;
                entries.Add((e.TransactionTime, e.DocCode ?? "", e.DocType, price, e.QtyChange));
            }

            var qtyAfterToDate = await _context.InventoryLedgers
                .Where(l => l.BranchProductId == branchProductId && l.BranchId == branchId && l.TransactionTime > to)
                .SumAsync(l => (decimal?)l.QtyChange) ?? 0;

            var stockAtToDate = bp.ActualStock - qtyAfterToDate;
            var inPeriodTotal = entries.Sum(e => e.Qty);
            openingStock = stockAtToDate - inPeriodTotal;
        }
        else
        {
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

        var running = openingStock;
        var resultEntries = entries.Select(e =>
        {
            running += e.Qty;

            // Xác định xem giao dịch có phải là giao dịch hủy (đảo ngược) không.
            // Các phiếu XUẤT (SALE, WRITE_OFF, TRANSFER, EXCHANGE_OUT, RETURN_SUPPLIER) bình thường làm giảm tồn (Qty < 0). Nếu Qty > 0 -> Hủy xuất.
            // Các phiếu NHẬP (PURCHASE, CUSTOMER_RETURN, CUSTOMER_RETURN_DEFECTIVE, EXCHANGE_IN) bình thường làm tăng tồn (Qty > 0). Nếu Qty < 0 -> Hủy nhập.
            bool isOutType = e.Type == "SALE" || e.Type == "WRITE_OFF" || e.Type == "TRANSFER" || e.Type == "EXCHANGE_OUT" || e.Type == "RETURN_SUPPLIER" || e.Type == "SUPPLIER_RETURN" || e.Type == "STOCK_EXPORT";
            bool isInType = e.Type == "PURCHASE" || e.Type == "CUSTOMER_RETURN" || e.Type == "CUSTOMER_RETURN_DEFECTIVE" || e.Type == "EXCHANGE_IN" || e.Type == "STOCK_IMPORT";
            
            bool isCancel = (isOutType && e.Qty > 0) || (isInType && e.Qty < 0);

            return new
            {
                TransactionTime = e.Time,
                DocCode = e.Doc,
                DocType = e.Type,
                UnitCostPrice = e.Price,
                QtyIn = e.Qty > 0 ? e.Qty : 0m,
                QtyOut = e.Qty < 0 ? -e.Qty : 0m,
                StockAfter = running,
                IsCancellation = isCancel
            };
        }).ToList();

        return new
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
        };
    }

    /// <summary>
    /// Chốt tồn kho cuối ngày - lưu closing_quantity, cost_price, closing_value cho toàn bộ sản phẩm.
    /// </summary>
    public async Task<object> CreateDailySnapshotAsync(Guid? branchIdOverride, ClaimsPrincipal currentUser)
    {
        var branchId = (IsOwner(currentUser) ? branchIdOverride : null) ?? await ResolveBranchIdAsync(currentUser);
        if (branchId == null)
            throw new BusinessException("Không xác định được chi nhánh.");

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

        return new { snapshotDate = today, totalProducts = snapshots.Count };
    }
}
