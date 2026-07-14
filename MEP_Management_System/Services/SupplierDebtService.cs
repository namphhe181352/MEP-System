using System;
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
                .FirstOrDefaultAsync(s => s.SupplierId == supplierId && s.Status == 1);

            if (supplier == null) return null;

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
                    Items = po.PurchaseOrderItems.Select(i => new SupplierDebtPoItemDto
                    {
                        ProductId = i.ProductId,
                        ProductCode = i.Product != null ? i.Product.ProductCode : "",
                        ProductName = i.Product != null ? i.Product.ProductName : "",
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        LineTotal = i.Quantity * i.UnitPrice
                    }).ToList()
                }).ToList();

            return new SupplierDebtDetailResponse
            {
                SupplierId = supplier.SupplierId,
                SupplierName = supplier.SupplierName,
                ContactPhone = supplier.ContactPhone,
                ContactEmail = supplier.ContactEmail,
                PurchaseOrders = poDetails
            };
        }
    }
}
