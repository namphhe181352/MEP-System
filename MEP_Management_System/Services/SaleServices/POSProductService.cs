using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.SaleServices;

public class POSProductService : IPOSProductService
{
    private readonly MEPDatabaseContext _context;

    public POSProductService(MEPDatabaseContext context)
    {
        _context = context;
    }

    private static ProductPriceDto MapBranchProductToDto(BranchProduct b) =>
        new()
        {
            BranchProductId = b.BranchProductId,
            ProductId = b.ProductId,
            ProductName = b.Product?.ProductName,
            ProductCode = b.Product?.ProductCode,
            Barcode = b.Product?.Barcode,
            RetailPrice = b.SellPrice,
            FloorPrice = b.CostPrice,
            Unit = b.Product?.BaseUnit,
            AvailableStock = b.AvailableStock,
            WarrantyQuantity = b.WarrantyQuantity,
            ImageUrl = b.Product?.MediaResources?.Where(m => m.IsPrimary == 1).Select(m => m.MediaUrl).FirstOrDefault(),
            ProductStatus = b.Product?.Status == 1 ? "active" : "inactive",
            DirectSale = b.DirectSale == 1,
            ConversionUnits = b.Product?.ProductConversionUnits
                ?.Where(u => u.DirectSale == 1)
                .Select(u => new ConversionUnitDto
                {
                    ConversionId = u.ConversionId,
                    UnitName = u.UnitName,
                    ConvertValue = u.ConvertValue,
                    Price = u.Price,
                    DirectSale = u.DirectSale == 1
                }).ToList() ?? new List<ConversionUnitDto>()
        };

    public async Task<List<ProductPriceDto>> GetProductsAsync(Guid branchId, string? search)
    {
        IQueryable<BranchProduct> query = _context.BranchProducts
            .AsNoTracking()
            .Include(b => b.Product)
            .ThenInclude(p => p!.ProductConversionUnits)
            .Include(b => b.Product)
            .ThenInclude(p => p!.MediaResources)
            .Where(b => b.BranchId == branchId && b.Product != null && (b.Product.BranchId == null || b.Product.BranchId == branchId))
            .Where(b => b.IsActive != -1 && (b.Product!.Status == null || b.Product!.Status != -1))
            .Where(b => b.IsActive == 1 || b.Product!.ProductConversionUnits.Any(u => u.DirectSale == 1));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(b => (b.Product != null && b.Product.ProductName != null && b.Product.ProductName.ToLower().Contains(searchLower))
                || (b.Product != null && b.Product.ProductCode != null && b.Product.ProductCode.ToLower().Contains(searchLower))
                || (b.Product != null && b.Product.Barcode != null && b.Product.Barcode.ToLower().Contains(searchLower)));
        }

        var products = await query.Take(100).ToListAsync();
        return products.Select(MapBranchProductToDto).ToList();
    }
}
