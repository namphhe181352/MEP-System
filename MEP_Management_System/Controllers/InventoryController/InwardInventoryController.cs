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
    public class InwardInventoryController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly ICurrentBranchAccessor _currentBranch;

        public InwardInventoryController(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch)
        {
            _context = context;
            _currentBranch = currentBranch;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
        }

        private bool IsOwner() => User.IsInRole("Owner");

        private static string GetTicketTypeName(InwardInventoryType type)
        {
            return type switch
            {
                InwardInventoryType.Purchase => "PURCHASE",
                InwardInventoryType.CustomerReturn => "CUSTOMER_RETURN",
                InwardInventoryType.BalanceAdjust => "BALANCE_ADJUST",
                _ => "PURCHASE"
            };
        }

        // ============================================================
        // GET /api/inwardinventory
        // Mặc định: CHỈ hiển thị PENDING + COMPLETED
        // Query ?status=CANCELLED hoặc ?status=ALL để xem thêm CANCELLED
        // ============================================================
        [HttpGet]
        [HasPermission(PermissionConstants.STOCK_VIEW)]
        public async Task<IActionResult> GetInwardInventories(
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
                    .Where(st => st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST")
                    .AsQueryable();

                if (IsOwner())
                {
                    if (branchId.HasValue)
                        query = query.Where(st => st.BranchId == branchId.Value);
                }
                else
                {
                    var staffBranchId = _currentBranch.GetBranchId();
                    if (staffBranchId == null)
                    {
                        return Unauthorized(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "JWT token is missing or invalid",
                            Errors = new() { "Authorization header required" }
                        });
                    }
                    query = query.Where(st => st.BranchId == staffBranchId.Value);
                }

                // ============================================================
                // QUY TẮC MỚI: Mặc định chỉ hiển thị PENDING + COMPLETED
                // Chỉ khi truyền tường minh status=CANCELLED hoặc status=ALL mới hiển thị CANCELLED
                // ============================================================
                if (!string.IsNullOrWhiteSpace(status))
                {
                    var normalizedStatus = status.Trim().ToUpperInvariant();
                    if (normalizedStatus == "ALL")
                    {
                        // Hiển thị tất cả trạng thái
                    }
                    else if (normalizedStatus == "CANCELLED")
                    {
                        query = query.Where(st => st.Status != null && st.Status.ToUpper() == "CANCELLED");
                    }
                    else
                    {
                        query = query.Where(st => st.Status != null && st.Status.ToUpper() == normalizedStatus);
                    }
                }
                else
                {
                    // Mặc định: Ẩn CANCELLED để giao diện sạch sẽ
                    query = query.Where(st => st.Status == null || (st.Status != "CANCELLED"));
                }

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

                return Ok(new ApiResponse<PaginatedResponse<InwardInventoryResponseDto>>
                {
                    Success = true,
                    Message = "Inward inventory history retrieved successfully",
                    Data = new PaginatedResponse<InwardInventoryResponseDto>
                    {
                        Items = items.Select(MapToInwardInventoryResponse).ToList(),
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
                    Message = "Error retrieving inward inventory history",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // GET /api/inwardinventory/{id}
        // ============================================================
        [HttpGet("{id:guid}")]
        [HasPermission(PermissionConstants.STOCK_VIEW)]
        public async Task<IActionResult> GetInwardInventoryById(Guid id)
        {
            try
            {
                var stockTicketQuery = _context.StockTickets
                    .Include(st => st.User)
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                            .ThenInclude(bp => bp.Product)
                    .Where(st => st.StockTicketId == id &&
                        (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST"));

                if (!IsOwner())
                {
                    var staffBranchId = _currentBranch.GetBranchId();
                    if (staffBranchId == null)
                    {
                        return Unauthorized(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "JWT token is missing or invalid",
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
                        Message = "Inward inventory not found",
                        Errors = new() { $"No inward inventory with ID: {id}" }
                    });
                }

                return Ok(new ApiResponse<InwardInventoryResponseDto>
                {
                    Success = true,
                    Message = "Inward inventory retrieved successfully",
                    Data = MapToInwardInventoryResponse(stockTicket)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error retrieving inward inventory",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // POST /api/inwardinventory
        // Tạo phiếu mới với Status = PENDING (chưa cập nhật kho)
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> CreateInwardInventory(
            [FromBody] InwardInventoryCreateDto dto,
            [FromQuery] Guid? branchId = null)
        {
            // Resolve branchId
            var resolvedBranchId = branchId ?? _currentBranch.GetBranchId();
            if (resolvedBranchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "JWT token is missing or invalid",
                    Errors = new() { "Authorization header required" }
                });
            }

            if (dto.Items == null || dto.Items.Count == 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Validation failed",
                    Errors = new() { "At least one item is required." }
                });
            }

            var itemValidationErrors = new List<string>();
            foreach (var item in dto.Items)
            {
                if (item.Quantity <= 0)
                    itemValidationErrors.Add($"Quantity must be greater than 0 for Id {item.Id}.");
            }

            if (itemValidationErrors.Any())
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Validation failed",
                    Errors = itemValidationErrors
                });
            }

            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "JWT token is missing or invalid",
                    Errors = new() { "User identity is missing from token." }
                });
            }

            var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            var ticketCode = GenerateTicketCode(dto.InwardType, currentTime);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ============================================================
                // QUY TẮC MỚI: Tạo phiếu với Status = PENDING
                // KHÔNG cập nhật tồn kho tại đây
                // ============================================================
                var stockTicket = new StockTicket
                {
                    StockTicketId = Guid.NewGuid(),
                    TicketCode = ticketCode,
                    BranchId = resolvedBranchId.Value,
                    UserId = currentUserId.Value,
                    TicketType = GetTicketTypeName(dto.InwardType),
                    Status = "PENDING", // THAY ĐỔI: Từ COMPLETED -> PENDING
                    Notes = dto.Note ?? dto.Reason,
                    CreatedAt = currentTime
                    // TODO: CompletedAt = null
                    // TODO: CancelledAt = null
                    // TODO: ConfirmedBy = null
                    // TODO: CancelledBy = null
                    // TODO: CancelReason = null
                };

                _context.StockTickets.Add(stockTicket);

                foreach (var item in dto.Items)
                {
                    // Tìm BranchProduct
                    var branchProduct = await _context.BranchProducts
                        .FirstOrDefaultAsync(bp =>
                            (bp.BranchProductId == item.Id || bp.ProductId == item.Id)
                            && bp.BranchId == resolvedBranchId.Value);

                    Guid actualBranchProductId;

                    if (branchProduct == null)
                    {
                        // Sản phẩm chưa có ở chi nhánh này -> tạo mới
                        var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == item.Id);
                        if (product == null)
                        {
                            throw new InvalidOperationException($"Product/BranchProduct with ID {item.Id} was not found in system.");
                        }

                        branchProduct = new BranchProduct
                        {
                            BranchProductId = Guid.NewGuid(),
                            BranchId = resolvedBranchId.Value,
                            ProductId = product.ProductId,
                            CostPrice = item.CostPrice > 0 ? item.CostPrice : 0,
                            SellPrice = 0,
                            ActualStock = 0,
                            ReservedStock = 0,
                            AvailableStock = 0,
                            IsActive = 1,
                            DirectSale = 1
                        };

                        _context.BranchProducts.Add(branchProduct);
                        await _context.SaveChangesAsync();
                        actualBranchProductId = branchProduct.BranchProductId;
                    }
                    else
                    {
                        actualBranchProductId = branchProduct.BranchProductId;
                        if (item.CostPrice > 0)
                        {
                            branchProduct.CostPrice = item.CostPrice;
                        }
                    }

                    // Tạo ticket item với số lượng tồn hiện tại (chưa cộng thêm)
                    var ticketItem = new StockTicketItem
                    {
                        TicketItemId = Guid.NewGuid(),
                        StockTicketId = stockTicket.StockTicketId,
                        BranchProductId = actualBranchProductId,
                        SystemQuantity = branchProduct.ActualStock, // Tồn trước khi nhập
                        ActualQuantity = branchProduct.ActualStock, // Tồn sau khi nhập (chưa thay đổi vì PENDING)
                        Discrepancy = item.Quantity
                    };

                    _context.StockTicketItems.Add(ticketItem);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Inward inventory created successfully. Please confirm to update stock.",
                    Data = new
                    {
                        TicketId = stockTicket.StockTicketId,
                        TicketCode = stockTicket.TicketCode,
                        TicketType = stockTicket.TicketType,
                        BranchId = stockTicket.BranchId,
                        Status = stockTicket.Status
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error creating inward inventory",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // PUT /api/inwardinventory/{id}
        // Chỉ cho phép sửa khi Status = PENDING
        // ============================================================
        [HttpPut("{id:guid}")]
        [HasPermission(PermissionConstants.STOCK_INWARD_UPDATE)]
        public async Task<IActionResult> UpdateInwardInventory(Guid id, [FromBody] InwardInventoryUpdateDto dto)
        {
            try
            {
                var stockTicketQuery = _context.StockTickets
                    .Include(st => st.StockTicketItems)
                    .Where(st => st.StockTicketId == id &&
                        (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST"));

                if (!IsOwner())
                {
                    var staffBranchId = _currentBranch.GetBranchId();
                    if (staffBranchId == null)
                    {
                        return Unauthorized(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "JWT token is missing or invalid",
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
                        Message = "Inward inventory not found",
                        Errors = new() { $"No inward inventory with ID: {id}" }
                    });
                }

                // ============================================================
                // QUY TẮC MỚI: Kiểm tra trạng thái trước khi sửa
                // ============================================================
                if (stockTicket.Status == "CANCELLED")
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Cannot update cancelled ticket",
                        Errors = new() { "Phiếu đã bị hủy. Không thể chỉnh sửa." }
                    });
                }

                if (stockTicket.Status == "COMPLETED")
                {
                    // COMPLETED: CHỈ cho phép sửa Note/Reason, không cho sửa Items
                    if (!string.IsNullOrWhiteSpace(dto.Note))
                        stockTicket.Notes = dto.Note;
                    else if (!string.IsNullOrWhiteSpace(dto.Reason))
                        stockTicket.Notes = dto.Reason;

                    await _context.SaveChangesAsync();

                    return Ok(new ApiResponse<InwardInventoryResponseDto>
                    {
                        Success = true,
                        Message = "Inward inventory updated (notes only for completed tickets)",
                        Data = MapToInwardInventoryResponse(stockTicket)
                    });
                }

                // PENDING: Cho phép sửa tất cả (Items, Note, Reason, Status)
                if (!string.IsNullOrWhiteSpace(dto.Note))
                    stockTicket.Notes = dto.Note;
                else if (!string.IsNullOrWhiteSpace(dto.Reason))
                    stockTicket.Notes = dto.Reason;

                if (!string.IsNullOrWhiteSpace(dto.Status))
                {
                    var normalized = dto.Status.Trim().ToUpperInvariant();
                    if (normalized is "PENDING" or "COMPLETED" or "CANCELLED")
                        stockTicket.Status = normalized;
                }

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<InwardInventoryResponseDto>
                {
                    Success = true,
                    Message = "Inward inventory updated successfully",
                    Data = MapToInwardInventoryResponse(stockTicket)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error updating inward inventory",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // POST /api/inwardinventory/{id}/confirm
        // Xác nhận phiếu nhập kho - Cập nhật tồn kho và đổi sang COMPLETED
        // ============================================================
        [HttpPost("{id:guid}/confirm")]
        [HasPermission(PermissionConstants.STOCK_INWARD_UPDATE)]
        public async Task<IActionResult> ConfirmInwardInventory(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var stockTicketQuery = _context.StockTickets
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                    .Where(st => st.StockTicketId == id &&
                        (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST"));

                if (!IsOwner())
                {
                    var staffBranchId = _currentBranch.GetBranchId();
                    if (staffBranchId == null)
                    {
                        return Unauthorized(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "JWT token is missing or invalid",
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
                        Message = "Inward inventory not found",
                        Errors = new() { $"No inward inventory with ID: {id}" }
                    });
                }

                // ============================================================
                // QUY TẮC MỚI: Kiểm tra trạng thái
                // ============================================================
                if (stockTicket.Status == "COMPLETED")
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Ticket already confirmed",
                        Errors = new() { "Phiếu đã được xác nhận trước đó." }
                    });
                }

                if (stockTicket.Status == "CANCELLED")
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Cannot confirm cancelled ticket",
                        Errors = new() { "Phiếu đã bị hủy. Không thể xác nhận." }
                    });
                }

                // Kiểm tra phiếu có Items không
                if (stockTicket.StockTicketItems == null || !stockTicket.StockTicketItems.Any())
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Cannot confirm ticket without items",
                        Errors = new() { "Phiếu không có sản phẩm nào. Vui lòng thêm sản phẩm trước khi xác nhận." }
                    });
                }

                // ============================================================
                // QUY TẮC MỚI: Cập nhật tồn kho (INWARD: cộng kho)
                // ============================================================
                var currentUserId = GetCurrentUserId();

                foreach (var item in stockTicket.StockTicketItems)
                {
                    var branchProduct = await _context.BranchProducts
                        .FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId);

                    if (branchProduct != null)
                    {
                        // Cập nhật tồn kho - Nhập kho thì CỘNG
                        branchProduct.ActualStock += item.Discrepancy;
                        branchProduct.AvailableStock += item.Discrepancy;

                        // Cập nhật ActualQuantity trong ticket item (tồn sau khi nhập)
                        item.ActualQuantity = branchProduct.ActualStock;
                        item.SystemQuantity = branchProduct.ActualStock - item.Discrepancy;
                    }
                }

                // Chuyển trạng thái sang COMPLETED
                stockTicket.Status = "COMPLETED";
                // TODO: stockTicket.CompletedAt = DateTime.UtcNow;
                // TODO: stockTicket.ConfirmedBy = currentUserId;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Inward inventory confirmed successfully. Stock updated.",
                    Data = new
                    {
                        TicketId = stockTicket.StockTicketId,
                        TicketCode = stockTicket.TicketCode,
                        Status = stockTicket.Status
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error confirming inward inventory",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // POST /api/inwardinventory/{id}/cancel
        // Hủy phiếu nhập kho - Xử lý rollback an toàn
        // ============================================================
        [HttpPost("{id:guid}/cancel")]
        [HasPermission(PermissionConstants.STOCK_INWARD_DELETE)]
        public async Task<IActionResult> CancelInwardInventory(Guid id, [FromBody] CancelTicketDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var stockTicketQuery = _context.StockTickets
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                    .Where(st => st.StockTicketId == id &&
                        (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST"));

                if (!IsOwner())
                {
                    var staffBranchId = _currentBranch.GetBranchId();
                    if (staffBranchId == null)
                    {
                        return Unauthorized(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "JWT token is missing or invalid",
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
                        Message = "Inward inventory not found",
                        Errors = new() { $"No inward inventory with ID: {id}" }
                    });
                }

                // ============================================================
                // QUY TẮC MỚI: Kiểm tra trạng thái
                // ============================================================
                if (stockTicket.Status == "CANCELLED")
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Ticket already cancelled",
                        Errors = new() { "Phiếu đã được hủy trước đó." }
                    });
                }

                var currentUserId = GetCurrentUserId();

                // ============================================================
                // QUY TẮC MỚI: Xử lý theo trạng thái hiện tại
                // ============================================================
                if (stockTicket.Status == "COMPLETED")
                {
                    // COMPLETED: Cần rollback tồn kho (trừ lại)
                    // Nhưng phải kiểm tra an toàn: không cho phép ActualStock < 0

                    var unsafeItems = new List<string>();

                    foreach (var item in stockTicket.StockTicketItems)
                    {
                        var branchProduct = await _context.BranchProducts
                            .FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId);

                        if (branchProduct != null)
                        {
                            // Kiểm tra: nếu trừ kho sẽ âm không?
                            if (branchProduct.ActualStock < item.Discrepancy ||
                                branchProduct.AvailableStock < item.Discrepancy)
                            {
                                unsafeItems.Add(branchProduct.Product?.ProductName ?? item.BranchProductId.ToString());
                            }
                        }
                    }

                    if (unsafeItems.Any())
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Cannot cancel - insufficient stock",
                            Errors = new List<string>
                            {
                                "Không thể hủy vì hàng đã được xuất khỏi kho.",
                                $"Sản phẩm bị ảnh hưởng: {string.Join(", ", unsafeItems)}"
                            }
                        });
                    }

                    // Đủ điều kiện - Thực hiện rollback
                    foreach (var item in stockTicket.StockTicketItems)
                    {
                        var branchProduct = await _context.BranchProducts
                            .FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId);

                        if (branchProduct != null)
                        {
                            branchProduct.ActualStock -= item.Discrepancy;
                            branchProduct.AvailableStock -= item.Discrepancy;
                        }
                    }
                }

                // PENDING hoặc COMPLETED (đã rollback): Đều chuyển sang CANCELLED
                stockTicket.Status = "CANCELLED";
                // TODO: stockTicket.CancelledAt = DateTime.UtcNow;
                // TODO: stockTicket.CancelledBy = currentUserId;
                // TODO: stockTicket.CancelReason = dto?.CancelReason;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = stockTicket.Status == "COMPLETED"
                        ? "Inward inventory cancelled and stock rolled back successfully"
                        : "Inward inventory cancelled successfully (pending ticket, no stock change)",
                    Data = new
                    {
                        TicketId = stockTicket.StockTicketId,
                        TicketCode = stockTicket.TicketCode,
                        Status = stockTicket.Status
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error cancelling inward inventory",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // DELETE /api/inwardinventory/{id} (Legacy - giữ lại cho tương thích)
        // Chuyển hướng sang /cancel
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
            return await CancelInwardInventory(id, cancelDto);
        }

        // ========== HELPER METHODS ==========

        private static string GenerateTicketCode(InwardInventoryType type, DateTime currentTime)
        {
            var typePrefix = type switch
            {
                InwardInventoryType.Purchase => "PUR-",
                InwardInventoryType.CustomerReturn => "CRN-",
                InwardInventoryType.BalanceAdjust => "BAL-",
                _ => "PUR-"
            };

            return $"{typePrefix}{currentTime:yyyyMMddHHmmss}";
        }

        private InwardInventoryResponseDto MapToInwardInventoryResponse(StockTicket stockTicket)
        {
            return new InwardInventoryResponseDto
            {
                StockTicketId = stockTicket.StockTicketId,
                TicketCode = stockTicket.TicketCode ?? string.Empty,
                TicketType = stockTicket.TicketType ?? string.Empty,
                Status = stockTicket.Status ?? string.Empty,
                BranchId = stockTicket.BranchId,
                UserId = stockTicket.UserId,
                UserName = stockTicket.User?.FullName ?? stockTicket.User?.Username,
                Reason = stockTicket.Notes,
                Note = stockTicket.Notes,
                CreatedAt = stockTicket.CreatedAt,
                // TODO: CompletedAt = stockTicket.CompletedAt,
                // TODO: CancelledAt = stockTicket.CancelledAt,
                // TODO: ConfirmedBy = stockTicket.ConfirmedBy,
                // TODO: CancelledBy = stockTicket.CancelledBy,
                // TODO: CancelReason = stockTicket.CancelReason,
                Items = stockTicket.StockTicketItems?.Select(item => new InwardInventoryItemResponseDto
                {
                    TicketItemId = item.TicketItemId,
                    BranchProductId = item.BranchProductId,
                    ProductId = item.BranchProduct?.ProductId ?? Guid.Empty,
                    ProductCode = item.BranchProduct?.Product?.ProductCode ?? string.Empty,
                    ProductName = item.BranchProduct?.Product?.ProductName ?? string.Empty,
                    SystemQuantity = item.SystemQuantity,
                    Quantity = item.Discrepancy,
                    ActualQuantity = item.ActualQuantity,
                    CostPrice = item.BranchProduct?.CostPrice ?? 0
                }).ToList() ?? new()
            };
        }
    }
}
