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
        // Owner: truyền ?branchId=guid để lọc theo chi nhánh cụ thể,
        //        hoặc không truyền để xem tất cả chi nhánh của mình.
        // Staff: chỉ xem được chi nhánh được gán trong JWT.
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
                    // Owner: lọc theo branchId nếu có truyền, không thì xem tất cả
                    if (branchId.HasValue)
                    {
                        query = query.Where(st => st.BranchId == branchId.Value);
                    }
                    // Không có branchId → Owner xem tất cả chi nhánh, không filter thêm
                }
                else
                {
                    // Staff: chỉ xem chi nhánh của mình từ JWT
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
        // Owner: có thể xem ticket của bất kỳ chi nhánh nào.
        // Staff: chỉ xem được ticket thuộc chi nhánh của mình.
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
                    // Staff: giới hạn theo chi nhánh trong JWT
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
                // Owner: không filter theo branch → xem được tất cả

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
        // Owner: truyền ?branchId=guid để tạo phiếu cho chi nhánh cụ thể.
        // Staff: tạo phiếu cho chi nhánh của mình trong JWT.
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> CreateInwardInventory(
            [FromBody] InwardInventoryCreateDto dto,
            [FromQuery] Guid? branchId = null)
        {
            // Resolve branchId: Owner dùng query param, Staff dùng JWT
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
                var stockTicket = new StockTicket
                {
                    StockTicketId = Guid.NewGuid(),
                    TicketCode = ticketCode,
                    BranchId = resolvedBranchId.Value,
                    UserId = currentUserId.Value,
                    TicketType = GetTicketTypeName(dto.InwardType),
                    Status = "COMPLETED",
                    Notes = dto.Note ?? dto.Reason,
                    CreatedAt = currentTime
                };

                _context.StockTickets.Add(stockTicket);

                foreach (var item in dto.Items)
                {
                    // Tìm linh hoạt: BranchProductId hoặc ProductId gốc
                    var branchProduct = await _context.BranchProducts
                        .FirstOrDefaultAsync(bp =>
                            (bp.BranchProductId == item.Id || bp.ProductId == item.Id)
                            && bp.BranchId == resolvedBranchId.Value);

                    if (branchProduct == null)
                    {
                        // Sản phẩm chưa có ở chi nhánh này → tạo mới BranchProduct
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
                        await _context.SaveChangesAsync(); // flush để có BranchProductId
                    }
                    else if (item.CostPrice > 0)
                    {
                        branchProduct.CostPrice = item.CostPrice;
                    }

                    var beforeQuantity = branchProduct.ActualStock;
                    branchProduct.ActualStock += item.Quantity;
                    branchProduct.AvailableStock += item.Quantity;

                    var ticketItem = new StockTicketItem
                    {
                        TicketItemId = Guid.NewGuid(),
                        StockTicketId = stockTicket.StockTicketId,
                        BranchProductId = branchProduct.BranchProductId, // FIX: dùng BranchProductId thực, không phải branchId
                        SystemQuantity = beforeQuantity,
                        ActualQuantity = branchProduct.ActualStock,
                        Discrepancy = item.Quantity
                    };

                    _context.StockTicketItems.Add(ticketItem);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Inward inventory created successfully",
                    Data = new
                    {
                        TicketId = stockTicket.StockTicketId,
                        TicketCode = stockTicket.TicketCode,
                        TicketType = stockTicket.TicketType,
                        BranchId = stockTicket.BranchId
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
        // Owner: chỉnh sửa được ticket ở bất kỳ chi nhánh nào.
        // Staff: chỉ chỉnh sửa được ticket thuộc chi nhánh của mình.
        // ============================================================
        [HttpPut("{id:guid}")]
        [HasPermission(PermissionConstants.STOCK_INWARD_UPDATE)]
        public async Task<IActionResult> UpdateInwardInventory(Guid id, [FromBody] InwardInventoryUpdateDto dto)
        {
            try
            {
                var stockTicketQuery = _context.StockTickets
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
        // DELETE /api/inwardinventory/{id}  (soft cancel)
        // Owner: huỷ được ticket ở bất kỳ chi nhánh nào.
        // Staff: chỉ huỷ được ticket thuộc chi nhánh của mình.
        // ============================================================
        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionConstants.STOCK_INWARD_DELETE)]
        public async Task<IActionResult> DeleteInwardInventory(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
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

                if (stockTicket.Status == "CANCELLED")
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Inward inventory is already cancelled"
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
                            branchProduct.ActualStock -= item.Discrepancy;
                            branchProduct.AvailableStock -= item.Discrepancy;
                        }
                    }
                }

                stockTicket.Status = "CANCELLED";
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Inward inventory cancelled successfully"
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
                Items = stockTicket.StockTicketItems?.Select(item => new InwardInventoryItemResponseDto
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