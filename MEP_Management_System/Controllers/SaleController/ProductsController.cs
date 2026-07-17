using System;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
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
    private readonly IProductService _productService;

    public ProductsController(Models.MEPDatabaseContext context, IProductService productService)
    {
        _context = context;
        _productService = productService;
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
            ProductStatus = b.Product?.Status == 1 ? "active" : "inactive",
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
            .Where(b => b.BranchId == branchId && b.IsActive == 1 && b.Product != null && b.Product.Status == 1);

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
        var dto = await _productService.GetProductPriceAsync(productId, User);
        if (dto == null) return NotFound(ApiErrorResponse.Create("MSG-59", "Product not found"));
        return Ok(dto);
    }

    // GET /api/pos/products/{productId}/stock
    [HttpGet("{productId:guid}/stock")]
    public async Task<ActionResult<ProductStockDto>> GetStock(
        Guid productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var dto = await _productService.GetProductStockAsync(productId, User);
        if (dto == null) return NotFound(ApiErrorResponse.Create("MSG-59", "Product not found"));

        // apply pagination at controller level to preserve original behavior
        var totalBranches = dto.ByBranch.Count;
        var paged = dto.ByBranch.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        dto.ByBranch = paged;
        return Ok(dto);
    }
}
