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

    // GET /api/pos/products
    [HttpGet]
    public async Task<ActionResult<List<ProductPriceDto>>> GetProducts([FromQuery] string? search)
    {
        var branchId = GetBranchId();

        // Projection query - EF can traverse Product without explicit Include
        var query = _context.BranchProducts
            .AsNoTracking()
            .Where(b => b.BranchId == branchId && b.IsActive == 1)
            .Select(b => new ProductPriceDto
            {
                ProductId = b.ProductId,
                ProductName = b.Product != null ? b.Product.ProductName : null,
                ProductCode = b.Product != null ? b.Product.ProductCode : null,
                Barcode = b.Product != null ? b.Product.Barcode : null,
                RetailPrice = b.SellPrice,
                FloorPrice = b.CostPrice,
                Unit = b.Product != null ? b.Product.BaseUnit : null,
                AvailableStock = b.AvailableStock,
                ImageUrl = b.Product != null
                    ? b.Product.MediaResources.Where(m => m.IsPrimary == 1).Select(m => m.MediaUrl).FirstOrDefault()
                    : null
            });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p => p.ProductName != null && p.ProductName.ToLower().Contains(searchLower)
                || p.ProductCode != null && p.ProductCode.ToLower().Contains(searchLower)
                || p.Barcode != null && p.Barcode.ToLower().Contains(searchLower));
        }

        var products = await query.Take(100).ToListAsync();
        return Ok(products);
    }

    // GET /api/pos/products/{productId}/price
    [HttpGet("{productId:guid}/price")]
    public async Task<ActionResult<ProductPriceDto>> GetPrice(Guid productId)
    {
        var branchId = GetBranchId();

        // Projection query - no Include needed
        var bp = await _context.BranchProducts
            .AsNoTracking()
            .Where(b => b.BranchId == branchId && b.ProductId == productId && b.IsActive == 1)
            .Select(b => new ProductPriceDto
            {
                ProductId = b.ProductId,
                ProductName = b.Product != null ? b.Product.ProductName : null,
                ProductCode = b.Product != null ? b.Product.ProductCode : null,
                Barcode = b.Product != null ? b.Product.Barcode : null,
                RetailPrice = b.SellPrice,
                FloorPrice = b.CostPrice,
                Unit = b.Product != null ? b.Product.BaseUnit : null,
                AvailableStock = b.AvailableStock,
                ImageUrl = b.Product != null
                    ? b.Product.MediaResources.Where(m => m.IsPrimary == 1).Select(m => m.MediaUrl).FirstOrDefault()
                    : null
            })
            .FirstOrDefaultAsync();

        if (bp == null) return NotFound(ApiErrorResponse.Create("MSG-59", "Product not found"));
        return Ok(bp);
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
