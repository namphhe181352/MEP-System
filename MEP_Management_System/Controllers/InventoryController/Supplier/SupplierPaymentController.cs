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

        private (Guid UserId, string Role, List<string> Permissions) GetUserInfo()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? "";
            
            // Lấy danh sách permission được nhúng trong JWT (nếu có), hoặc trả về list rỗng
            var permissions = User.FindAll("permission").Select(c => c.Value).ToList();
            
            Guid.TryParse(userIdClaim, out Guid userId);
            return (userId, roleClaim, permissions);
        }

        private bool HasPermission(string requiredPermission)
        {
            var user = GetUserInfo();
            if (user.Role == "Owner") return true; // Owner auto pass
            return user.Permissions.Contains(requiredPermission);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] Guid? supplierId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
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
                    SupplierName = p.Supplier.SupplierName,
                    OrderCode = p.PurchaseOrder != null ? p.PurchaseOrder.OrderCode : null,
                    Amount = p.Amount,
                    PaymentDate = p.PaymentDate.ToString("yyyy-MM-dd HH:mm"),
                    PaymentMethod = p.PaymentMethod,
                    ReferenceCode = p.ReferenceCode,
                    Note = p.Note,
                    RecordedByName = p.RecordedByUser != null ? p.RecordedByUser.Username : null, // Giả định trường Name hiển thị của User
                    Status = p.IsDeleted == 1 ? "CANCELLED" : "ACTIVE"
                }).ToListAsync();

            return Ok(new ApiResponse<PaginatedResponse<SupplierPaymentResponseDto>>
            {
                Success = true,
                Data = new PaginatedResponse<SupplierPaymentResponseDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                }
            });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSupplierPaymentDto dto)
        {
            if (!HasPermission("Create_Supplier_Debt"))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền thực hiện chức năng này." });

            var user = GetUserInfo();

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
                RecordedBy = user.UserId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = 0
            };

            _context.SupplierPayments.Add(payment);
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object> { Success = true, Message = "Ghi nhận thanh toán công nợ thành công." });
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSupplierPaymentDto dto)
        {
            if (!HasPermission("Edit_Supplier_Debt"))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền chỉnh sửa." });

            var payment = await _context.SupplierPayments.FindAsync(id);
            if (payment == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy phiếu chi." });

            payment.Note = dto.Note; // Chỉ cho sửa ghi chú giải trình, tuyệt đối không cho sửa Số tiền 'Amount' để tránh lệch sổ sách
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object> { Success = true, Message = "Cập nhật ghi chú thành công." });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!HasPermission("Cancel_Supplier_Debt"))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền hủy phiếu chi này." });

            var payment = await _context.SupplierPayments.FindAsync(id);
            if (payment == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy phiếu chi." });

            payment.IsDeleted = 1; // Khóa / Hủy phiếu thanh toán, công nợ sẽ tự động tăng lại tương ứng
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object> { Success = true, Message = "Đã hủy phiếu chi thành công (Khôi phục lại dư nợ)." });
        }
    }
}