using System;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Nhập kho legacy (Stock Import)")]
    public class StockImportController : ControllerBase
    {
        private readonly IStockImportService _stockImportService;
        private readonly ICurrentBranchAccessor _currentBranch;

        public StockImportController(
            IStockImportService stockImportService,
            ICurrentBranchAccessor currentBranch)
        {
            _stockImportService = stockImportService;
            _currentBranch = currentBranch;
        }

        // GET /api/stockimport — filter theo status / ticketType / fromDate / toDate / pagination
        [HttpGet]
        public async Task<IActionResult> GetStockImports(
            [FromQuery] string? status,
            [FromQuery] string? ticketType,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] Guid? branchId = null)
        {
            var result = await _stockImportService.GetStockImportsAsync(
                User, branchId, status, ticketType, fromDate, toDate, pageNumber, pageSize);
            return Ok(result);
        }

        // GET /api/stockimport/{id}
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetStockImport(Guid id)
        {
            var result = await _stockImportService.GetStockImportByIdAsync(User, id, _currentBranch.GetBranchId());
            return Ok(result);
        }

        // POST /api/stockimport — tạo phiếu nhập PENDING (chưa ghi ledger; confirm mới cộng kho & ghi ledger)
        [HttpPost]
        [HasPermission(PermissionConstants.STOCK_INWARD_CREATE)]
        public async Task<IActionResult> CreateStockImport(
            [FromBody] StockImportCreateDto dto,
            [FromQuery] Guid? branchId = null)
        {
            var result = await _stockImportService.CreateStockImportAsync(User, dto, branchId);
            return Ok(result);
        }
    }
}