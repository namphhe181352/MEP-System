using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Services;
using MEP_Management_System.Services.SaleServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/products")]
[Authorize]
[Tags("Quản lý bán hàng (Sale)")]
public class ProductsController : ControllerBase
{
    private readonly IPOSProductService _posProductService;
    private readonly ICurrentBranchAccessor _currentBranch;

    public ProductsController(IPOSProductService posProductService, ICurrentBranchAccessor currentBranch)
    {
        _posProductService = posProductService;
        _currentBranch = currentBranch;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found in token"));

    [HttpGet]
    public async Task<ActionResult<List<ProductPriceDto>>> GetProducts([FromQuery] string? search)
    {
        var branchId = GetBranchId();
        var products = await _posProductService.GetProductsAsync(branchId, search);
        return Ok(products);
    }

    [HttpGet("{productId:guid}/price")]
    public async Task<ActionResult<ProductPriceDto>> GetPrice(Guid productId)
    {
        var dto = await ProductService.GetProductPriceAsync(productId, User, _currentBranch);
        if (dto == null) return NotFound(ApiErrorResponse.Create("MSG-59", "Product not found"));
        return Ok(dto);
    }

    [HttpGet("{productId:guid}/stock")]
    public async Task<ActionResult<ProductStockDto>> GetStock(
        Guid productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var dto = await ProductService.GetProductStockAsync(productId, User);
        if (dto == null) return NotFound(ApiErrorResponse.Create("MSG-59", "Product not found"));

        var totalBranches = dto.ByBranch.Count;
        var paged = dto.ByBranch.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        dto.ByBranch = paged;
        return Ok(dto);
    }
}
