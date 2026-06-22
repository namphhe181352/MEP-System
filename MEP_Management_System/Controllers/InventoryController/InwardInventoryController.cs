using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace MEP_Management_System.Controllers.InventoryController
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class InwardInventoryController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;

        public InwardInventoryController(MEPDatabaseContext context)
        {
            _context = context;
        }

        private Guid? GetUserBranchId()
        {
            var branchIdClaim = User.FindFirst("branchId")?.Value ?? User.FindFirst(ClaimTypes.GroupSid)?.Value;
            return Guid.TryParse(branchIdClaim, out Guid branchId) ? branchId : null;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
        }

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

        [HttpGet]
        public async Task<IActionResult> GetInwardInventories(
            [FromQuery] string? status,
            [FromQuery] string? ticketType,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var branchId = GetUserBranchId();
                if (branchId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "JWT token is missing or invalid",
                        Errors = new() { "Authorization header required" }
                    });
                }

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var query = _context.StockTickets
                    .Include(st => st.User)
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                            .ThenInclude(bp => bp.Product)
                    .Where(st => st.BranchId == branchId.Value)
                    .Where(st => st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST")
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(st => st.Status != null && st.Status.ToLower() == status.Trim().ToLower());
                }

                if (!string.IsNullOrWhiteSpace(ticketType))
                {
                    query = query.Where(st => st.TicketType != null && st.TicketType.ToLower() == ticketType.Trim().ToLower());
                }

                if (fromDate.HasValue)
                {
                    query = query.Where(st => st.CreatedAt >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(st => st.CreatedAt <= toDate.Value.AddDays(1));
                }

                query = query.OrderByDescending(st => st.CreatedAt);

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var responseItems = items.Select(MapToInwardInventoryResponse).ToList();

                return Ok(new ApiResponse<PaginatedResponse<InwardInventoryResponseDto>>
                {
                    Success = true,
                    Message = "Inward inventory history retrieved successfully",
                    Data = new PaginatedResponse<InwardInventoryResponseDto>
                    {
                        Items = responseItems,
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

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetInwardInventoryById(Guid id)
        {
            try
            {
                var branchId = GetUserBranchId();
                if (branchId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "JWT token is missing or invalid",
                        Errors = new() { "Authorization header required" }
                    });
                }

                var stockTicket = await _context.StockTickets
                    .Include(st => st.User)
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                            .ThenInclude(bp => bp.Product)
                    .FirstOrDefaultAsync(st => st.StockTicketId == id && st.BranchId == branchId.Value &&
                        (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST"));

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

        [HttpPost]
        public async Task<IActionResult> CreateInwardInventory([FromBody] InwardInventoryCreateDto dto)
        {
            var branchId = GetUserBranchId();
            if (branchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "JWT token is missing or invalid",
                    Errors = new() { "Authorization header required" }
                });
            }

            //if (dto.BranchId != branchId.Value)
            //{
            //    return BadRequest(new ApiResponse<object>
            //    {
            //        Success = false,
            //        Message = "Invalid branch",
            //        Errors = new() { "BranchId does not match the current user branch." }
            //    });
            //}

            if (dto.Items == null || dto.Items.Count == 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Validation failed",
                    Errors = new() { "At least one item is required." }
                });
            }

            // Thực hiện validate số lượng đầu vào
            var itemValidationErrors = new List<string>();
            foreach (var item in dto.Items)
            {
                if (item.Quantity <= 0)
                {
                    itemValidationErrors.Add($"Quantity must be greater than 0 for Id {item.Id}.");
                }
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
                    UserId = currentUserId.Value,
                    TicketType = GetTicketTypeName(dto.InwardType),
                    Status = "COMPLETED",
                    Notes = dto.Note ?? dto.Reason,
                    CreatedAt = currentTime
                };

                _context.StockTickets.Add(stockTicket);

                foreach (var item in dto.Items)
                {
                    // Tìm kiếm linh hoạt: Check xem ID truyền lên là BranchProductId hay ProductId gốc
                    var branchProduct = await _context.BranchProducts
                        .FirstOrDefaultAsync(bp => (bp.BranchProductId == item.Id || bp.ProductId == item.Id) && bp.BranchId == branchId.Value);

                    if (branchProduct == null)
                    {
                        // Trường hợp chưa có dòng ở chi nhánh này -> Tìm từ bảng Product gốc
                        var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == item.Id);

                        if (product == null)
                        {
                            throw new InvalidOperationException($"Product/BranchProduct with ID {item.Id} was not found in system.");
                        }

                        branchProduct = new BranchProduct
                        {
                            BranchProductId = Guid.NewGuid(),
                            BranchId = branchId.Value,
                            ProductId = product.ProductId,
                            CostPrice = item.CostPrice > 0 ? item.CostPrice : 0, // Cập nhật giá nhập nếu có
                            SellPrice = 0,
                            ActualStock = 0,
                            ReservedStock = 0,
                            AvailableStock = 0,
                            IsActive = 1,
                            DirectSale = 1
                        };

                        _context.BranchProducts.Add(branchProduct);
                    }
                    else if (item.CostPrice > 0)
                    {
                        // Nếu đã tồn tại sản phẩm, cập nhật lại giá vốn mới nhất theo đợt nhập
                        branchProduct.CostPrice = item.CostPrice;
                    }

                    var beforeQuantity = branchProduct.ActualStock;
                    var newActualQuantity = beforeQuantity + item.Quantity;
                    var newAvailableQuantity = branchProduct.AvailableStock + item.Quantity;

                    branchProduct.ActualStock = newActualQuantity;
                    branchProduct.AvailableStock = newAvailableQuantity;

                    var ticketItem = new StockTicketItem
                    {
                        TicketItemId = Guid.NewGuid(),
                        StockTicketId = stockTicket.StockTicketId,
                        BranchProductId = branchId.Value,
                        SystemQuantity = beforeQuantity,
                        ActualQuantity = newActualQuantity,
                        Discrepancy = item.Quantity // Lưu giá trị tuyệt đối lượng biến động nhập kho
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

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateInwardInventory(Guid id, [FromBody] InwardInventoryUpdateDto dto)
        {
            try
            {
                var branchId = GetUserBranchId();
                if (branchId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "JWT token is missing or invalid",
                        Errors = new() { "Authorization header required" }
                    });
                }

                var stockTicket = await _context.StockTickets
                    .FirstOrDefaultAsync(st => st.StockTicketId == id && st.BranchId == branchId.Value &&
                        (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST"));

                if (stockTicket == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Inward inventory not found",
                        Errors = new() { $"No inward inventory with ID: {id}" }
                    });
                }

                if (!string.IsNullOrWhiteSpace(dto.Reason))
                {
                    stockTicket.Notes = dto.Reason;
                }

                if (!string.IsNullOrWhiteSpace(dto.Note))
                {
                    stockTicket.Notes = dto.Note;
                }

                if (!string.IsNullOrWhiteSpace(dto.Status))
                {
                    var normalized = dto.Status.Trim().ToUpperInvariant();
                    if (normalized is "PENDING" or "COMPLETED" or "CANCELLED")
                    {
                        stockTicket.Status = normalized;
                    }
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

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteInwardInventory(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var branchId = GetUserBranchId();
                if (branchId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "JWT token is missing or invalid",
                        Errors = new() { "Authorization header required" }
                    });
                }

                var stockTicket = await _context.StockTickets
                    .Include(st => st.StockTicketItems)
                    .FirstOrDefaultAsync(st => st.StockTicketId == id && st.BranchId == branchId.Value &&
                        (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST"));

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
                            .FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId && bp.BranchId == branchId.Value);

                        if (branchProduct != null)
                        {
                            // Trừ trả lại lượng hàng đã cộng vào trước đó khi nhập kho
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
                Items = stockTicket.StockTicketItems.Select(item => new InwardInventoryItemResponseDto
                {
                    TicketItemId = item.TicketItemId,
                    BranchProductId = item.BranchProductId,
                    ProductId = item.BranchProduct?.ProductId ?? Guid.Empty,
                    ProductCode = item.BranchProduct?.Product?.ProductCode ?? string.Empty,
                    ProductName = item.BranchProduct?.Product?.ProductName ?? string.Empty,
                    SystemQuantity = item.SystemQuantity,
                    Quantity = item.Discrepancy,
                    ActualQuantity = item.ActualQuantity
                }).ToList()
            };
        }
    }
}