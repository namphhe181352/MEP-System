using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        private readonly MEPDatabaseContext _context;
        private readonly ICurrentBranchAccessor _currentBranch;

        public ExpenseController(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch)
        {
            _context = context;
            _currentBranch = currentBranch;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
        }

        private bool HasGlobalAccess()
        {
            return User.IsInRole(PermissionConstants.OWNER_ROLE);
        }

        /// <summary>
        /// Resolve branchId - Ưu tiên: DefaultBranchId > branchId param > JWT > Active branch
        /// </summary>
        private async Task<Guid?> ResolveBranchId(Guid? branchIdParam = null)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null) return null;

            // Ưu tiên 1: DefaultBranchId từ user
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == currentUserId.Value);

            if (user?.DefaultBranchId != null && user.DefaultBranchId != Guid.Empty)
            {
                return user.DefaultBranchId;
            }

            // Ưu tiên 2: branchId từ query param (Owner)
            if (HasGlobalAccess() && branchIdParam.HasValue)
            {
                return branchIdParam.Value;
            }

            // Ưu tiên 3: BranchId từ JWT
            var jwtBranchId = _currentBranch.GetBranchId();
            if (jwtBranchId.HasValue)
            {
                return jwtBranchId.Value;
            }

            // Ưu tiên 4: Lấy chi nhánh active duy nhất
            var activeBranch = await _context.Branches
                .AsNoTracking()
                .Where(b => b.IsActive == 1)
                .FirstOrDefaultAsync();

            return activeBranch?.BranchId;
        }

        /// <summary>
        /// Ghi log hệ thống (silent error handling - không làm fail request chính).
        /// </summary>
        private async Task TryWriteSystemLog(string action, string entityType, Guid entityId, string description)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null) return;

                var log = new SystemLog
                {
                    LogId = Guid.NewGuid(),
                    UserId = currentUserId.Value,
                    Action = action,
                    TableName = entityType.ToLower(),
                    EntityType = entityType,
                    EntityId = entityId,
                    Description = description,
                    Level = "INFO",
                    Source = "ExpenseController",
                    Timestamp = DateTime.UtcNow
                };

                _context.SystemLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Silent error - không làm fail request chính
            }
        }

        #region Expense Voucher Endpoints

        /// <summary>
        /// GET /api/expense - Danh sách phiếu chi tiền với phân trang và filter.
        /// </summary>
        [HttpGet]
        [HasPermission(PermissionConstants.EXPENSE_VIEW)]
        public async Task<IActionResult> GetExpenseVouchers(
            [FromQuery] ExpenseVoucherParameters @params,
            [FromQuery] Guid? branchId = null)
        {
            try
            {
                if (@params.PageNumber < 1) @params.PageNumber = 1;
                if (@params.PageSize < 1 || @params.PageSize > 100) @params.PageSize = 20;

                var branchIdResolved = await ResolveBranchId(branchId);
                if (branchIdResolved == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ.",
                        Errors = new() { "No valid branch found." }
                    });
                }

                var query = _context.ExpenseVouchers
                    .Include(ev => ev.Category)
                    .Include(ev => ev.Supplier)
                    .Include(ev => ev.CreatedByUser)
                    .Include(ev => ev.ConfirmedByUser)
                    .Include(ev => ev.CancelledByUser)
                    .Include(ev => ev.Branch)
                    .Where(ev => ev.BranchId == branchIdResolved)
                    .AsQueryable();

                // Filter theo status
                if (!string.IsNullOrWhiteSpace(@params.Status))
                {
                    var normalizedStatus = @params.Status.Trim().ToUpperInvariant();
                    if (normalizedStatus != "ALL")
                    {
                        query = query.Where(ev => ev.Status != null && ev.Status.ToUpper() == normalizedStatus);
                    }
                }
                else
                {
                    // Mặc định: Ẩn CANCELLED
                    query = query.Where(ev => ev.Status != "CANCELLED");
                }

                // Filter theo category
                if (@params.CategoryId.HasValue)
                {
                    query = query.Where(ev => ev.CategoryId == @params.CategoryId.Value);
                }

                // Filter theo supplier
                if (@params.SupplierId.HasValue)
                {
                    query = query.Where(ev => ev.SupplierId == @params.SupplierId.Value);
                }

                // Filter theo khoảng ngày
                if (@params.FromDate.HasValue)
                {
                    query = query.Where(ev => ev.CreatedAt >= @params.FromDate.Value);
                }

                if (@params.ToDate.HasValue)
                {
                    query = query.Where(ev => ev.CreatedAt <= @params.ToDate.Value.AddDays(1));
                }

                // Sorting
                query = ApplyVoucherSorting(query, @params.Sort, @params.Order);

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)@params.PageSize);

                var items = await query
                    .Skip((@params.PageNumber - 1) * @params.PageSize)
                    .Take(@params.PageSize)
                    .ToListAsync();

                var resultList = items.Select(MapToExpenseVoucherResponse).ToList();

                return Ok(new ApiResponse<PaginatedResponse<ExpenseVoucherResponseDto>>
                {
                    Success = true,
                    Message = "Lấy danh sách phiếu chi tiền thành công.",
                    Data = new PaginatedResponse<ExpenseVoucherResponseDto>
                    {
                        Items = resultList,
                        TotalCount = totalCount,
                        PageNumber = @params.PageNumber,
                        PageSize = @params.PageSize,
                        TotalPages = totalPages,
                        HasNextPage = @params.PageNumber < totalPages,
                        HasPreviousPage = @params.PageNumber > 1
                    }
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

        /// <summary>
        /// GET /api/expense/{id} - Chi tiết một phiếu chi tiền.
        /// </summary>
        [HttpGet("{id:guid}")]
        [HasPermission(PermissionConstants.EXPENSE_VIEW)]
        public async Task<IActionResult> GetExpenseVoucherById(Guid id)
        {
            try
            {
                var branchIdResolved = await ResolveBranchId();
                if (branchIdResolved == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                var voucher = await _context.ExpenseVouchers
                    .Include(ev => ev.Category)
                    .Include(ev => ev.Supplier)
                    .Include(ev => ev.CreatedByUser)
                    .Include(ev => ev.ConfirmedByUser)
                    .Include(ev => ev.CancelledByUser)
                    .Include(ev => ev.Branch)
                    .FirstOrDefaultAsync(ev => ev.VoucherId == id && ev.BranchId == branchIdResolved);

                if (voucher == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy phiếu chi tiền.",
                        Errors = new() { $"No expense voucher with ID: {id}" }
                    });
                }

                return Ok(new ApiResponse<ExpenseVoucherResponseDto>
                {
                    Success = true,
                    Message = "Lấy chi tiết phiếu chi tiền thành công.",
                    Data = MapToExpenseVoucherResponse(voucher)
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

        /// <summary>
        /// POST /api/expense - Tạo phiếu chi tiền mới (trạng thái PENDING).
        /// </summary>
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
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không xác định được định danh người dùng từ Token."
                    });
                }

                var resolvedBranchId = await ResolveBranchId(branchId);
                if (resolvedBranchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                // Validation
                var errors = new List<string>();
                if (dto.Amount <= 0) errors.Add("Số tiền chi phải lớn hơn 0.");
                if (string.IsNullOrWhiteSpace(dto.Reason)) errors.Add("Lý do chi là bắt buộc.");

                if (errors.Any())
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                // Kiểm tra Category tồn tại và IsActive
                var category = await _context.ExpenseCategories
                    .FirstOrDefaultAsync(c => c.CategoryId == dto.CategoryId && c.IsActive == 1);

                if (category == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Nhóm chi phí không hợp lệ hoặc đã bị vô hiệu hóa.",
                        Errors = new() { $"CategoryId: {dto.CategoryId} không tìm thấy hoặc không hoạt động." }
                    });
                }

                // Kiểm tra Supplier nếu có
                if (dto.SupplierId.HasValue)
                {
                    var supplier = await _context.Suppliers
                        .FirstOrDefaultAsync(s => s.SupplierId == dto.SupplierId.Value && s.Status == 1);

                    if (supplier == null)
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Nhà cung cấp không hợp lệ hoặc đã bị vô hiệu hóa."
                        });
                    }
                }

                var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                var voucherCode = await GenerateVoucherCode(resolvedBranchId.Value, currentTime);

                var voucher = new ExpenseVoucher
                {
                    VoucherId = Guid.NewGuid(),
                    VoucherCode = voucherCode,
                    BranchId = resolvedBranchId.Value,
                    CategoryId = dto.CategoryId,
                    SupplierId = dto.SupplierId,
                    Amount = dto.Amount,
                    Reason = dto.Reason.Trim(),
                    Note = dto.Note?.Trim(),
                    Status = "PENDING",
                    CreatedBy = currentUserId.Value,
                    CreatedAt = currentTime
                };

                _context.ExpenseVouchers.Add(voucher);
                await _context.SaveChangesAsync();

                // Ghi log
                await TryWriteSystemLog(
                    "CREATE_EXPENSE_VOUCHER",
                    "ExpenseVoucher",
                    voucher.VoucherId,
                    $"Tạo phiếu chi {voucherCode} - Số tiền: {voucher.Amount:N0}đ - Lý do: {voucher.Reason}"
                );

                // Reload với includes
                var createdVoucher = await _context.ExpenseVouchers
                    .Include(ev => ev.Category)
                    .Include(ev => ev.Supplier)
                    .Include(ev => ev.CreatedByUser)
                    .Include(ev => ev.Branch)
                    .FirstAsync(ev => ev.VoucherId == voucher.VoucherId);

                return Ok(new ApiResponse<ExpenseVoucherResponseDto>
                {
                    Success = true,
                    Message = "Tạo phiếu chi tiền thành công.",
                    Data = MapToExpenseVoucherResponse(createdVoucher)
                });
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

        /// <summary>
        /// PATCH /api/expense/{id}/confirm - Xác nhận phiếu chi tiền (PENDING → COMPLETED).
        /// </summary>
        [HttpPatch("{id:guid}/confirm")]
        [HasPermission(PermissionConstants.EXPENSE_CONFIRM)]
        public async Task<IActionResult> ConfirmExpenseVoucher(Guid id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không xác định được định danh người dùng."
                    });
                }

                var branchIdResolved = await ResolveBranchId();
                if (branchIdResolved == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                var voucher = await _context.ExpenseVouchers
                    .Include(ev => ev.Category)
                    .Include(ev => ev.Supplier)
                    .Include(ev => ev.CreatedByUser)
                    .Include(ev => ev.Branch)
                    .FirstOrDefaultAsync(ev => ev.VoucherId == id && ev.BranchId == branchIdResolved);

                if (voucher == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy phiếu chi tiền."
                    });
                }

                // Kiểm tra trạng thái
                if (voucher.Status == "COMPLETED")
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Phiếu đã được xác nhận trước đó."
                    });
                }

                if (voucher.Status == "CANCELLED")
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Phiếu đã bị hủy. Không thể xác nhận."
                    });
                }

                var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                voucher.Status = "COMPLETED";
                voucher.ConfirmedBy = currentUserId;
                voucher.CompletedAt = currentTime;

                await _context.SaveChangesAsync();

                // Ghi log
                await TryWriteSystemLog(
                    "CONFIRM_EXPENSE_VOUCHER",
                    "ExpenseVoucher",
                    voucher.VoucherId,
                    $"Xác nhận phiếu chi {voucher.VoucherCode} - Số tiền: {voucher.Amount:N0}đ"
                );

                // Reload để lấy ConfirmedByUser
                var confirmedVoucher = await _context.ExpenseVouchers
                    .Include(ev => ev.Category)
                    .Include(ev => ev.Supplier)
                    .Include(ev => ev.CreatedByUser)
                    .Include(ev => ev.ConfirmedByUser)
                    .Include(ev => ev.CancelledByUser)
                    .Include(ev => ev.Branch)
                    .FirstAsync(ev => ev.VoucherId == voucher.VoucherId);

                return Ok(new ApiResponse<ExpenseVoucherResponseDto>
                {
                    Success = true,
                    Message = "Xác nhận phiếu chi tiền thành công.",
                    Data = MapToExpenseVoucherResponse(confirmedVoucher)
                });
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

        /// <summary>
        /// PATCH /api/expense/{id}/cancel - Hủy phiếu chi tiền (chỉ PENDING mới hủy được).
        /// </summary>
        [HttpPatch("{id:guid}/cancel")]
        [HasPermission(PermissionConstants.EXPENSE_CANCEL)]
        public async Task<IActionResult> CancelExpenseVoucher(Guid id, [FromBody] CancelExpenseVoucherDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không xác định được định danh người dùng."
                    });
                }

                if (string.IsNullOrWhiteSpace(dto?.CancelReason))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Lý do hủy là bắt buộc."
                    });
                }

                var branchIdResolved = await ResolveBranchId();
                if (branchIdResolved == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                var voucher = await _context.ExpenseVouchers
                    .Include(ev => ev.Category)
                    .Include(ev => ev.Supplier)
                    .Include(ev => ev.CreatedByUser)
                    .Include(ev => ev.Branch)
                    .FirstOrDefaultAsync(ev => ev.VoucherId == id && ev.BranchId == branchIdResolved);

                if (voucher == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy phiếu chi tiền."
                    });
                }

                // Kiểm tra trạng thái
                if (voucher.Status == "CANCELLED")
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Phiếu đã được hủy trước đó."
                    });
                }

                var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                voucher.Status = "CANCELLED";
                voucher.CancelledBy = currentUserId;
                voucher.CancelledAt = currentTime;
                voucher.CancelReason = dto.CancelReason.Trim();

                await _context.SaveChangesAsync();

                // Ghi log
                await TryWriteSystemLog(
                    "CANCEL_EXPENSE_VOUCHER",
                    "ExpenseVoucher",
                    voucher.VoucherId,
                    $"Hủy phiếu chi {voucher.VoucherCode} - Lý do: {dto.CancelReason}"
                );

                // Reload để lấy CancelledByUser
                var cancelledVoucher = await _context.ExpenseVouchers
                    .Include(ev => ev.Category)
                    .Include(ev => ev.Supplier)
                    .Include(ev => ev.CreatedByUser)
                    .Include(ev => ev.ConfirmedByUser)
                    .Include(ev => ev.CancelledByUser)
                    .Include(ev => ev.Branch)
                    .FirstAsync(ev => ev.VoucherId == voucher.VoucherId);

                return Ok(new ApiResponse<ExpenseVoucherResponseDto>
                {
                    Success = true,
                    Message = "Hủy phiếu chi tiền thành công.",
                    Data = MapToExpenseVoucherResponse(cancelledVoucher)
                });
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

        #endregion

        #region Expense Category Endpoints

        /// <summary>
        /// GET /api/expense/categories - Danh sách nhóm chi phí kèm số phiếu đang dùng.
        /// </summary>
        [HttpGet("categories")]
        [HasPermission(PermissionConstants.EXPENSE_VIEW)]
        public async Task<IActionResult> GetExpenseCategories()
        {
            try
            {
                var categories = await _context.ExpenseCategories
                    .Include(c => c.ExpenseVouchers)
                    .OrderBy(c => c.CategoryName)
                    .ToListAsync();

                var result = categories.Select(c => new ExpenseCategoryDto
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName,
                    IsActive = c.IsActive,
                    VoucherCount = c.ExpenseVouchers.Count,
                    PendingCount = c.ExpenseVouchers.Count(ev => ev.Status == "PENDING")
                }).ToList();

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

        /// <summary>
        /// POST /api/expense/categories - Tạo nhóm chi phí mới.
        /// </summary>
        [HttpPost("categories")]
        [HasPermission(PermissionConstants.EXPENSE_CATEGORY_MANAGE)]
        public async Task<IActionResult> CreateExpenseCategory([FromBody] ExpenseCategoryUpsertDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không xác định được định danh người dùng."
                    });
                }

                if (string.IsNullOrWhiteSpace(dto.CategoryName))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Tên nhóm chi phí là bắt buộc."
                    });
                }

                var trimmedName = dto.CategoryName.Trim();

                // Kiểm tra trùng tên
                var existing = await _context.ExpenseCategories
                    .FirstOrDefaultAsync(c => c.CategoryName.ToLower() == trimmedName.ToLower());

                if (existing != null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Tên nhóm chi phí đã tồn tại.",
                        Errors = new() { $"Danh mục '{trimmedName}' đã tồn tại." }
                    });
                }

                var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                var category = new ExpenseCategory
                {
                    CategoryId = Guid.NewGuid(),
                    CategoryName = trimmedName,
                    IsActive = 1,
                    CreatedBy = currentUserId,
                    CreatedAt = currentTime
                };

                _context.ExpenseCategories.Add(category);
                await _context.SaveChangesAsync();

                // Ghi log
                await TryWriteSystemLog(
                    "CREATE_EXPENSE_CATEGORY",
                    "ExpenseCategory",
                    category.CategoryId,
                    $"Tạo nhóm chi phí: {category.CategoryName}"
                );

                return Ok(new ApiResponse<ExpenseCategoryDto>
                {
                    Success = true,
                    Message = "Tạo nhóm chi phí thành công.",
                    Data = new ExpenseCategoryDto
                    {
                        CategoryId = category.CategoryId,
                        CategoryName = category.CategoryName,
                        IsActive = category.IsActive,
                        VoucherCount = 0,
                        PendingCount = 0
                    }
                });
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

        /// <summary>
        /// PUT /api/expense/categories/{id} - Sửa tên nhóm chi phí.
        /// </summary>
        [HttpPut("categories/{id:guid}")]
        [HasPermission(PermissionConstants.EXPENSE_CATEGORY_MANAGE)]
        public async Task<IActionResult> UpdateExpenseCategory(Guid id, [FromBody] ExpenseCategoryUpsertDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không xác định được định danh người dùng."
                    });
                }

                if (string.IsNullOrWhiteSpace(dto.CategoryName))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Tên nhóm chi phí là bắt buộc."
                    });
                }

                var trimmedName = dto.CategoryName.Trim();

                var category = await _context.ExpenseCategories
                    .Include(c => c.ExpenseVouchers)
                    .FirstOrDefaultAsync(c => c.CategoryId == id);

                if (category == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy nhóm chi phí."
                    });
                }

                // Kiểm tra trùng tên (trừ chính nó)
                var duplicate = await _context.ExpenseCategories
                    .FirstOrDefaultAsync(c => c.CategoryId != id &&
                        c.CategoryName.ToLower() == trimmedName.ToLower());

                if (duplicate != null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Tên nhóm chi phí đã tồn tại."
                    });
                }

                var oldName = category.CategoryName;
                category.CategoryName = trimmedName;

                await _context.SaveChangesAsync();

                // Ghi log
                await TryWriteSystemLog(
                    "UPDATE_EXPENSE_CATEGORY",
                    "ExpenseCategory",
                    category.CategoryId,
                    $"Đổi tên nhóm chi phí: '{oldName}' → '{category.CategoryName}'"
                );

                return Ok(new ApiResponse<ExpenseCategoryDto>
                {
                    Success = true,
                    Message = $"Đổi tên nhóm chi phí '{oldName}' thành '{category.CategoryName}' thành công.",
                    Data = new ExpenseCategoryDto
                    {
                        CategoryId = category.CategoryId,
                        CategoryName = category.CategoryName,
                        IsActive = category.IsActive,
                        VoucherCount = category.ExpenseVouchers.Count,
                        PendingCount = category.ExpenseVouchers.Count(ev => ev.Status == "PENDING")
                    }
                });
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

        /// <summary>
        /// DELETE /api/expense/categories/{id} - Xóa nhóm chi phí (chỉ khi không còn phiếu PENDING).
        /// </summary>
        [HttpDelete("categories/{id:guid}")]
        [HasPermission(PermissionConstants.EXPENSE_CATEGORY_MANAGE)]
        public async Task<IActionResult> DeleteExpenseCategory(Guid id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không xác định được định danh người dùng."
                    });
                }

                var category = await _context.ExpenseCategories
                    .Include(c => c.ExpenseVouchers)
                    .FirstOrDefaultAsync(c => c.CategoryId == id);

                if (category == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy nhóm chi phí."
                    });
                }

                // Kiểm tra có phiếu PENDING nào đang dùng không
                var pendingVouchers = category.ExpenseVouchers
                    .Where(ev => ev.Status == "PENDING")
                    .ToList();

                if (pendingVouchers.Any())
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không thể xóa nhóm chi phí đang có phiếu PENDING.",
                        Errors = new()
                        {
                            $"Có {pendingVouchers.Count} phiếu chi tiền đang ở trạng thái PENDING sử dụng nhóm này.",
                            "Vui lòng hủy hoặc xác nhận các phiếu PENDING trước khi xóa nhóm chi phí."
                        }
                    });
                }

                var categoryName = category.CategoryName;
                var totalVouchers = category.ExpenseVouchers.Count;

                // Xóa mềm: set IsActive = 0
                category.IsActive = 0;

                await _context.SaveChangesAsync();

                // Ghi log
                await TryWriteSystemLog(
                    "DELETE_EXPENSE_CATEGORY",
                    "ExpenseCategory",
                    category.CategoryId,
                    $"Xóa nhóm chi phí: {categoryName} (có {totalVouchers} phiếu)"
                );

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Xóa nhóm chi phí '{categoryName}' thành công. (Có {totalVouchers} phiếu chi tiền được giữ lại)"
                });
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

        #endregion

        #region Helper Methods

        /// <summary>
        /// Sinh mã phiếu chi: PXT-001, PXT-002...
        /// Reset theo ngày hoặc tăng toàn cục - tùy theo yêu cầu.
        /// Ở đây dùng simple auto-increment toàn cục.
        /// </summary>
        private async Task<string> GenerateVoucherCode(Guid branchId, DateTime currentTime)
        {
            var prefix = "PXT";
            var today = currentTime.ToString("yyyyMMdd");

            for (int attempt = 1; attempt <= 5; attempt++)
            {
                var todayCount = await _context.ExpenseVouchers
                    .Where(ev => ev.BranchId == branchId &&
                        ev.CreatedAt != null &&
                        ev.CreatedAt.Value.Date == currentTime.Date)
                    .CountAsync();

                var nextNumber = todayCount + attempt;
                var code = $"{prefix}-{nextNumber:000000}";

                var exists = await _context.ExpenseVouchers
                    .AnyAsync(ev => ev.VoucherCode == code);
                if (!exists) return code;
            }

            return $"{prefix}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        }

        private IQueryable<ExpenseVoucher> ApplyVoucherSorting(
            IQueryable<ExpenseVoucher> query, string? sortBy, string? order)
        {
            bool isDesc = order?.ToLower() == "desc";
            string sort = sortBy?.ToLower() ?? "createdat";

            return sort switch
            {
                "code" => isDesc
                    ? query.OrderByDescending(ev => ev.VoucherCode)
                    : query.OrderBy(ev => ev.VoucherCode),
                "amount" => isDesc
                    ? query.OrderByDescending(ev => ev.Amount)
                    : query.OrderBy(ev => ev.Amount),
                "status" => isDesc
                    ? query.OrderByDescending(ev => ev.Status)
                    : query.OrderBy(ev => ev.Status),
                "createdat" => isDesc
                    ? query.OrderByDescending(ev => ev.CreatedAt)
                    : query.OrderBy(ev => ev.CreatedAt),
                "category" => isDesc
                    ? query.OrderByDescending(ev => ev.Category.CategoryName)
                    : query.OrderBy(ev => ev.Category.CategoryName),
                _ => query.OrderByDescending(ev => ev.CreatedAt)
            };
        }

        /// <summary>
        /// Map Entity → Response DTO, tính CanConfirm/CanCancel dựa trên permission.
        /// </summary>
        private ExpenseVoucherResponseDto MapToExpenseVoucherResponse(ExpenseVoucher ev)
        {
            bool canConfirm = false;
            bool canCancel = false;

            if (ev.Status == "PENDING")
            {
                // Owner luôn được confirm/cancel
                bool isOwner = HasGlobalAccess();

                // Hoặc có permission tương ứng
                bool hasConfirmPermission = User.HasClaim(
                    c => c.Type == PermissionConstants.PERMISSION_CLAIM_TYPE
                      && c.Value == PermissionConstants.EXPENSE_CONFIRM);

                bool hasCancelPermission = User.HasClaim(
                    c => c.Type == PermissionConstants.PERMISSION_CLAIM_TYPE
                      && c.Value == PermissionConstants.EXPENSE_CANCEL);

                canConfirm = isOwner || hasConfirmPermission;
                canCancel = isOwner || hasCancelPermission;
            }

            return new ExpenseVoucherResponseDto
            {
                VoucherId = ev.VoucherId,
                VoucherCode = ev.VoucherCode,
                BranchId = ev.BranchId,
                BranchName = ev.Branch?.BranchName,
                CategoryId = ev.CategoryId,
                CategoryName = ev.Category?.CategoryName ?? "N/A",
                SupplierId = ev.SupplierId,
                SupplierName = ev.Supplier?.SupplierName,
                Amount = ev.Amount,
                Reason = ev.Reason,
                Note = ev.Note,
                Status = ev.Status,
                CreatedBy = ev.CreatedBy,
                CreatedByName = ev.CreatedByUser?.FullName ?? "Hệ thống",
                CreatedAt = ev.CreatedAt,
                ConfirmedBy = ev.ConfirmedBy,
                ConfirmedByName = ev.ConfirmedByUser?.FullName,
                CompletedAt = ev.CompletedAt,
                CancelledBy = ev.CancelledBy,
                CancelledByName = ev.CancelledByUser?.FullName,
                CancelledAt = ev.CancelledAt,
                CancelReason = ev.CancelReason,
                CanConfirm = canConfirm,
                CanCancel = canCancel
            };
        }

        #endregion
    }
}
