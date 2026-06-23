using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.InventoryController.Supplier
{
    [Authorize]
    [ApiController]
    [Route("api/suppliers")]
    [Tags("Quản lý nhà cung cấp (Inventory)")]
    public class SupplierController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;

        public SupplierController(MEPDatabaseContext context)
        {
            _context = context;
        }

        // Helper: chuyển mã hiển thị từ GUID (8 ký tự đầu)
        private static string ToCode(Guid id) => id.ToString()[..8].ToUpper();

        // Helper: map Status (short?) -> "active" / "inactive"
        private static string StatusLabel(short? s) => s == 1 ? "active" : "inactive";

        // Helper: map "active" / "inactive" -> short
        private static short ParseStatus(string? s) => s == "inactive" ? (short)0 : (short)1;

        // ============================================================
        // GET /api/suppliers
        // Query: searchTerm, status (active|inactive|all), groupName,
        //        pageNumber, pageSize
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetSuppliers([FromQuery] SupplierQueryParameters p)
        {
            try
            {
                var query = _context.Suppliers
                    .Include(s => s.PurchaseOrders)
                    .AsQueryable();

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

                var suppliers = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();

                // Distinct groups
                var distinctGroups = await _context.Suppliers
                    .Where(s => s.GroupName != null)
                    .Select(s => s.GroupName!)
                    .Distinct()
                    .OrderBy(g => g)
                    .ToListAsync();

                // Map sang DTO (kèm computed: currentDebt, totalPurchased)
                var items = suppliers.Select(s => new SupplierListItemDto
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
                    CurrentDebt = s.PurchaseOrders
                        .Where(po => po.Status == "APPROVED")
                        .Sum(po => po.TotalAmount ?? 0),
                    TotalPurchased = s.PurchaseOrders
                        .Where(po => po.Status == "COMPLETED" || po.Status == "APPROVED")
                        .Sum(po => po.TotalAmount ?? 0),
                    AverageRating = s.AverageRating,
                    RatingCount = s.RatingCount ?? 0,
                    LastRatedAt = s.LastRatedAt,
                }).ToList();

                var totalCount = items.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)p.PageSize);
                var paged = items
                    .Skip((p.PageNumber - 1) * p.PageSize)
                    .Take(p.PageSize)
                    .ToList();

                return Ok(new DTOs.ApiResponse<SupplierPagedResponse>
                {
                    Success = true,
                    Message = "Suppliers retrieved successfully",
                    Data = new SupplierPagedResponse
                    {
                        Items = paged,
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
        // ============================================================
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetSupplier(Guid id)
        {
            try
            {
                var s = await _context.Suppliers
                    .FirstOrDefaultAsync(x => x.SupplierId == id);

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
                        CreatedAt = s.CreatedAt,
                        AverageRating = s.AverageRating,
                        RatingCount = s.RatingCount ?? 0,
                        LastRatedAt = s.LastRatedAt,
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
        // ============================================================
        [HttpPost]
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

                // Check trùng tên (case-insensitive)
                var exists = await _context.Suppliers
                    .AnyAsync(x => x.SupplierName.ToLower() == req.SupplierName.ToLower());
                if (exists)
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Supplier name already exists",
                        Errors = new() { $"Nhà cung cấp '{req.SupplierName}' đã tồn tại." }
                    });

                var s = new Models.Supplier
                {
                    SupplierId = Guid.NewGuid(),
                    SupplierName = req.SupplierName.Trim(),
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
                    CreatedAt = DateTime.UtcNow,
                    RatingCount = 0,
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
                        CreatedAt = s.CreatedAt,
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
        // ============================================================
        [HttpPut("{id:guid}")]
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

                var s = await _context.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == id);
                if (s == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Supplier not found",
                        Errors = new() { $"No supplier with ID: {id}" }
                    });

                // Check trùng tên với NCC khác
                var nameExists = await _context.Suppliers
                    .AnyAsync(x => x.SupplierId != id
                                && x.SupplierName.ToLower() == req.SupplierName.ToLower());
                if (nameExists)
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Supplier name already exists",
                        Errors = new() { $"Nhà cung cấp '{req.SupplierName}' đã tồn tại." }
                    });

                s.SupplierName = req.SupplierName.Trim();
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
                        CreatedAt = s.CreatedAt,
                        AverageRating = s.AverageRating,
                        RatingCount = s.RatingCount ?? 0,
                        LastRatedAt = s.LastRatedAt,
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
        // DELETE /api/suppliers/{id}  (soft delete: set Status = 0)
        // ============================================================
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteSupplier(Guid id)
        {
            try
            {
                var s = await _context.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == id);
                if (s == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Supplier not found",
                        Errors = new() { $"No supplier with ID: {id}" }
                    });

                // Soft delete: chỉ tắt trạng thái, giữ lại lịch sử PO
                s.Status = 0;
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Supplier deactivated successfully"
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
}
