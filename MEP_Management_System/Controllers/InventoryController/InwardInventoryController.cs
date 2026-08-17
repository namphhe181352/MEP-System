using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Services;
using MEP_Management_System.Services.InventoryServices;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Xuất nhập kho (Inventory)")]
    public class InwardInventoryController : ControllerBase
    {
        private readonly IInwardInventoryService _inwardInventoryService;
        private readonly ICurrentBranchAccessor _currentBranch;

        public InwardInventoryController(
            IInwardInventoryService inwardInventoryService,
            ICurrentBranchAccessor currentBranch)
        {
            _inwardInventoryService = inwardInventoryService;
            _currentBranch = currentBranch;
        }

        // ============================================================
        // GET /api/inwardinventory
        // Mặc định: CHỈ hiển thị PENDING + COMPLETED
        // Query ?status=CANCELLED hoặc ?status=ALL để xem thêm CANCELLED
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetInwardInventories(
            [FromQuery] string? status,
            [FromQuery] string? ticketType,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] Guid? branchId = null)
        {
            var result = await _inwardInventoryService.GetInwardInventoriesAsync(
                User, branchId, status, ticketType, fromDate, toDate, pageNumber, pageSize);
            return Ok(result);
        }

        // ============================================================
        // GET /api/inwardinventory/{id}
        // ============================================================
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetInwardInventoryById(Guid id)
        {
            var result = await _inwardInventoryService.GetInwardInventoryByIdAsync(User, id, _currentBranch.GetBranchId());
            return Ok(result);
        }

        // ============================================================
        // POST /api/inwardinventory
        // Tạo phiếu mới với Status = PENDING (chưa cập nhật kho)
        // ============================================================
        [HttpPost]
        [HasPermission(PermissionConstants.STOCK_INWARD_CREATE)]
        public async Task<IActionResult> CreateInwardInventory(
            [FromBody] InwardInventoryCreateDto dto,
            [FromQuery] Guid? branchId = null)
        {
            var result = await _inwardInventoryService.CreateInwardInventoryAsync(User, dto, branchId);
            return Ok(result);
        }

        // ============================================================
        // PUT /api/inwardinventory/{id}
        // Chỉ cho phép sửa khi Status = PENDING
        // ============================================================
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateInwardInventory(Guid id, [FromBody] InwardInventoryUpdateDto dto)
        {
            var result = await _inwardInventoryService.UpdateInwardInventoryAsync(User, id, dto, _currentBranch.GetBranchId());
            return Ok(result);
        }

        // ============================================================
        // POST /api/inwardinventory/{id}/confirm
        // Xác nhận phiếu nhập kho - Cập nhật tồn kho và đổi sang COMPLETED
        // ============================================================
        [HttpPost("{id:guid}/confirm")]
        [HasPermission(PermissionConstants.STOCK_INWARD_UPDATE)]
        public async Task<IActionResult> ConfirmInwardInventory(Guid id)
        {
            var result = await _inwardInventoryService.ConfirmInwardInventoryAsync(User, id, _currentBranch.GetBranchId());
            return Ok(result);
        }

        // ============================================================
        // POST /api/inwardinventory/{id}/cancel
        // Hủy phiếu nhập kho - Xử lý rollback an toàn
        // Quyen huy:
        // - PENDING: nguoi tao phieu hoac co STOCK_INWARD_DELETE hoac Owner
        // - COMPLETED: chi co STOCK_INWARD_DELETE hoac Owner
        // ============================================================
        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> CancelInwardInventory(Guid id, [FromBody] CancelTicketDto dto)
        {
            var result = await _inwardInventoryService.CancelInwardInventoryAsync(User, id, dto, _currentBranch.GetBranchId());
            return Ok(result);
        }

        // ============================================================
        // DELETE /api/inwardinventory/{id}
        // ⚠️ DEPRECATED: Chỉ giữ lại cho tương thích FE cũ.
        // FE mới nên dùng: POST /api/inwardinventory/{id}/cancel
        // ============================================================
        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionConstants.STOCK_INWARD_DELETE)]
        public async Task<IActionResult> DeleteInwardInventory(Guid id)
        {
            // Gọi nội bộ vào Cancel với CancelReason mặc định
            var cancelDto = new CancelTicketDto
            {
                CancelReason = "Hủy qua API DELETE (legacy)"
            };
            var result = await _inwardInventoryService.CancelInwardInventoryAsync(User, id, cancelDto, _currentBranch.GetBranchId());
            return Ok(result);
        }

        // ============================================================
        // GET /api/inwardinventory/{id}/print
        // Trả về HTML phiếu nhập kho
        // ============================================================
        [HttpGet("{id:guid}/print")]
        [HasPermission(PermissionConstants.STOCK_INWARD_CREATE)]
        public async Task<IActionResult> Print(Guid id)
        {
            var html = await _inwardInventoryService.GetPrintHtmlAsync(User, id, _currentBranch.GetBranchId());
            return Content(html, "text/html");
        }

        #region Return-to-Supplier Support

        /// <summary>Lấy danh sách phiếu nhập đã hoàn thành của 1 NCC (để chọn khi xuất trả).</summary>
        [HttpGet("by-supplier/{supplierId:guid}")]
        public async Task<IActionResult> GetBySupplier(Guid supplierId, [FromQuery] Guid? branchId)
        {
            var result = await _inwardInventoryService.GetBySupplierAsync(User, supplierId, branchId);
            return Ok(result);
        }

        /// <summary>Lấy danh sách sản phẩm còn có thể trả của 1 phiếu nhập.</summary>
        [HttpGet("{id:guid}/returnable-items")]
        public async Task<IActionResult> GetReturnableItems(Guid id)
        {
            var result = await _inwardInventoryService.GetReturnableItemsAsync(User, id);
            return Ok(result);
        }

        #endregion
    }
}