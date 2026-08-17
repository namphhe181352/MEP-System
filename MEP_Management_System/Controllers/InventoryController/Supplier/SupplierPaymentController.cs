using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.Services;
using MEP_Management_System.Services.InventoryService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MEP_Management_System.Controllers.InventoryController.Supplier
{
    [Authorize]
    [ApiController]
    [Route("api/supplierpayments")]
    [Tags("Quản lý nhà cung cấp (Inventory)")]
    public class SupplierPaymentController : ControllerBase
    {
        private readonly ISupplierPaymentService _supplierPaymentService;
        private readonly ICurrentBranchAccessor _currentBranch;

        public SupplierPaymentController(ISupplierPaymentService supplierPaymentService, ICurrentBranchAccessor currentBranch)
        {
            _supplierPaymentService = supplierPaymentService;
            _currentBranch = currentBranch;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid.TryParse(userIdClaim, out Guid userId);
            return userId;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] Guid? supplierId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.SUPPLIER_PAYMENT_VIEW)
                && !User.HasClaim("permission", PermissionConstants.SUPPLIER_PAYMENT_CREATE)
                && !User.HasClaim("permission", PermissionConstants.SUPPLIER_PAYMENT_DELETE))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền xem thanh toán NCC." });

            var branchId = _currentBranch.GetBranchId();

            var (items, totalCount) = await _supplierPaymentService.GetPaymentsAsync(branchId, supplierId, pageNumber, pageSize);

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Supplier payments retrieved successfully",
                Data = new
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    HasNextPage = pageNumber < totalPages,
                    HasPreviousPage = pageNumber > 1
                }
            });
        }

        [HttpPost]
        [HasPermission(PermissionConstants.SUPPLIER_PAYMENT_CREATE)]
        public async Task<IActionResult> Create([FromBody] DTOs.InventoryDTOs.CreateSupplierPaymentDto dto)
        {
            var currentUserId = GetCurrentUserId();
            await _supplierPaymentService.CreatePaymentAsync(dto, currentUserId);

            return Ok(new ApiResponse<object> { Success = true, Message = "Ghi nhận thanh toán công nợ thành công." });
        }

        [HttpPut("{id:guid}")]
        [HasPermission(PermissionConstants.SUPPLIER_PAYMENT_UPDATE)]
        public async Task<IActionResult> Update(Guid id, [FromBody] DTOs.InventoryDTOs.UpdateSupplierPaymentDto dto)
        {
            await _supplierPaymentService.UpdatePaymentAsync(id, dto);

            return Ok(new ApiResponse<object> { Success = true, Message = "Cập nhật phiếu chi thành công." });
        }

        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionConstants.SUPPLIER_PAYMENT_DELETE)]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _supplierPaymentService.DeletePaymentAsync(id);

            return Ok(new ApiResponse<object> { Success = true, Message = "Đã hủy phiếu chi thành công (Khôi phục lại dư nợ nhà cung cấp)." });
        }
    }
}
