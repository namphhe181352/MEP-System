using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MEP_Management_System.Controllers.InventoryController.Supplier
{
    [Authorize]
    [ApiController]
    [Route("api/supplierpayments")]
    [Tags("Quản lý nhà cung cấp (Inventory)")]
    public class SupplierPaymentController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;

        public SupplierPaymentController(MEPDatabaseContext context)
        {
            _context = context;
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid.TryParse(userIdClaim, out Guid userId);
            return userId;
        }

        [HttpGet]
        [HasPermission(PermissionConstants.SUPPLIER_PAYMENT_VIEW)]
        public async Task<IActionResult> GetAll([FromQuery] Guid? supplierId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.SupplierPayments
                    .Include(p => p.Supplier)
                    .Include(p => p.PurchaseOrder)
                    .Include(p => p.RecordedByUser)
                    .AsQueryable();

                if (supplierId.HasValue)
                    query = query.Where(p => p.SupplierId == supplierId.Value);

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new SupplierPaymentResponseDto
                    {
                        PaymentId = p.PaymentId,
                        SupplierId = p.SupplierId,
                        SupplierName = p.Supplier != null ? p.Supplier.SupplierName : "",
                        OrderCode = p.PurchaseOrder != null ? p.PurchaseOrder.OrderCode : null,
                        Amount = p.Amount,
                        PaymentDate = p.PaymentDate.ToString("yyyy-MM-dd HH:mm"),
                        PaymentMethod = p.PaymentMethod,
                        ReferenceCode = p.ReferenceCode,
                        Note = p.Note,
                        RecordedByName = p.RecordedByUser != null ? p.RecordedByUser.Username : "",
                        Status = p.IsDeleted == 1 ? "CANCELLED" : "ACTIVE",
                        CreatedBy = p.CreatedBy
                    }).ToListAsync();

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
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error",
                    Errors = new() { ex.Message }
                });
            }
        }

        [HttpPost]
        [HasPermission(PermissionConstants.SUPPLIER_PAYMENT_CREATE)]
        public async Task<IActionResult> Create([FromBody] CreateSupplierPaymentDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();

                var payment = new SupplierPayment
                {
                    PaymentId = Guid.NewGuid(),
                    SupplierId = dto.SupplierId,
                    PurchaseOrderId = dto.PurchaseOrderId,
                    Amount = dto.Amount,
                    PaymentDate = DateTime.UtcNow,
                    PaymentMethod = dto.PaymentMethod,
                    ReferenceCode = dto.ReferenceCode,
                    Note = dto.Note,
                    RecordedBy = currentUserId,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = 0,
                    CreatedBy = null
                };

                _context.SupplierPayments.Add(payment);
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object> { Success = true, Message = "Ghi nhận thanh toán công nợ thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Internal server error", Errors = new() { ex.Message } });
            }
        }

        [HttpPut("{id:guid}")]
        [HasPermission(PermissionConstants.SUPPLIER_PAYMENT_UPDATE)]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSupplierPaymentDto dto)
        {
            try
            {
                var payment = await _context.SupplierPayments.FindAsync(id);
                if (payment == null)
                    return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy phiếu chi." });

                payment.Note = dto.Note;

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object> { Success = true, Message = "Cập nhật ghi chú thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Internal server error", Errors = new() { ex.Message } });
            }
        }

        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionConstants.SUPPLIER_PAYMENT_DELETE)]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var payment = await _context.SupplierPayments.FindAsync(id);
                if (payment == null)
                    return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy phiếu chi." });

                payment.IsDeleted = 1;

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object> { Success = true, Message = "Đã hủy phiếu chi thành công (Khôi phục lại dư nợ nhà cung cấp)." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Internal server error", Errors = new() { ex.Message } });
            }
        }
    }
}