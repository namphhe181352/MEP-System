using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.Models;
using MEP_Management_System.DTOs.SaleDTOs;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/products")]
[Authorize(Roles = "SalesStaff,Owner")]
[Tags("Quản lý bán hàng (Sale)")]
public class ProductsController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;

    public ProductsController(Models.MEPDatabaseContext context)
    {
        _context = context;
    }


    // GET /api/pos/products
    [HttpGet]
    public async Task<ActionResult<List<ProductPriceDto>>> GetProducts()
    {
        var branchId = Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found"));
        var products = await _context.BranchProducts
            .Include(b => b.Product)
            .Where(b => b.BranchId == branchId && b.IsActive == 1)
            .Take(100)
            .ToListAsync();

        return Ok(products.Select(b => new ProductPriceDto
        {
            ProductId = b.ProductId,
            ProductName = b.Product?.ProductName,
            ProductCode = b.Product?.ProductCode,
            Barcode = b.Product?.Barcode,
            RetailPrice = b.SellPrice,
            FloorPrice = b.CostPrice,
            Unit = b.Product?.BaseUnit
        }).ToList());
    }
    // GET /api/pos/products/{productId}/price
    [HttpGet("{productId:guid}/price")]
    public async Task<ActionResult<ProductPriceDto>> GetPrice(Guid productId)
    {
        var branchId = Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found"));
        var bp = await _context.BranchProducts
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.BranchId == branchId && b.ProductId == productId && b.IsActive == 1);

        if (bp == null) return NotFound(ApiErrorResponse.Create("MSG-59", "Product not found"));

        return Ok(new ProductPriceDto
        {
            ProductId = bp.ProductId,
            ProductName = bp.Product?.ProductName,
            ProductCode = bp.Product?.ProductCode,
            Barcode = bp.Product?.Barcode,
            RetailPrice = bp.SellPrice,
            FloorPrice = bp.CostPrice,
            Unit = bp.Product?.BaseUnit
        });
    }

    // GET /api/pos/products/{productId}/stock
    [HttpGet("{productId:guid}/stock")]
    public async Task<ActionResult<ProductStockDto>> GetStock(Guid productId)
    {
        var branchId = Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found"));
        var product = await _context.Products.FindAsync(productId);
        if (product == null) return NotFound(ApiErrorResponse.Create("MSG-59", "Product not found"));

        // Cross-branch stock lookup (BR-79)
        var allBp = await _context.BranchProducts
            .Include(b => b.Branch)
            .Where(b => b.ProductId == productId && b.IsActive == 1)
            .ToListAsync();

        var dto = new ProductStockDto
        {
            ProductId = productId,
            ProductName = product.ProductName,
            ProductCode = product.ProductCode,
            InStockQty = allBp.Sum(b => b.AvailableStock),
            InTransitQty = allBp.Sum(b => b.ReservedStock),
            ByBranch = allBp.Select(b => new ProductStockDto.BranchStockDto
            {
                BranchId = b.BranchId,
                BranchName = b.Branch?.BranchName,
                AvailableStock = b.AvailableStock,
                InTransitStock = b.ReservedStock
            }).ToList()
        };

        return Ok(dto);
    }
}
