using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MEP_Management_System.Controllers.InventoryController.Supplier
{
    [Authorize] // Bắt buộc phải đăng nhập JWT cho toàn bộ Controller
    [ApiController]
    [Route("api/suppliers")]
    [Tags("Quản lý nhà cung cấp (Inventory)")]
    public class SupplierController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly ISupplierDebtService _supplierDebtService;
        private readonly ICurrentBranchAccessor _currentBranch;

        public SupplierController(MEPDatabaseContext context, ISupplierDebtService supplierDebtService, ICurrentBranchAccessor currentBranch)
        {
            _context = context;
            _supplierDebtService = supplierDebtService;
            _currentBranch = currentBranch;
        }

        // Helper: chuyển mã hiển thị từ GUID (8 ký tự đầu)
        private static string ToCode(Guid id) => id.ToString()[..8].ToUpper();

        // Helper: map Status (short?) -> "active" / "inactive"
        private static string StatusLabel(short? s) => s == 1 ? "active" : "inactive";

        // Helper: map "active" / "inactive" -> short
        private static short ParseStatus(string? s) => s == "inactive" ? (short)0 : (short)1;

        // ============================================================
        // GET /api/suppliers
        // Quyền truy cập: Owner (Mặc định qua Handler) hoặc có quyền SUPPLIER_VIEW
        // ============================================================
        [HttpGet]
        [HasPermission(PermissionConstants.SUPPLIER_VIEW)]
        public async Task<IActionResult> GetSuppliers([FromQuery] SupplierQueryParameters p)
        {
            try
            {
                var branchId = _currentBranch.GetBranchId();
                if (branchId == null) return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

                var query = _context.Suppliers.Where(s => s.BranchId == branchId).AsQueryable();

                // Filter search
                if (!string.IsNullOrWhiteSpace(p.SearchTerm))
                {
                    var term = p.SearchTerm.Trim().ToLower();
                    query = query.Where(s =>
                        s.SupplierName.ToLower().Contains(term) ||
                        (s.ContactPhone != null && s.ContactPhone.Contains(term)) ||
                        (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(term)) ||
                        (s.TaxCode != null && s.TaxCode.ToLower().Contains(term)));
                }

                // Filter status
                if (!string.IsNullOrWhiteSpace(p.Status) && p.Status != "all")
                {
                    var st = ParseStatus(p.Status);
                    query = query.Where(s => s.Status == st);
                }

                // Filter group
                if (!string.IsNullOrWhiteSpace(p.GroupName) && p.GroupName != "all")
                    query = query.Where(s => s.GroupName == p.GroupName);

                // Tính tổng số lượng trực tiếp từ Database bằng CountAsync
                var totalCount = await query.CountAsync();

                // Thực hiện Phân trang (Skip/Take) ngay tại SQL và chỉ lấy trang hiện tại lên RAM
                var suppliers = await query
                    .OrderByDescending(s => s.CreatedAt)
                    .Skip((p.PageNumber - 1) * p.PageSize)
                    .Take(p.PageSize)
                    .ToListAsync();

                // Distinct groups toàn hệ thống của chi nhánh này
                var distinctGroups = await _context.Suppliers
                    .Where(s => s.BranchId == branchId && s.GroupName != null)
                    .Select(s => s.GroupName!)
                    .Distinct()
                    .OrderBy(g => g)
                    .ToListAsync();

                // Tính công nợ chính xác qua ISupplierDebtService (batch) - đảm bảo đồng bộ với /api/supplierdebt
                var supplierIds = suppliers.Select(s => s.SupplierId).ToList();
                var debtMap = await _supplierDebtService.CalculateCurrentDebtBatchAsync(supplierIds);

                // Map dữ liệu của trang hiện tại sang DTO (kèm computed: currentDebt, totalPurchased, overpaid)
                var items = suppliers.Select(s =>
                {
                    var debt = debtMap.TryGetValue(s.SupplierId, out var d) ? d : new SupplierDebtSummary();
                    return new SupplierListItemDto
                    {
                        Id = s.SupplierId,
                        Code = ToCode(s.SupplierId),
                        Name = s.SupplierName,
                        ContactPerson = s.ContactPerson,
                        ContactPhone = s.ContactPhone,
                        ContactEmail = s.ContactEmail,
                        Address = s.Address,
                        GroupName = s.GroupName,
                        TaxCode = s.TaxCode,
                        Status = StatusLabel(s.Status),
                        CreatedAt = s.CreatedAt,
                        CurrentDebt = debt.CurrentDebt,
                        TotalPurchased = debt.TotalPurchased,
                        OverpaidAmount = debt.OverpaidAmount,
                        DebtStatus = debt.Status
                    };
                }).ToList();

                var totalPages = (int)Math.Ceiling(totalCount / (double)p.PageSize);

                return Ok(new DTOs.ApiResponse<SupplierPagedResponse>
                {
                    Success = true,
                    Message = "Suppliers retrieved successfully",
                    Data = new SupplierPagedResponse
                    {
                        Items = items,
                        TotalCount = totalCount,
                        PageNumber = p.PageNumber,
                        PageSize = p.PageSize,
                        TotalPages = totalPages,
                        HasNextPage = p.PageNumber < totalPages,
                        HasPreviousPage = p.PageNumber > 1,
                        DistinctGroups = distinctGroups,
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

        // ============================================================
        // GET /api/suppliers/{id}
        // Quyền truy cập: Owner (Mặc định qua Handler) hoặc có quyền SUPPLIER_VIEW
        // ============================================================
        [HttpGet("{id:guid}")]
        [HasPermission(PermissionConstants.SUPPLIER_VIEW)]
        public async Task<IActionResult> GetSupplier(Guid id)
        {
            try
            {
                var branchId = _currentBranch.GetBranchId();
                if (branchId == null) return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

                var s = await _context.Suppliers
                    .FirstOrDefaultAsync(x => x.SupplierId == id && x.BranchId == branchId);

                if (s == null)
                    return NotFound(new DTOs.ApiResponse<object>
                    {
                        Success = false,
                        Message = "Supplier not found",
                        Errors = new() { $"No supplier with ID: {id}" }
                    });

                return Ok(new ApiResponse<SupplierDetailDto>
                {
                    Success = true,
                    Message = "Supplier retrieved successfully",
                    Data = new SupplierDetailDto
                    {
                        Id = s.SupplierId,
                        Code = ToCode(s.SupplierId),
                        Name = s.SupplierName,
                        ContactPerson = s.ContactPerson,
                        ContactPhone = s.ContactPhone,
                        ContactEmail = s.ContactEmail,
                        TaxCode = s.TaxCode,
                        GroupName = s.GroupName,
                        BankAccount = s.BankAccount,
                        BankName = s.BankName,
                        Website = s.Website,
                        Address = s.Address,
                        Notes = s.Notes,
                        Status = StatusLabel(s.Status),
                        CreatedAt = s.CreatedAt
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

        // ============================================================
        // POST /api/suppliers
        // Quyền truy cập: Owner (Mặc định qua Handler) hoặc có quyền SUPPLIER_MANAGE
        // ============================================================
        [HttpPost]
        [HasPermission(PermissionConstants.SUPPLIER_CREATE)]
        public async Task<IActionResult> CreateSupplier([FromBody] SupplierUpsertRequest req)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                var branchId = _currentBranch.GetBranchId();
                if (branchId == null) return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

                var inputName = req.SupplierName.Trim();
                var exists = await _context.Suppliers
                    .AnyAsync(x => x.SupplierName.Trim().ToLower() == inputName.ToLower() && x.BranchId == branchId);
                if (exists)
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Tên nhà cung cấp đã tồn tại",
                        Errors = new() { $"Nhà cung cấp '{req.SupplierName}' đã tồn tại." }
                    });

                // Lấy UserId từ JWT lưu vào CreatedBy phục vụ audit lịch sử
                Guid? currentUserId = null;
                var nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(nameIdentifier, out var parsedGuid))
                {
                    currentUserId = parsedGuid;
                }

                var s = new Models.Supplier
                {
                    SupplierId = Guid.NewGuid(),
                    SupplierName = inputName,
                    ContactPhone = req.ContactPhone?.Trim(),
                    ContactEmail = req.ContactEmail?.Trim(),
                    ContactPerson = req.ContactPerson?.Trim(),
                    TaxCode = req.TaxCode?.Trim(),
                    GroupName = req.GroupName?.Trim(),
                    BankAccount = req.BankAccount?.Trim(),
                    BankName = req.BankName?.Trim(),
                    Website = req.Website?.Trim(),
                    Address = req.Address?.Trim(),
                    Notes = req.Notes?.Trim(),
                    Status = req.Status,
                    BranchId = branchId,
                    CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                    CreatedBy = currentUserId
                };

                _context.Suppliers.Add(s);
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<SupplierDetailDto>
                {
                    Success = true,
                    Message = "Supplier created successfully",
                    Data = new SupplierDetailDto
                    {
                        Id = s.SupplierId,
                        Code = ToCode(s.SupplierId),
                        Name = s.SupplierName,
                        ContactPerson = s.ContactPerson,
                        ContactPhone = s.ContactPhone,
                        ContactEmail = s.ContactEmail,
                        TaxCode = s.TaxCode,
                        GroupName = s.GroupName,
                        BankAccount = s.BankAccount,
                        BankName = s.BankName,
                        Website = s.Website,
                        Address = s.Address,
                        Notes = s.Notes,
                        Status = StatusLabel(s.Status),
                        CreatedAt = s.CreatedAt
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

        // ============================================================
        // PUT /api/suppliers/{id}
        // Quyền truy cập: Owner (Mặc định qua Handler) hoặc có quyền SUPPLIER_MANAGE
        // ============================================================
        [HttpPut("{id:guid}")]
        [HasPermission(PermissionConstants.SUPPLIER_UPDATE)]
        public async Task<IActionResult> UpdateSupplier(Guid id, [FromBody] SupplierUpsertRequest req)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                var branchId = _currentBranch.GetBranchId();
                if (branchId == null) return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

                var s = await _context.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == id && x.BranchId == branchId);
                if (s == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Supplier not found",
                        Errors = new() { $"No supplier with ID: {id}" }
                    });

                var inputName = req.SupplierName.Trim();

                // Kiểm tra trùng tên với nhà cung cấp khác có kèm theo Trim()
                var nameExists = await _context.Suppliers
                    .AnyAsync(x => x.SupplierId != id && x.BranchId == branchId
                                && x.SupplierName.Trim().ToLower() == inputName.ToLower());
                if (nameExists)
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Tên nhà cung cấp đã tồn tại",
                        Errors = new() { $"Nhà cung cấp '{req.SupplierName}' đã tồn tại." }
                    });

                // 🌟 Lấy UserId từ JWT lưu vào UpdatedBy để theo dõi ai là người sửa đổi cuối cùng
                Guid? currentUserId = null;
                var nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(nameIdentifier, out var parsedGuid))
                {
                    currentUserId = parsedGuid;
                }

                s.SupplierName = inputName;
                s.ContactPhone = req.ContactPhone?.Trim();
                s.ContactEmail = req.ContactEmail?.Trim();
                s.ContactPerson = req.ContactPerson?.Trim();
                s.TaxCode = req.TaxCode?.Trim();
                s.GroupName = req.GroupName?.Trim();
                s.BankAccount = req.BankAccount?.Trim();
                s.BankName = req.BankName?.Trim();
                s.Website = req.Website?.Trim();
                s.Address = req.Address?.Trim();
                s.Notes = req.Notes?.Trim();
                s.Status = req.Status;

                // 🌟 Ghi nhận vết Audit sửa đổi
                s.UpdatedBy = currentUserId;
                s.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<SupplierDetailDto>
                {
                    Success = true,
                    Message = "Supplier updated successfully",
                    Data = new SupplierDetailDto
                    {
                        Id = s.SupplierId,
                        Code = ToCode(s.SupplierId),
                        Name = s.SupplierName,
                        ContactPerson = s.ContactPerson,
                        ContactPhone = s.ContactPhone,
                        ContactEmail = s.ContactEmail,
                        TaxCode = s.TaxCode,
                        GroupName = s.GroupName,
                        BankAccount = s.BankAccount,
                        BankName = s.BankName,
                        Website = s.Website,
                        Address = s.Address,
                        Notes = s.Notes,
                        Status = StatusLabel(s.Status),
                        CreatedAt = s.CreatedAt
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

        // ============================================================
        // DELETE /api/suppliers/{id}  (Hard Delete)
        // Quyền truy cập: Owner (Mặc định qua Handler) hoặc có quyền SUPPLIER_MANAGE
        // ============================================================
        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionConstants.SUPPLIER_DELETE)]
        public async Task<IActionResult> DeleteSupplier(Guid id)
        {
            try
            {
                var branchId = _currentBranch.GetBranchId();
                if (branchId == null) return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

                var s = await _context.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == id && x.BranchId == branchId);
                if (s == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Supplier not found",
                        Errors = new() { $"No supplier with ID: {id}" }
                    });
                _context.Suppliers.Remove(s);

                // Lưu thay đổi (EF Core sẽ sinh ra lệnh DELETE FROM suppliers WHERE...)
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Supplier deleted permanently from database" // Sửa lại message cho đúng bản chất
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

        // ============================================================
        // PATCH /api/suppliers/{id}/status
        // Toggle trạng thái hợp tác: "active" ↔ "inactive" (Ngừng hợp tác / Kích hoạt lại)
        // Body: { "status": "active" | "inactive" }
        // Quyền truy cập: SUPPLIER_UPDATE
        // ============================================================
        [HttpPatch("{id:guid}/status")]
        [HasPermission(PermissionConstants.SUPPLIER_UPDATE)]
        public async Task<IActionResult> ToggleSupplierStatus(Guid id, [FromBody] StatusToggleRequest req)
        {
            try
            {
                if (req == null || string.IsNullOrWhiteSpace(req.Status))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Status is required",
                        Errors = new() { "Body must be: { \"status\": \"active\" | \"inactive\" }" }
                    });
                }

                var branchId = _currentBranch.GetBranchId();
                if (branchId == null) return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

                var s = await _context.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == id && x.BranchId == branchId);
                if (s == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Supplier not found",
                        Errors = new() { $"No supplier with ID: {id}" }
                    });

                var newStatus = ParseStatus(req.Status);
                s.Status = newStatus;

                // Audit: người cập nhật
                Guid? currentUserId = null;
                var nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(nameIdentifier, out var parsedGuid))
                {
                    currentUserId = parsedGuid;
                }
                s.UpdatedBy = currentUserId;
                s.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = newStatus == 1 ? "Đã kích hoạt lại nhà cung cấp." : "Đã ngừng hợp tác với nhà cung cấp.",
                    Data = new { Id = s.SupplierId, Status = StatusLabel(s.Status) }
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
    }

    /// <summary>Request body cho PATCH /api/suppliers/{id}/status. Hỗ trợ "active" | "inactive".</summary>
    public class StatusToggleRequest
    {
        public string? Status { get; set; }
    }
}