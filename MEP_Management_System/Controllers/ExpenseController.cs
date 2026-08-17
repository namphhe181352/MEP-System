using AutoMapper;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Services;
using MEP_Management_System.Services.SaleServices;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MEP_Management_System.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Phiếu Chi Tiền (Expense)")]
    public class ExpenseController : ControllerBase
    {
        private readonly IExpenseService _expenseService;
        private readonly IBranchResolutionService _branchResolver;
        private readonly ICurrentBranchAccessor _currentBranch;

        public ExpenseController(IExpenseService expenseService, IBranchResolutionService branchResolver, ICurrentBranchAccessor currentBranch)
        {
            _expenseService = expenseService;
            _branchResolver = branchResolver;
            _currentBranch = currentBranch;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
        }

        private bool HasGlobalAccess() => User.IsInRole(PermissionConstants.OWNER_ROLE);

        private async Task<Guid> ResolveBranchId(Guid? branchIdParam = null)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                throw new UnauthorizedAccessException("Không xác định được người dùng.");

            return await _branchResolver.ResolveBranchIdAsync(
                branchIdParam,
                currentUserId.Value,
                HasGlobalAccess(),
                _currentBranch.GetBranchId()
            );
        }

        [HttpGet]
        [HasPermission(PermissionConstants.EXPENSE_VIEW)]
        public async Task<IActionResult> GetExpenseVouchers(
            [FromQuery] ExpenseVoucherParameters @params,
            [FromQuery] Guid? branchId = null)
        {
            try
            {
                var branchIdResolved = await ResolveBranchId(branchId);
                var result = await _expenseService.GetVouchersAsync(branchIdResolved, @params);
                return Ok(new ApiResponse<PaginatedResponse<ExpenseVoucherResponseDto>>
                {
                    Success = true,
                    Message = "Lấy danh sách phiếu chi tiền thành công.",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi lấy danh sách phiếu chi.",
                    Errors = new() { ex.Message }
                });
            }
        }

        [HttpGet("{id:guid}")]
        [HasPermission(PermissionConstants.EXPENSE_VIEW)]
        public async Task<IActionResult> GetExpenseVoucherById(Guid id)
        {
            try
            {
                var branchIdResolved = await ResolveBranchId();
                var voucher = await _expenseService.GetVoucherByIdAsync(id, branchIdResolved);
                if (voucher == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy phiếu chi tiền."
                    });

                return Ok(new ApiResponse<ExpenseVoucherResponseDto>
                {
                    Success = true,
                    Message = "Lấy chi tiết phiếu chi tiền thành công.",
                    Data = voucher
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi lấy chi tiết phiếu chi.",
                    Errors = new() { ex.Message }
                });
            }
        }

        [HttpPost]
        [HasPermission(PermissionConstants.EXPENSE_CREATE)]
        public async Task<IActionResult> CreateExpenseVoucher(
            [FromBody] ExpenseVoucherCreateDto dto,
            [FromQuery] Guid? branchId = null)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không xác định được định danh người dùng từ Token."
                    });

                var branchIdResolved = await ResolveBranchId(branchId);
                var result = await _expenseService.CreateVoucherAsync(dto, currentUserId.Value, branchIdResolved);

                return Ok(new ApiResponse<ExpenseVoucherResponseDto>
                {
                    Success = true,
                    Message = "Tạo phiếu chi tiền thành công.",
                    Data = result
                });
            }
            catch (BusinessException ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi tạo phiếu chi tiền.",
                    Errors = new() { inner, ex.Message }
                });
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateExpenseVoucher(Guid id, [FromBody] UpdateExpenseVoucherDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                    return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không xác định được người dùng." });

                var result = await _expenseService.UpdateVoucherAsync(id, dto, currentUserId.Value, HasGlobalAccess());
                if (result == null)
                    return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy phiếu chi." });

                return Ok(new ApiResponse<object> { Success = true, Message = "Cập nhật phiếu chi thành công." });
            }
            catch (BusinessException ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi khi cập nhật phiếu chi.", Errors = new() { ex.Message } });
            }
        }

        [HttpPatch("{id:guid}/confirm")]
        [HasPermission(PermissionConstants.EXPENSE_CONFIRM)]
        public async Task<IActionResult> ConfirmExpenseVoucher(Guid id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                    return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không xác định được định danh người dùng." });

                var branchIdResolved = await ResolveBranchId();
                var result = await _expenseService.ConfirmVoucherAsync(id, currentUserId.Value, branchIdResolved);

                if (result == null)
                    return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy phiếu chi tiền." });

                return Ok(new ApiResponse<ExpenseVoucherResponseDto>
                {
                    Success = true,
                    Message = "Xác nhận phiếu chi tiền thành công.",
                    Data = result
                });
            }
            catch (BusinessException ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi xác nhận phiếu chi tiền.",
                    Errors = new() { ex.Message }
                });
            }
        }

        [HttpPatch("{id:guid}/cancel")]
        [HasPermission(PermissionConstants.EXPENSE_CANCEL)]
        public async Task<IActionResult> CancelExpenseVoucher(Guid id, [FromBody] CancelExpenseVoucherDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                    return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không xác định được định danh người dùng." });

                var branchIdResolved = await ResolveBranchId();
                var result = await _expenseService.CancelVoucherAsync(id, dto, currentUserId.Value, branchIdResolved);

                if (result == null)
                    return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy phiếu chi tiền." });

                return Ok(new ApiResponse<ExpenseVoucherResponseDto>
                {
                    Success = true,
                    Message = "Hủy phiếu chi tiền thành công.",
                    Data = result
                });
            }
            catch (BusinessException ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi hủy phiếu chi tiền.",
                    Errors = new() { ex.Message }
                });
            }
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetExpenseCategories([FromQuery] Guid? branchId = null)
        {
            try
            {
                var branchIdResolved = await ResolveBranchId(branchId);
                var result = await _expenseService.GetCategoriesAsync(branchIdResolved);
                return Ok(new ApiResponse<List<ExpenseCategoryDto>>
                {
                    Success = true,
                    Message = "Lấy danh sách nhóm chi phí thành công.",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi lấy danh sách nhóm chi phí.",
                    Errors = new() { ex.Message }
                });
            }
        }

        [HttpPost("categories")]
        [HasPermission(PermissionConstants.EXPENSE_CATEGORY_MANAGE)]
        public async Task<IActionResult> CreateExpenseCategory([FromBody] ExpenseCategoryUpsertDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                    return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không xác định được định danh người dùng." });

                var branchIdResolved = await ResolveBranchId();
                var result = await _expenseService.CreateCategoryAsync(dto, currentUserId.Value, branchIdResolved);

                return Ok(new ApiResponse<ExpenseCategoryDto>
                {
                    Success = true,
                    Message = "Tạo nhóm chi phí thành công.",
                    Data = result
                });
            }
            catch (BusinessException ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi tạo nhóm chi phí.",
                    Errors = new() { ex.Message }
                });
            }
        }

        [HttpPut("categories/{id:guid}")]
        [HasPermission(PermissionConstants.EXPENSE_CATEGORY_MANAGE)]
        public async Task<IActionResult> UpdateExpenseCategory(Guid id, [FromBody] ExpenseCategoryUpsertDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                    return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không xác định được định danh người dùng." });

                var branchIdResolved = await ResolveBranchId();
                var result = await _expenseService.UpdateCategoryAsync(id, dto, currentUserId.Value, branchIdResolved);

                if (result == null)
                    return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy nhóm chi phí." });

                return Ok(new ApiResponse<ExpenseCategoryDto>
                {
                    Success = true,
                    Message = "Cập nhật nhóm chi phí thành công.",
                    Data = result
                });
            }
            catch (BusinessException ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi cập nhật nhóm chi phí.",
                    Errors = new() { ex.Message }
                });
            }
        }

        [HttpDelete("categories/{id:guid}")]
        [HasPermission(PermissionConstants.EXPENSE_CATEGORY_MANAGE)]
        public async Task<IActionResult> DeleteExpenseCategory(Guid id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                    return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không xác định được định danh người dùng." });

                var branchIdResolved = await ResolveBranchId();
                await _expenseService.DeleteCategoryAsync(id, currentUserId.Value, branchIdResolved);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Đã xóa nhóm chi phí."
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
            catch (BusinessException ex)
            {
                return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi xóa nhóm chi phí.",
                    Errors = new() { ex.Message }
                });
            }
        }
    }
}
