using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

public class SupplierDebtService : ISupplierDebtService
{
    private readonly MEPDatabaseContext _context;
    private readonly ICurrentBranchAccessor _currentBranch;

    public SupplierDebtService(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch)
    {
        _context = context;
        _currentBranch = currentBranch;
    }

    /// <summary>
    /// Gets paginated supplier debts with summary statistics.
    /// </summary>
    public async Task<SupplierDebtPagedResponse> GetSupplierDebtsAsync(SupplierDebtParameters @params, Guid? branchId, ClaimsPrincipal user)
    {
        // Permission check
        var isOwner = user?.IsInRole("Owner") ?? false;
        var hasPermission = user?.HasClaim("permission", PermissionConstants.SUPPLIER_PAYMENT_VIEW) == true
            || user?.HasClaim("permission", PermissionConstants.SUPPLIER_PAYMENT_CREATE) == true
            || user?.HasClaim("permission", PermissionConstants.SUPPLIER_PAYMENT_DELETE) == true;
        
        if (!isOwner && !hasPermission)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền xem công nợ.");
        }

        var supplierQuery = _context.Suppliers
            .Include(s => s.PurchaseOrders)
            .Include(s => s.StockTickets.Where(t => t.TicketType == "PURCHASE" && t.Status == "COMPLETED"))
            .Include(s => s.SupplierPayments.Where(p => p.IsDeleted == 0))
            .Where(s => s.Status == 1);

        var resolvedBranchId = branchId ?? _currentBranch.GetBranchId();
        if (resolvedBranchId.HasValue && resolvedBranchId != Guid.Empty)
        {
            supplierQuery = supplierQuery.Where(s => s.BranchId == resolvedBranchId.Value);
        }

        // Search theo 8 ký tự đầu của GUID
        if (!string.IsNullOrWhiteSpace(@params.SearchTerm))
        {
            var term = @params.SearchTerm.Trim().ToLower();
            supplierQuery = supplierQuery.Where(s =>
                s.SupplierName.ToLower().Contains(term) ||
                s.SupplierId.ToString().Substring(0, 8).ToLower().Contains(term));
        }

        var suppliers = await supplierQuery.ToListAsync();
        var today = DateTime.UtcNow.Date;
        var currentMonth = new DateTime(today.Year, today.Month, 1);

        var debtList = new List<SupplierDebtDto>();

        foreach (var s in suppliers)
        {
            // Nợ đầu kỳ
            decimal openingPurchased = s.PurchaseOrders
                .Where(po => po.CreatedAt < currentMonth && (po.Status == "APPROVED" || po.Status == "PENDING"))
                .Sum(po => po.TotalAmount ?? 0);

            decimal openingStockTicketDebt = s.StockTickets
                .Where(t => t.CompletedAt.HasValue && t.CompletedAt.Value < currentMonth)
                .Sum(t => t.TotalAmount);

            decimal openingPaid = s.SupplierPayments
                .Where(p => p.PaymentDate < currentMonth)
                .Sum(p => p.Amount);

            decimal openingDebt = openingPurchased + openingStockTicketDebt - openingPaid;

            // Phát sinh mua trong kỳ
            decimal purchasedInPeriod = s.PurchaseOrders
                .Where(po => po.CreatedAt >= currentMonth
                          && (po.Status == "APPROVED" || po.Status == "PENDING"))
                .Sum(po => po.TotalAmount ?? 0);

            decimal stockTicketPurchasedInPeriod = s.StockTickets
                .Where(t => t.CompletedAt.HasValue && t.CompletedAt.Value >= currentMonth)
                .Sum(t => t.TotalAmount);

            purchasedInPeriod += stockTicketPurchasedInPeriod;

            // Thực tế đã trả trong kỳ
            decimal paidInPeriod = s.SupplierPayments
                .Where(p => p.PaymentDate >= currentMonth)
                .Sum(p => p.Amount);

            // Dư nợ cuối kỳ
            decimal rawClosing = openingDebt + purchasedInPeriod - paidInPeriod;
            decimal closingDebt = rawClosing < 0 ? 0 : rawClosing;
            decimal overpaidAmount = rawClosing < 0 ? -rawClosing : 0;

            // Hạn thanh toán
            var latestApprovedPo = s.PurchaseOrders
                .Where(po => po.Status == "APPROVED")
                .OrderByDescending(po => po.ExpectedDeliveryDate)
                .FirstOrDefault();

            DateTime? dueDate = latestApprovedPo?.ExpectedDeliveryDate;
            int overdueDays = 0;
            if (dueDate.HasValue && dueDate.Value.Date < today && closingDebt > 0)
            {
                overdueDays = (today - dueDate.Value.Date).Days;
            }

            // Trạng thái công nợ
            string calcStatus;
            if (overpaidAmount > 0) calcStatus = "Trả thừa";
            else if (closingDebt <= 0) calcStatus = "Hết nợ";
            else if (overdueDays > 0) calcStatus = "Quá hạn";
            else calcStatus = "Còn nợ";

            debtList.Add(new SupplierDebtDto
            {
                Id = s.SupplierId.ToString(),
                Code = s.SupplierId.ToString()[..8].ToUpper(),
                Name = s.SupplierName,
                ContactPhone = s.ContactPhone,
                ContactEmail = s.ContactEmail,
                Address = s.Address,
                GroupName = s.GroupName,
                OpeningDebt = openingDebt,
                PurchasedInPeriod = purchasedInPeriod,
                PaidInPeriod = paidInPeriod,
                ClosingDebt = closingDebt,
                OverpaidAmount = overpaidAmount,
                DueDate = dueDate?.ToString("yyyy-MM-dd") ?? "",
                OverdueDays = overdueDays,
                Status = calcStatus
            });
        }

        // Lọc theo trạng thái
        if (!string.IsNullOrWhiteSpace(@params.Status) && @params.Status != "all")
        {
            debtList = debtList.Where(d => d.Status == @params.Status).ToList();
        }

        // Summary
        var summary = new SupplierDebtSummaryDto
        {
            TotalClosingDebt = debtList.Sum(d => d.ClosingDebt),
            TotalOverdueDebt = debtList.Where(d => d.Status == "Quá hạn").Sum(d => d.ClosingDebt),
            TotalPaidInPeriod = debtList.Sum(d => d.PaidInPeriod),
            TotalPurchasedInPeriod = debtList.Sum(d => d.PurchasedInPeriod),
            TotalSuppliers = debtList.Count,
            OverdueCount = debtList.Count(d => d.Status == "Quá hạn")
        };

        // Phân trang
        var totalCount = debtList.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)@params.PageSize);

        var pagedItems = debtList
            .Skip((@params.PageNumber - 1) * @params.PageSize)
            .Take(@params.PageSize)
            .ToList();

        return new SupplierDebtPagedResponse
        {
            Items = pagedItems,
            TotalCount = totalCount,
            PageNumber = @params.PageNumber,
            PageSize = @params.PageSize,
            TotalPages = totalPages,
            HasNextPage = @params.PageNumber < totalPages,
            HasPreviousPage = @params.PageNumber > 1,
            Summary = summary
        };
    }

    /// <summary>
    /// Gets supplier debt detail with PO and stock ticket history.
    /// </summary>
    public async Task<SupplierDebtDetailResponse?> GetSupplierDebtDetailAsync(Guid supplierId, ClaimsPrincipal currentUser)
    {
        var supplier = await _context.Suppliers
            .Include(s => s.PurchaseOrders)
                .ThenInclude(po => po.PurchaseOrderItems)
                    .ThenInclude(poi => poi.Product)
            .Include(s => s.StockTickets.Where(t => t.TicketType == "PURCHASE" && t.Status == "COMPLETED"))
                .ThenInclude(t => t.StockTicketItems)
                    .ThenInclude(i => i.BranchProduct)
                        .ThenInclude(bp => bp.Product)
            .FirstOrDefaultAsync(s => s.SupplierId == supplierId && s.Status == 1);

        if (supplier == null) return null;

        // Batch-lookup CostPrice cho các PO items có UnitPrice = 0
        var poProductIds = supplier.PurchaseOrders
            .SelectMany(po => po.PurchaseOrderItems)
            .Where(i => i.UnitPrice <= 0)
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();

        Dictionary<Guid, decimal>? productCostPrices = null;
        if (poProductIds.Count > 0)
        {
            productCostPrices = await _context.BranchProducts
                .Where(bp => poProductIds.Contains(bp.ProductId) && bp.BranchId == supplier.BranchId)
                .GroupBy(bp => bp.ProductId)
                .Select(g => new { ProductId = g.Key, CostPrice = g.Max(bp => bp.CostPrice) })
                .ToDictionaryAsync(x => x.ProductId, x => x.CostPrice);
        }

        var poDetails = supplier.PurchaseOrders
            .OrderByDescending(po => po.CreatedAt)
            .Select(po => new SupplierDebtPoDetailDto
            {
                PurchaseOrderId = po.PurchaseOrderId,
                OrderCode = po.OrderCode ?? po.PurchaseOrderId.ToString()[..8].ToUpper(),
                TotalAmount = po.TotalAmount ?? 0,
                Status = po.Status,
                ExpectedDeliveryDate = po.ExpectedDeliveryDate?.ToString("yyyy-MM-dd"),
                CreatedAt = po.CreatedAt?.ToString("yyyy-MM-dd"),
                Items = po.PurchaseOrderItems.Select(i =>
                {
                    var unitPrice = i.UnitPrice > 0 ? i.UnitPrice
                        : (productCostPrices != null && productCostPrices.TryGetValue(i.ProductId, out var cp) ? cp : 0);
                    return new SupplierDebtPoItemDto
                    {
                        ProductId = i.ProductId,
                        ProductCode = i.Product != null ? i.Product.ProductCode : "",
                        ProductName = i.Product != null ? i.Product.ProductName : "",
                        Quantity = i.Quantity,
                        UnitPrice = unitPrice,
                        LineTotal = i.Quantity * unitPrice
                    };
                }).ToList()
            }).ToList();

        var stockTickets = await _context.StockTickets
            .Where(t => t.SupplierId == supplierId
                && t.TicketType == "PURCHASE"
                && t.Status == "COMPLETED")
            .Include(t => t.StockTicketItems)
                .ThenInclude(i => i.BranchProduct)
                    .ThenInclude(bp => bp.Product)
            .OrderByDescending(t => t.CompletedAt)
            .ToListAsync();

        var stProductIds = stockTickets
            .SelectMany(st => st.StockTicketItems)
            .Where(i => (i.UnitPrice ?? 0) <= 0)
            .Select(i => i.BranchProduct?.ProductId ?? Guid.Empty)
            .Where(pid => pid != Guid.Empty && !(productCostPrices?.ContainsKey(pid) ?? false))
            .Distinct()
            .ToList();

        if (productCostPrices == null) productCostPrices = new Dictionary<Guid, decimal>();
        if (stProductIds.Count > 0)
        {
            var stCostPrices = await _context.BranchProducts
                .Where(bp => stProductIds.Contains(bp.ProductId) && bp.BranchId == supplier.BranchId)
                .GroupBy(bp => bp.ProductId)
                .Select(g => new { ProductId = g.Key, CostPrice = g.Max(bp => bp.CostPrice) })
                .ToDictionaryAsync(x => x.ProductId, x => x.CostPrice);
            foreach (var kvp in stCostPrices)
                productCostPrices[kvp.Key] = kvp.Value;
        }

        var stDetails = stockTickets.Select(st => new SupplierDebtStDetailDto
        {
            StockTicketId = st.StockTicketId,
            TicketCode = st.TicketCode ?? st.StockTicketId.ToString()[..8].ToUpper(),
            TotalAmount = st.TotalAmount,
            Status = st.Status ?? "COMPLETED",
            CreatedAt = st.CreatedAt?.ToString("yyyy-MM-dd"),
            CompletedAt = st.CompletedAt?.ToString("yyyy-MM-dd"),
            Items = st.StockTicketItems.Select(i =>
            {
                var unitPrice = i.UnitPrice ?? 0;
                if (unitPrice <= 0 && productCostPrices != null)
                {
                    var pid = i.BranchProduct?.ProductId ?? Guid.Empty;
                    if (pid != Guid.Empty)
                        productCostPrices.TryGetValue(pid, out unitPrice);
                }
                return new SupplierDebtPoItemDto
                {
                    ProductId = i.BranchProduct?.ProductId ?? Guid.Empty,
                    ProductCode = i.BranchProduct?.Product?.ProductCode ?? "",
                    ProductName = i.BranchProduct?.Product?.ProductName ?? "",
                    Quantity = i.Discrepancy,
                    UnitPrice = unitPrice,
                    LineTotal = i.Discrepancy * unitPrice
                };
            }).ToList()
        }).ToList();

        return new SupplierDebtDetailResponse
        {
            SupplierId = supplier.SupplierId,
            SupplierName = supplier.SupplierName,
            ContactPhone = supplier.ContactPhone,
            ContactEmail = supplier.ContactEmail,
            PurchaseOrders = poDetails,
            StockTickets = stDetails
        };
    }

    /// <summary>
    /// Tính công nợ hiện tại dùng công thức thống nhất.
    /// </summary>
    public async Task<SupplierDebtSummary> CalculateCurrentDebtAsync(Guid supplierId, DateTime? asOfDate = null)
    {
        var asOf = asOfDate ?? DateTime.UtcNow;

        var poPurchased = await _context.PurchaseOrders
            .Where(po => po.SupplierId == supplierId
                      && (po.Status == "APPROVED" || po.Status == "PENDING")
                      && po.CreatedAt <= asOf)
            .SumAsync(po => (decimal?)(po.TotalAmount ?? 0)) ?? 0m;

        var stockTicketPurchased = await _context.StockTickets
            .Where(t => t.SupplierId == supplierId
                     && t.TicketType == "PURCHASE"
                     && t.Status == "COMPLETED"
                     && t.CompletedAt.HasValue
                     && t.CompletedAt.Value <= asOf)
            .SumAsync(t => (decimal?)(t.TotalAmount)) ?? 0m;

        var totalPaid = await _context.SupplierPayments
            .Where(p => p.SupplierId == supplierId
                     && p.IsDeleted == 0
                     && p.PaymentDate <= asOf)
            .SumAsync(p => (decimal?)(p.Amount)) ?? 0m;

        var totalPurchased = poPurchased + stockTicketPurchased;
        var raw = totalPurchased - totalPaid;
        var currentDebt = raw < 0 ? 0m : raw;
        var overpaid = raw < 0 ? -raw : 0m;

        return new SupplierDebtSummary
        {
            CurrentDebt = currentDebt,
            TotalPurchased = totalPurchased,
            TotalPaid = totalPaid,
            OverpaidAmount = overpaid,
            Status = raw < 0 ? "Trả thừa" : (currentDebt > 0 ? "Còn nợ" : "Hết nợ")
        };
    }

    public async Task<Dictionary<Guid, SupplierDebtSummary>> CalculateCurrentDebtBatchAsync(IEnumerable<Guid> supplierIds, DateTime? asOfDate = null)
    {
        var ids = supplierIds.ToList();
        var result = new Dictionary<Guid, SupplierDebtSummary>();
        if (ids.Count == 0) return result;

        var asOf = asOfDate ?? DateTime.UtcNow;

        var poPurchases = await _context.PurchaseOrders
            .Where(po => ids.Contains(po.SupplierId)
                      && (po.Status == "APPROVED" || po.Status == "PENDING")
                      && po.CreatedAt <= asOf)
            .GroupBy(po => po.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(po => po.TotalAmount ?? 0) })
            .ToListAsync();

        var stockTicketPurchases = await _context.StockTickets
            .Where(t => t.SupplierId.HasValue && ids.Contains(t.SupplierId.Value)
                     && t.TicketType == "PURCHASE"
                     && t.Status == "COMPLETED"
                     && t.CompletedAt.HasValue
                     && t.CompletedAt.Value <= asOf)
            .GroupBy(t => t.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(t => t.TotalAmount) })
            .ToListAsync();

        var payments = await _context.SupplierPayments
            .Where(p => ids.Contains(p.SupplierId)
                     && p.IsDeleted == 0
                     && p.PaymentDate <= asOf)
            .GroupBy(p => p.SupplierId)
            .Select(g => new { SupplierId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToListAsync();

        var poMap = poPurchases.ToDictionary(x => (Guid)x.SupplierId!, x => x.Total);
        var stMap = stockTicketPurchases.ToDictionary(x => (Guid)x.SupplierId!, x => x.Total);
        var paidMap = payments.ToDictionary(x => x.SupplierId, x => x.Total);

        foreach (var id in ids)
        {
            var poSum = poMap.GetValueOrDefault(id, 0m);
            var stSum = stMap.GetValueOrDefault(id, 0m);
            var paidSum = paidMap.GetValueOrDefault(id, 0m);

            var totalPurchased = poSum + stSum;
            var raw = totalPurchased - paidSum;
            var currentDebt = raw < 0 ? 0m : raw;
            var overpaid = raw < 0 ? -raw : 0m;

            result[id] = new SupplierDebtSummary
            {
                CurrentDebt = currentDebt,
                TotalPurchased = totalPurchased,
                TotalPaid = paidSum,
                OverpaidAmount = overpaid,
                Status = raw < 0 ? "Trả thừa" : (currentDebt > 0 ? "Còn nợ" : "Hết nợ")
            };
        }

        return result;
    }
}
