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

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Xuất nhập kho (Inventory)")]
    public class OutwardInventoryController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly ICurrentBranchAccessor _currentBranch;

        public OutwardInventoryController(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch)
        {
            _context = context;
            _currentBranch = currentBranch;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
        }

        /// <summary>
        /// Kiểm tra xem User hiện tại có quyền xem/thao tác toàn hệ thống (tất cả chi nhánh) hay không.
        /// Thường là các Role: Owner, Admin, TechSupport,... hoặc có Quyền (Permission) cụ thể.
        /// </summary>
        private bool HasGlobalAccess()
        {
            // RBAC cách 1: Check theo vai trò quản trị tối cao
            return User.IsInRole("Owner") || User.IsInRole("Admin");

            // RBAC cách 2 (Chuẩn nhất): Check theo claim Permission nếu hệ thống của bạn lưu Permission trong JWT
            // return User.HasClaim(c => c.Type == "Permission" && c.Value == "Inventory.Outward.ViewAll");
        }

        private static string GetTicketTypeName(OutwardInventoryType type)
        {
            return type switch
            {
                OutwardInventoryType.ReturnToSupplier => "RETURN_SUPPLIER",
                OutwardInventoryType.DamagedHaoHut => "WRITE_OFF",
                OutwardInventoryType.InternalUse => "TRANSFER",
                _ => "WRITE_OFF"
            };
        }

        // ============================================================
        // GET /api/outwardinventory
        // Quyền tối thiểu: Phải có vai trò Nhân viên (Staff) trở lên hoặc có quyền xem.
        // Tầm vực dữ liệu: GlobalAccess (Owner/Admin) xem tất cả; Staff chỉ xem chi nhánh được phân quyền.
        // ============================================================
        [HttpGet]
        [HasPermission(PermissionConstants.STOCK_VIEW)]
        public async Task<IActionResult> GetOutwardInventories(
            [FromQuery] string? status,
            [FromQuery] string? ticketType,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] Guid? branchId = null)
        {
            try
            {
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var query = _context.StockTickets
                    .Include(st => st.User)
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                            .ThenInclude(bp => bp.Product)
                    .Where(st => st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER")
                    .AsQueryable();

                // Áp dụng Phân quyền RBAC về phạm vi dữ liệu (Data Scope)
                if (HasGlobalAccess())
                {
                    // Người dùng cấp cao: có quyền xem tất cả, hoặc lọc theo chi nhánh bất kỳ nếu truyền lên
                    if (branchId.HasValue)
                        query = query.Where(st => st.BranchId == branchId.Value);
                }
                else
                {
                    // Người dùng cấp cơ sở (Staff/WarehouseKeeper): Bị giới hạn nghiêm ngặt theo Chi nhánh trong JWT
                    var staffBranchId = _currentBranch.GetBranchId();
                    if (staffBranchId == null)
                    {
                        return Unauthorized(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Tài khoản của bạn chưa được cấu hình chi nhánh hợp lệ.",
                            Errors = new() { "Branch context is missing in token." }
                        });
                    }
                    query = query.Where(st => st.BranchId == staffBranchId.Value);
                }

                // Các bộ lọc dữ liệu thông thường
                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(st => st.Status != null && st.Status.ToLower() == status.Trim().ToLower());

                if (!string.IsNullOrWhiteSpace(ticketType))
                    query = query.Where(st => st.TicketType != null && st.TicketType.ToLower() == ticketType.Trim().ToLower());

                if (fromDate.HasValue)
                    query = query.Where(st => st.CreatedAt >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(st => st.CreatedAt <= toDate.Value.AddDays(1));

                query = query.OrderByDescending(st => st.CreatedAt);

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return Ok(new ApiResponse<PaginatedResponse<OutwardInventoryResponseDto>>
                {
                    Success = true,
                    Message = "Lấy lịch sử xuất kho thành công.",
                    Data = new PaginatedResponse<OutwardInventoryResponseDto>
                    {
                        Items = items.Select(MapToOutwardInventoryResponse).ToList(),
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
                    Message = "Lỗi khi lấy dữ liệu xuất kho.",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // GET /api/outwardinventory/{id}
        // ============================================================
        [HttpGet("{id:guid}")]
        [HasPermission(PermissionConstants.STOCK_VIEW)]
        public async Task<IActionResult> GetOutwardInventoryById(Guid id)
        {
            try
            {
                var stockTicketQuery = _context.StockTickets
                    .Include(st => st.User)
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                            .ThenInclude(bp => bp.Product)
                    .Where(st => st.StockTicketId == id &&
                        (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER"));

                // Kiểm tra quyền hạn phạm vi dữ liệu
                if (!HasGlobalAccess())
                {
                    var staffBranchId = _currentBranch.GetBranchId();
                    if (staffBranchId == null)
                    {
                        return Unauthorized(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "JWT token không hợp lệ hoặc thiếu thông tin chi nhánh.",
                            Errors = new() { "Authorization header required" }
                        });
                    }
                    stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == staffBranchId.Value);
                }

                var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

                if (stockTicket == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy phiếu xuất kho hoặc bạn không có quyền xem phiếu này.",
                        Errors = new() { $"No outward inventory with ID: {id}" }
                    });
                }

                return Ok(new ApiResponse<OutwardInventoryResponseDto>
                {
                    Success = true,
                    Message = "Lấy chi tiết phiếu xuất kho thành công.",
                    Data = MapToOutwardInventoryResponse(stockTicket)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi hệ thống khi lấy chi tiết phiếu.",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // POST /api/outwardinventory
        // Quyền: Cần quyền tạo mới phiếu xuất kho
        // ============================================================
        [HttpPost]
        // [Authorize(Policy = "Inventory.Outward.Create")]
        public async Task<IActionResult> CreateOutwardInventory(
            [FromBody] OutwardInventoryCreateDto dto,
            [FromQuery] Guid? branchId = null)
        {
            try
            {
                // RBAC logic giải quyết Chi nhánh thao tác: 
                // Nếu là GlobalAccess được phép truyền chi nhánh khác, Staff luôn bị ép dùng chi nhánh của mình.
                Guid? resolvedBranchId = HasGlobalAccess() ? (branchId ?? _currentBranch.GetBranchId()) : _currentBranch.GetBranchId();

                if (resolvedBranchId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không xác định được chi nhánh để tạo phiếu.",
                        Errors = new() { "Branch context required." }
                    });
                }

                if (dto.Items == null || dto.Items.Count == 0)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ",
                        Errors = new() { "Yêu cầu có ít nhất 1 mặt hàng để xuất kho." }
                    });
                }

                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không xác định được định danh người dùng từ Token.",
                        Errors = new() { "User identity missing." }
                    });
                }

                var branchProductIds = dto.Items.Select(i => i.BranchProductId).Distinct().ToList();
                var branchProducts = await _context.BranchProducts
                    .Where(bp => bp.BranchId == resolvedBranchId.Value && branchProductIds.Contains(bp.BranchProductId))
                    .Include(bp => bp.Product)
                    .ToListAsync();

                if (branchProducts.Count != branchProductIds.Count)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Yêu cầu không hợp lệ",
                        Errors = new() { "Một hoặc nhiều sản phẩm không thuộc về chi nhánh đang chọn." }
                    });
                }

                // Kiểm tra số lượng và tồn kho khả dụng
                var validationErrors = new List<string>();
                foreach (var item in dto.Items)
                {
                    if (item.Quantity <= 0)
                    {
                        validationErrors.Add($"Số lượng xuất của sản phẩm phải lớn hơn 0.");
                        continue;
                    }

                    var bp = branchProducts.FirstOrDefault(x => x.BranchProductId == item.BranchProductId);
                    if (bp != null && item.Quantity > bp.AvailableStock)
                    {
                        validationErrors.Add($"Sản phẩm '{bp.Product?.ProductName ?? item.BranchProductId.ToString()}' không đủ số lượng tồn kho khả dụng để xuất.");
                    }
                }

                if (validationErrors.Any())
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Kiểm tra tồn kho không đạt yêu cầu.",
                        Errors = validationErrors
                    });
                }

                var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                var ticketCode = GenerateTicketCode(dto.OutwardType, currentTime);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var stockTicket = new StockTicket
                    {
                        StockTicketId = Guid.NewGuid(),
                        TicketCode = ticketCode,
                        BranchId = resolvedBranchId.Value,
                        UserId = currentUserId.Value,
                        TicketType = GetTicketTypeName(dto.OutwardType),
                        Status = "COMPLETED",
                        Notes = !string.IsNullOrWhiteSpace(dto.Note) ? dto.Note : dto.Reason,
                        CreatedAt = currentTime
                    };

                    _context.StockTickets.Add(stockTicket);

                    foreach (var item in dto.Items)
                    {
                        var bp = branchProducts.First(x => x.BranchProductId == item.BranchProductId);

                        var systemQtyBefore = bp.ActualStock;
                        bp.ActualStock -= item.Quantity;
                        bp.AvailableStock -= item.Quantity;

                        var ticketItem = new StockTicketItem
                        {
                            TicketItemId = Guid.NewGuid(),
                            StockTicketId = stockTicket.StockTicketId,
                            BranchProductId = bp.BranchProductId,
                            SystemQuantity = systemQtyBefore,           // Tồn trước khi xuất
                            ActualQuantity = bp.ActualStock,            // Tồn sau khi xuất
                            Discrepancy = item.Quantity                 // Số lượng xuất đi
                        };

                        _context.StockTicketItems.Add(ticketItem);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Tạo phiếu xuất kho thành công.",
                        Data = new { TicketId = stockTicket.StockTicketId, TicketCode = stockTicket.TicketCode }
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Gặp lỗi khi tạo phiếu xuất kho.",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // PUT /api/outwardinventory/{id}
        // ============================================================
        [HttpPut("{id:guid}")]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_UPDATE)]
        public async Task<IActionResult> UpdateOutwardInventory(Guid id, [FromBody] OutwardInventoryUpdateDto dto)
        {
            try
            {
                var stockTicketQuery = _context.StockTickets
                    .Where(st => st.StockTicketId == id &&
                        (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER"));

                if (!HasGlobalAccess())
                {
                    var staffBranchId = _currentBranch.GetBranchId();
                    if (staffBranchId == null)
                    {
                        return Unauthorized(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Không có quyền thực hiện hành động này.",
                            Errors = new() { "Branch context missing" }
                        });
                    }
                    stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == staffBranchId.Value);
                }

                var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

                if (stockTicket == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy phiếu cần cập nhật hoặc bạn không có quyền sửa đổi phiếu này.",
                        Errors = new() { $"No outward inventory with ID: {id}" }
                    });
                }

                if (!string.IsNullOrWhiteSpace(dto.Reason))
                    stockTicket.Notes = dto.Reason;

                if (!string.IsNullOrWhiteSpace(dto.Status))
                {
                    var normalized = dto.Status.Trim().ToUpperInvariant();
                    if (normalized is "PENDING" or "COMPLETED" or "CANCELLED")
                        stockTicket.Status = normalized;
                }

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<OutwardInventoryResponseDto>
                {
                    Success = true,
                    Message = "Cập nhật thông tin phiếu thành công.",
                    Data = MapToOutwardInventoryResponse(stockTicket)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi cập nhật phiếu xuất kho.",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // DELETE /api/outwardinventory/{id}  (Soft cancel)
        // ============================================================
        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_DELETE)]
        public async Task<IActionResult> DeleteOutwardInventory(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var stockTicketQuery = _context.StockTickets
                    .Include(st => st.StockTicketItems)
                    .Where(st => st.StockTicketId == id &&
                        (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER"));

                if (!HasGlobalAccess())
                {
                    var staffBranchId = _currentBranch.GetBranchId();
                    if (staffBranchId == null)
                    {
                        return Unauthorized(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Xác thực không hợp lệ.",
                            Errors = new() { "Branch id missing" }
                        });
                    }
                    stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == staffBranchId.Value);
                }

                var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

                if (stockTicket == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy phiếu xuất kho để hủy hoặc bạn không có quyền hạn.",
                        Errors = new() { $"No outward inventory with ID: {id}" }
                    });
                }

                if (stockTicket.Status == "CANCELLED")
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Phiếu này đã được hủy trước đó."
                    });
                }

                if (stockTicket.Status == "COMPLETED")
                {
                    foreach (var item in stockTicket.StockTicketItems)
                    {
                        var branchProduct = await _context.BranchProducts
                            .FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId);

                        if (branchProduct != null)
                        {
                            // Hoàn lại số lượng đã trừ vào kho khi hủy phiếu thành công
                            branchProduct.ActualStock += item.Discrepancy;
                            branchProduct.AvailableStock += item.Discrepancy;
                        }
                    }
                }

                stockTicket.Status = "CANCELLED";
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Hủy phiếu xuất kho và hoàn lại số lượng sản phẩm thành công."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi thực hiện hủy phiếu xuất kho.",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ========== HELPER METHODS ==========

        private static string GenerateTicketCode(OutwardInventoryType type, DateTime currentTime)
        {
            var typePrefix = type switch
            {
                OutwardInventoryType.ReturnToSupplier => "RTS",
                OutwardInventoryType.DamagedHaoHut => "DAM",
                OutwardInventoryType.InternalUse => "INT",
                _ => "OUT"
            };
            return $"{typePrefix}-{currentTime:yyyyMMddHHmmss}";
        }

        private OutwardInventoryResponseDto MapToOutwardInventoryResponse(StockTicket stockTicket)
        {
            return new OutwardInventoryResponseDto
            {
                StockTicketId = stockTicket.StockTicketId,
                TicketCode = stockTicket.TicketCode ?? string.Empty,
                TicketType = stockTicket.TicketType ?? string.Empty,
                Status = stockTicket.Status ?? string.Empty,
                BranchId = stockTicket.BranchId,
                UserId = stockTicket.UserId,
                UserName = stockTicket.User?.FullName ?? stockTicket.User?.Username,
                Reason = stockTicket.Notes,
                CreatedAt = stockTicket.CreatedAt,
                Items = stockTicket.StockTicketItems?.Select(item => new OutwardInventoryItemResponseDto
                {
                    TicketItemId = item.TicketItemId,
                    BranchProductId = item.BranchProductId,
                    ProductId = item.BranchProduct?.ProductId ?? Guid.Empty,
                    ProductCode = item.BranchProduct?.Product?.ProductCode ?? string.Empty,
                    ProductName = item.BranchProduct?.Product?.ProductName ?? string.Empty,
                    SystemQuantity = item.SystemQuantity,
                    Quantity = item.Discrepancy,
                    ActualQuantity = item.ActualQuantity
                }).ToList() ?? new()
            };
        }
    }
}