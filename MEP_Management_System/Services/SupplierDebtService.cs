using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services
{
    public class SupplierDebtService : ISupplierDebtService
    {
        private readonly MEPDatabaseContext _context;

        public SupplierDebtService(MEPDatabaseContext context)
        {
            _context = context;
        }

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

            // Batch-lookup CostPrice cho các PO items có UnitPrice = 0 (đơn cũ chưa lưu giá)
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

<<<<<<< HEAD
            var stockTicketDetails = supplier.StockTickets
                .OrderByDescending(t => t.CompletedAt ?? t.CreatedAt)
                .Select(t => new SupplierDebtPoDetailDto
                {
                    PurchaseOrderId = t.StockTicketId,
                    OrderCode = t.TicketCode ?? t.StockTicketId.ToString()[..8].ToUpper(),
                    TotalAmount = t.TotalAmount,
                    Status = t.Status ?? "COMPLETED",
                    ExpectedDeliveryDate = t.CompletedAt?.ToString("yyyy-MM-dd"),
                    CreatedAt = (t.CompletedAt ?? t.CreatedAt)?.ToString("yyyy-MM-dd"),
                    Items = t.StockTicketItems.Select(i => new SupplierDebtPoItemDto
=======
            // Thêm StockTickets PURCHASE COMPLETED (phiếu nhập kho đã hoàn thành)
            var stockTickets = await _context.StockTickets
                .Where(t => t.SupplierId == supplierId
                    && t.TicketType == "PURCHASE"
                    && t.Status == "COMPLETED")
                .Include(t => t.StockTicketItems)
                    .ThenInclude(i => i.BranchProduct)
                        .ThenInclude(bp => bp.Product)
                .OrderByDescending(t => t.CompletedAt)
                .ToListAsync();

            // Batch-lookup CostPrice cho ST items có UnitPrice = 0
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
>>>>>>> bca0f3e71c7921a59c1389279c9a58109a183eeb
                    {
                        ProductId = i.BranchProduct?.ProductId ?? Guid.Empty,
                        ProductCode = i.BranchProduct?.Product?.ProductCode ?? "",
                        ProductName = i.BranchProduct?.Product?.ProductName ?? "",
                        Quantity = i.Discrepancy,
<<<<<<< HEAD
                        UnitPrice = i.UnitPrice ?? i.BranchProduct?.CostPrice ?? 0,
                        LineTotal = i.Discrepancy * (i.UnitPrice ?? i.BranchProduct?.CostPrice ?? 0)
                    }).ToList()
                }).ToList();
=======
                        UnitPrice = unitPrice,
                        LineTotal = i.Discrepancy * unitPrice
                    };
                }).ToList()
            }).ToList();
>>>>>>> bca0f3e71c7921a59c1389279c9a58109a183eeb

            return new SupplierDebtDetailResponse
            {
                SupplierId = supplier.SupplierId,
                SupplierName = supplier.SupplierName,
                ContactPhone = supplier.ContactPhone,
                ContactEmail = supplier.ContactEmail,
                PurchaseOrders = poDetails,
<<<<<<< HEAD
                StockTickets = stockTicketDetails
=======
                StockTickets = stDetails
>>>>>>> bca0f3e71c7921a59c1389279c9a58109a183eeb
            };
        }

        /// <summary>
        /// Tính công nợ hiện tại dùng công thức thống nhất:
        ///   TotalPurchased = POs(APPROVED|COMPLETED) + StockTickets(PURCHASE, COMPLETED, CompletedAt &lt;= asOfDate)
        ///   TotalPaid = SupplierPayments(IsDeleted=0, PaymentDate &lt;= asOfDate)
        ///   CurrentDebt = TotalPurchased - TotalPaid
        ///   Status = "Còn nợ" nếu CurrentDebt > 0, ngược lại "Hết nợ"
        /// </summary>
        public async Task<SupplierDebtSummary> CalculateCurrentDebtAsync(Guid supplierId, DateTime? asOfDate = null)
        {
            var asOf = asOfDate ?? DateTime.UtcNow;

            var poPurchased = await _context.PurchaseOrders
                .Where(po => po.SupplierId == supplierId
                          && (po.Status == "APPROVED" || po.Status == "COMPLETED")
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
            // Clamp về 0 — công nợ không thể âm. Khi trả thừa, hiển thị "Hết nợ" + OverpaidAmount
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

            // 1 query cho POs
            var poPurchases = await _context.PurchaseOrders
                .Where(po => ids.Contains(po.SupplierId)
                          && (po.Status == "APPROVED" || po.Status == "COMPLETED")
                          && po.CreatedAt <= asOf)
                .GroupBy(po => po.SupplierId)
                .Select(g => new { SupplierId = g.Key, Total = g.Sum(po => po.TotalAmount ?? 0) })
                .ToListAsync();

            // 1 query cho StockTickets
            var stockTicketPurchases = await _context.StockTickets
                .Where(t => t.SupplierId.HasValue && ids.Contains(t.SupplierId.Value)
                         && t.TicketType == "PURCHASE"
                         && t.Status == "COMPLETED"
                         && t.CompletedAt.HasValue
                         && t.CompletedAt.Value <= asOf)
                .GroupBy(t => t.SupplierId)
                .Select(g => new { SupplierId = g.Key, Total = g.Sum(t => t.TotalAmount) })
                .ToListAsync();

            // 1 query cho SupplierPayments
            var payments = await _context.SupplierPayments
                .Where(p => ids.Contains(p.SupplierId)
                         && p.IsDeleted == 0
                         && p.PaymentDate <= asOf)
                .GroupBy(p => p.SupplierId)
                .Select(g => new { SupplierId = g.Key, Total = g.Sum(p => p.Amount) })
                .ToListAsync();

            // Convert sang Dictionary để O(1) lookup, tránh N+1 trong loop
            // Note: EF Core may project SupplierId of mixed nullability (StockTicket.SupplierId is Guid?,
            // SupplierPayment.SupplierId is Guid); cast to Guid to satisfy ToDictionary's notnull TKey constraint.
#pragma warning disable CS8714
            var poMap = poPurchases.ToDictionary(x => (Guid)x.SupplierId!, x => x.Total);
            var stMap = stockTicketPurchases.ToDictionary(x => (Guid)x.SupplierId!, x => x.Total);
            var paidMap = payments.ToDictionary(x => x.SupplierId, x => x.Total);
#pragma warning restore CS8714

            foreach (var id in ids)
            {
                var poSum = poMap.GetValueOrDefault(id, 0m);
                var stSum = stMap.GetValueOrDefault(id, 0m);
                var paidSum = paidMap.GetValueOrDefault(id, 0m);

                var totalPurchased = poSum + stSum;
                // Clamp về 0 — khi trả thừa, rawDebt < 0 → trả về 0 + OverpaidAmount
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
}
