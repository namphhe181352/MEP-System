using System;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("goods-issue")]
    [Authorize]
    [Tags("Xuất Kho Nội Bộ (Goods Issue)")]
    public class GoodsIssueController : ControllerBase
    {
        private readonly IGoodsIssueService _goodsIssueService;
        private readonly ICurrentBranchAccessor _currentBranch;

        public GoodsIssueController(IGoodsIssueService goodsIssueService, ICurrentBranchAccessor currentBranch)
        {
            _goodsIssueService = goodsIssueService;
            _currentBranch = currentBranch;
        }

        private Guid? GetCurrentBranchId() => _currentBranch.GetBranchId();

        // ============================================================
        // GET /goods-issue  (danh sách phiếu xuất, phân trang)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetGoodsIssues(
            [FromQuery] string? keyword,
            [FromQuery] string? status,
            [FromQuery] string? issueType,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _goodsIssueService.GetGoodsIssuesAsync(
                User, GetCurrentBranchId(), keyword, status, issueType, fromDate, toDate, page, pageSize);
            return Ok(result);
        }

        // ============================================================
        // GET /goods-issue/{id}  (chi tiết phiếu xuất)
        // ============================================================
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetGoodsIssue(Guid id)
        {
            var result = await _goodsIssueService.GetGoodsIssueByIdAsync(User, id, GetCurrentBranchId());
            return Ok(result);
        }

        // ============================================================
        // POST /goods-issue  (tạo phiếu xuất)
        // ============================================================
        [HttpPost]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_CREATE)]
        public async Task<IActionResult> CreateGoodsIssue([FromBody] GoodsIssueCreateDto dto)
        {
            var result = await _goodsIssueService.CreateGoodsIssueAsync(User, dto, GetCurrentBranchId());
            return Ok(result);
        }

        // ============================================================
        // PUT /goods-issue/{id}  (cập nhật phiếu xuất)
        // ============================================================
        [HttpPut("{id:guid}")]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_UPDATE)]
        public async Task<IActionResult> UpdateGoodsIssue(Guid id, [FromBody] GoodsIssueCreateDto dto)
        {
            var result = await _goodsIssueService.UpdateGoodsIssueAsync(User, id, dto, GetCurrentBranchId());
            return Ok(result);
        }

        // ============================================================
        // DELETE /goods-issue/{id}  (xóa phiếu xuất)
        // ============================================================
        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_DELETE)]
        public async Task<IActionResult> DeleteGoodsIssue(Guid id)
        {
            var result = await _goodsIssueService.DeleteGoodsIssueAsync(User, id, GetCurrentBranchId());
            return Ok(result);
        }

        // ============================================================
        // GET /goods-issue/products/search  (tra cứu sản phẩm cho xuất kho)
        // ============================================================
        [HttpGet("products/search")]
        public async Task<IActionResult> SearchProducts([FromQuery] string? keyword)
        {
            var result = await _goodsIssueService.SearchProductsAsync(User, GetCurrentBranchId(), keyword);
            return Ok(result);
        }

        // ============================================================
        // GET /goods-issue/customers  (danh sách khách hàng)
        // ============================================================
        [HttpGet("customers")]
        public async Task<IActionResult> GetCustomers([FromQuery] string? search)
        {
            var result = await _goodsIssueService.GetCustomersAsync(User, GetCurrentBranchId(), search);
            return Ok(result);
        }

        // ============================================================
        // POST /goods-issue/customers  (tạo nhanh khách hàng)
        // ============================================================
        [HttpPost("customers")]
        [HasPermission(PermissionConstants.CUSTOMER_CREATE)]
        public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request)
        {
            var result = await _goodsIssueService.CreateCustomerAsync(User, GetCurrentBranchId(), request);
            return Ok(result);
        }

        // ============================================================
        // GET /goods-issue/warehouses  (danh sách kho/branch)
        // ============================================================
        [HttpGet("warehouses")]
        public async Task<IActionResult> GetWarehouses()
        {
            var result = await _goodsIssueService.GetWarehousesAsync(User, GetCurrentBranchId());
            return Ok(result);
        }

        // ============================================================
        // POST /goods-issue/{id}/attachments  (upload file đính kèm)
        // ============================================================
        [HttpPost("{id:guid}/attachments")]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_CREATE)]
        public async Task<IActionResult> UploadAttachment(Guid id, IFormFile file)
        {
            var result = await _goodsIssueService.UploadAttachmentAsync(User, GetCurrentBranchId(), id, file);
            return Ok(result);
        }
    }
}