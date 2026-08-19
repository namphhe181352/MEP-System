using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Services;
using MEP_Management_System.Services.InventoryServices;
using MEP_Management_System.Authorization;
using Microsoft.AspNetCore.Mvc;
using CancelTicketDto = MEP_Management_System.DTOs.InventoryDTOs.CancelTicketDto;

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Xuất nhập kho (Inventory)")]
    public class OutwardInventoryController : ControllerBase
    {
        private readonly IOutwardInventoryService _outwardInventoryService;
        private readonly ICurrentBranchAccessor _currentBranch;

        public OutwardInventoryController(
            IOutwardInventoryService outwardInventoryService,
            ICurrentBranchAccessor currentBranch)
        {
            _outwardInventoryService = outwardInventoryService;
            _currentBranch = currentBranch;
        }

        private Guid? GetCurrentBranchId() => _currentBranch.GetBranchId();

        // ============================================================
        // GET /api/outwardinventory
        // Mặc định: CHỈ hiển thị PENDING + COMPLETED
        // Query ?status=CANCELLED hoặc ?status=ALL để xem thêm CANCELLED
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetOutwardInventories(
            [FromQuery] string? searchKeyword,
            [FromQuery] string? status,
            [FromQuery] string? ticketType,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] Guid? branchId = null)
        {
            var result = await _outwardInventoryService.GetOutwardInventoriesAsync(
                User, GetCurrentBranchId(), searchKeyword, status, ticketType, fromDate, toDate, pageNumber, pageSize);
            return Ok(result);
        }

        // ============================================================
        // GET /api/outwardinventory/{id}
        // ============================================================
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetOutwardInventoryById(Guid id)
        {
            var result = await _outwardInventoryService.GetOutwardInventoryByIdAsync(
                User, id, GetCurrentBranchId());
            return Ok(result);
        }

        // ============================================================
        // POST /api/outwardinventory
        // Tạo phiếu mới với Status = PENDING (chưa cập nhật kho)
        // ============================================================
        [HttpPost]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_CREATE)]
        public async Task<IActionResult> CreateOutwardInventory(
            [FromBody] OutwardInventoryCreateDto dto,
            [FromQuery] Guid? branchId = null)
        {
            var result = await _outwardInventoryService.CreateOutwardInventoryAsync(
                User, dto, branchId);
            return Ok(result);
        }

        // ============================================================
        // PUT /api/outwardinventory/{id}
        // Chỉ cho phép sửa khi Status = PENDING
        // ============================================================
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateOutwardInventory(Guid id, [FromBody] OutwardInventoryUpdateDto dto)
        {
            var result = await _outwardInventoryService.UpdateOutwardInventoryAsync(
                User, id, dto, GetCurrentBranchId());
            return Ok(result);
        }

        // ============================================================
        // POST /api/outwardinventory/{id}/confirm
        // Xác nhận phiếu xuất kho - Kiểm tra tồn kho & cập nhật kho
        // ============================================================
        [HttpPost("{id:guid}/confirm")]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_CONFIRM)]
        public async Task<IActionResult> ConfirmOutwardInventory(Guid id)
        {
            var result = await _outwardInventoryService.ConfirmOutwardInventoryAsync(
                User, id, GetCurrentBranchId());
            return Ok(result);
        }

        // ============================================================
        // POST /api/outwardinventory/{id}/cancel
        // Hủy phiếu xuất kho - Xử lý rollback tồn kho
        // Quyen huy:
        // - PENDING: nguoi tao phieu hoac co STOCK_OUTWARD_DELETE hoac Owner
        // - COMPLETED: chi co STOCK_OUTWARD_DELETE hoac Owner
        // ============================================================
        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> CancelOutwardInventory(Guid id, [FromBody] CancelTicketDto dto)
        {
            var result = await _outwardInventoryService.CancelOutwardInventoryAsync(
                User, id, dto, GetCurrentBranchId());
            return Ok(result);
        }

        // ============================================================
        // DELETE /api/outwardinventory/{id}
        // ⚠️ DEPRECATED: Chỉ giữ lại cho tương thích FE cũ.
        // FE mới nên dùng: POST /api/outwardinventory/{id}/cancel
        // ============================================================
        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_DELETE)]
        public async Task<IActionResult> DeleteOutwardInventory(Guid id)
        {
            var cancelDto = new CancelTicketDto { CancelReason = "Hủy qua API DELETE (legacy)" };
            var result = await _outwardInventoryService.CancelOutwardInventoryAsync(
                User, id, cancelDto, GetCurrentBranchId());
            return Ok(result);
        }

        // ============================================================
        // GET /api/outwardinventory/{id}/print
        // Tra ve HTML phieu xuat kho
        // ============================================================
        [HttpGet("{id:guid}/print")]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_CREATE)]
        public async Task<IActionResult> Print(Guid id)
        {
            var html = await _outwardInventoryService.GetPrintHtmlAsync(User, id, GetCurrentBranchId());
            return Content(html, "text/html");
        }
    }
}