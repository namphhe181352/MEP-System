using System;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/products")]
[HasPermission(PermissionConstants.PRODUCT_VIEW)]
[Tags("Quản lý bán hàng (Sale)")]
public class ProductsController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;

    public ProductsController(Models.MEPDatabaseContext context)
    {
        _context = context;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found in token"));

    // P3 Fix: Helper method to map projection to DTO - inline projection to avoid EF Core memory leak
    private static ProductPriceDto MapBranchProductToDto(BranchProduct b) =>
        new()
        {
            ProductId = b.ProductId,
            ProductName = b.Product?.ProductName,
            ProductCode = b.Product?.ProductCode,
            Barcode = b.Product?.Barcode,
            RetailPrice = b.SellPrice,
            FloorPrice = b.CostPrice,
            Unit = b.Product?.BaseUnit,
            AvailableStock = b.AvailableStock,
            ImageUrl = b.Product?.MediaResources?.Where(m => m.IsPrimary == 1).Select(m => m.MediaUrl).FirstOrDefault(),
            ConversionUnits = b.Product?.ProductConversionUnits
                ?.Where(u => u.DirectSale == 1 || u.DirectSale == null)
                .Select(u => new ConversionUnitDto
                {
                    ConversionId = u.ConversionId,
                    UnitName = u.UnitName,
                    ConvertValue = u.ConvertValue,
                    Price = u.Price,
                    DirectSale = u.DirectSale == 1
                }).ToList() ?? new List<ConversionUnitDto>()
        };

    // GET /api/pos/products
    [HttpGet]
    public async Task<ActionResult<List<ProductPriceDto>>> GetProducts([FromQuery] string? search)
    {
        var branchId = GetBranchId();

        // Query with Include for related data
        IQueryable<BranchProduct> query = _context.BranchProducts
            .AsNoTracking()
            .Include(b => b.Product)
            .ThenInclude(p => p!.ProductConversionUnits)
            .Include(b => b.Product)
            .ThenInclude(p => p!.MediaResources)
            .Where(b => b.BranchId == branchId && b.IsActive == 1);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(b => (b.Product != null && b.Product.ProductName != null && b.Product.ProductName.ToLower().Contains(searchLower))
                || (b.Product != null && b.Product.ProductCode != null && b.Product.ProductCode.ToLower().Contains(searchLower))
                || (b.Product != null && b.Product.Barcode != null && b.Product.Barcode.ToLower().Contains(searchLower)));
        }

        var products = await query.Take(100).ToListAsync();
        var result = products.Select(MapBranchProductToDto).ToList();

        return Ok(result);
    }

    // GET /api/pos/products/{productId}/price
    [HttpGet("{productId:guid}/price")]
    public async Task<ActionResult<ProductPriceDto>> GetPrice(Guid productId)
    {
        var branchId = GetBranchId();

        // Include Product and ConversionUnits for mapping
        var bp = await _context.BranchProducts
            .AsNoTracking()
            .Include(b => b.Product)
            .ThenInclude(p => p!.ProductConversionUnits)
            .Include(b => b.Product)
            .ThenInclude(p => p!.MediaResources)
            .Where(b => b.BranchId == branchId && b.ProductId == productId && b.IsActive == 1)
            .FirstOrDefaultAsync();

        if (bp == null) return NotFound(ApiErrorResponse.Create("MSG-59", "Product not found"));

        return Ok(MapBranchProductToDto(bp));
    }

    // GET /api/pos/products/{productId}/stock
    [HttpGet("{productId:guid}/stock")]
    public async Task<ActionResult<ProductStockDto>> GetStock(
        Guid productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var product = await _context.Products.FindAsync(productId);
        if (product == null) return NotFound(ApiErrorResponse.Create("MSG-59", "Product not found"));

        // Cross-branch stock lookup (BR-79) — with pagination
        var query = _context.BranchProducts
            .AsNoTracking()
            .Where(b => b.ProductId == productId && b.IsActive == 1);

        var totalBranches = await query.CountAsync();
        var allBp = await query
            .Select(b => new ProductStockDto.BranchStockDto
            {
                BranchId = b.BranchId,
                BranchName = b.Branch != null ? b.Branch.BranchName : null,
                AvailableStock = b.AvailableStock,
                InTransitStock = b.ReservedStock
            })
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        var dto = new ProductStockDto
        {
            ProductId = productId,
            ProductName = product.ProductName,
            ProductCode = product.ProductCode,
            InStockQty = allBp.Sum(b => b.AvailableStock),
            InTransitQty = allBp.Sum(b => b.InTransitStock),
            ByBranch = allBp
        };

        return Ok(dto);
    }
}
