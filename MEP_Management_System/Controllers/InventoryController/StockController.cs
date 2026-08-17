using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Ton Kho (Stock)")]
    [Authorize]
    public class StockController : ControllerBase
    {
        private readonly IStockService _stockService;
        private readonly ICurrentBranchAccessor _currentBranch;

        public StockController(IStockService stockService, ICurrentBranchAccessor currentBranch)
        {
            _stockService = stockService;
            _currentBranch = currentBranch;
        }

        private Guid? GetCurrentBranchId() => _currentBranch.GetBranchId();

        // GET /api/Stock?search=&branchId=&pageNumber=&pageSize=&lowStockOnly=true
        [HttpGet]
        [HasPermission(PermissionConstants.STOCK_VIEW)]
        public async Task<IActionResult> GetStocks(
            [FromQuery] string? search,
            [FromQuery] Guid? branchId,
            [FromQuery] bool? lowStockOnly,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _stockService.GetStocksAsync(
                User, branchId ?? GetCurrentBranchId(), search, lowStockOnly, pageNumber, pageSize);
            return Ok(result);
        }

        // GET /api/Stock/product/{productId}
        [HttpGet("product/{productId:guid}")]
        [HasPermission(PermissionConstants.STOCK_VIEW)]
        public async Task<IActionResult> GetStockByProduct(Guid productId)
        {
            var result = await _stockService.GetStockByProductAsync(User, productId, GetCurrentBranchId());
            return Ok(result);
        }

        // PUT /api/Stock/{productId}
        [HttpPut("{productId:guid}")]
        [HasPermission(PermissionConstants.STOCK_UPDATE)]
        public async Task<IActionResult> UpdateStock(Guid productId, [FromBody] UpdateStockDto dto)
        {
            var result = await _stockService.UpdateStockAsync(User, productId, dto, GetCurrentBranchId());
            return Ok(result);
        }
    }
}