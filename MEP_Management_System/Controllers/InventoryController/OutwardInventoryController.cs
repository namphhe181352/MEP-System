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

namespace MEP_Management_System.Controllers.InventoryController
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Xuất nhập kho (Inventory)")]
    public class OutwardInventoryController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;

        public OutwardInventoryController(MEPDatabaseContext context)
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

        [HttpGet]
        public async Task<IActionResult> GetOutwardInventories(
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
                    return Unauthorized(new ApiResponse<object> { Success = false, Message = "JWT token is missing or invalid", Errors = new() { "Authorization header required" } });
                }

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var query = _context.StockTickets
                    .Include(st => st.User)
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                            .ThenInclude(bp => bp.Product)
                    .Where(st => st.BranchId == branchId.Value)
                    .Where(st => st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER")
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(st => st.Status != null && st.Status.ToLower() == status.Trim().ToLower());
                }

                if (!string.IsNullOrWhiteSpace(ticketType))
                {
                    query = query.Where(st => st.TicketType != null && st.TicketType.ToLower() == ticketType.Trim().ToLower());
                }

                if (fromDate.HasValue) query = query.Where(st => st.CreatedAt >= fromDate.Value);
                if (toDate.HasValue) query = query.Where(st => st.CreatedAt <= toDate.Value.AddDays(1));

                query = query.OrderByDescending(st => st.CreatedAt);

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
                var responseItems = items.Select(MapToOutwardInventoryResponse).ToList();

                return Ok(new ApiResponse<PaginatedResponse<OutwardInventoryResponseDto>>
                {
                    Success = true,
                    Message = "Outward inventory history retrieved successfully",
                    Data = new PaginatedResponse<OutwardInventoryResponseDto>
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
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Error retrieving outward inventory", Errors = new() { ex.Message } });
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetOutwardInventoryById(Guid id)
        {
            try
            {
                var branchId = GetUserBranchId();
                if (branchId == null)
                {
                    return Unauthorized(new ApiResponse<object> { Success = false, Message = "JWT token is missing or invalid", Errors = new() { "Authorization header required" } });
                }

                var stockTicket = await _context.StockTickets
                    .Include(st => st.User)
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                            .ThenInclude(bp => bp.Product)
                    .FirstOrDefaultAsync(st => st.StockTicketId == id && st.BranchId == branchId.Value &&
                        (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER"));

                if (stockTicket == null)
                {
                    return NotFound(new ApiResponse<object> { Success = false, Message = "Outward inventory not found", Errors = new() { $"No outward inventory with ID: {id}" } });
                }

                return Ok(new ApiResponse<OutwardInventoryResponseDto>
                {
                    Success = true,
                    Message = "Outward inventory retrieved successfully",
                    Data = MapToOutwardInventoryResponse(stockTicket)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Error retrieving outward inventory", Errors = new() { ex.Message } });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateOutwardInventory([FromBody] OutwardInventoryCreateDto dto)
        {
            try
            {
                var branchId = GetUserBranchId();
                if (branchId == null)
                {
                    return Unauthorized(new ApiResponse<object> { Success = false, Message = "JWT token is missing or invalid", Errors = new() { "Authorization header required" } });
                }

                //if (dto.BranchId != branchId.Value)
                //{
                //    return BadRequest(new ApiResponse<object> { Success = false, Message = "Invalid branch", Errors = new() { "BranchId does not match user branch." } });
                //}

                if (dto.Items == null || dto.Items.Count == 0)
                {
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Validation failed", Errors = new() { "At least one item is required." } });
                }

                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                {
                    return Unauthorized(new ApiResponse<object> { Success = false, Message = "JWT token is missing or invalid", Errors = new() { "User identity missing." } });
                }

                var branchProductIds = dto.Items.Select(i => i.BranchProductId).Distinct().ToList();
                // ĐOẠN MỚI ĐÃ SỬA: Thay bp.BranchProductId bằng bp.ProductId
                var branchProducts = await _context.BranchProducts
                    .Where(bp => bp.BranchId == branchId.Value && branchProductIds.Contains(bp.ProductId))
                    .Include(bp => bp.Product)
                    .ToListAsync();

                if (branchProducts.Count != branchProductIds.Count)
                {
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Validation failed", Errors = new() { "One or more products do not belong to this branch." } });
                }

                // Kiểm tra tồn kho khả dụng
                var validationErrors = new List<string>();
                foreach (var item in dto.Items)
                {
                    if (item.Quantity <= 0)
                    {
                        validationErrors.Add($"Quantity must be greater than 0.");
                        continue;
                    }

                    var bp = branchProducts.FirstOrDefault(x => x.BranchProductId == item.BranchProductId);
                    if (bp != null && item.Quantity > bp.AvailableStock)
                    {
                        validationErrors.Add($"Insufficient stock for product '{bp.Product?.ProductName ?? item.BranchProductId.ToString()}'.");
                    }
                }

                if (validationErrors.Any())
                {
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Inventory validation failed", Errors = validationErrors });
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
                        BranchId = branchId.Value,
                        UserId = currentUserId.Value,
                        TicketType = GetTicketTypeName(dto.OutwardType),
                        Status = "COMPLETED",
                        Notes = !string.IsNullOrWhiteSpace(dto.Note) ? dto.Note : dto.Reason,
                        CreatedAt = currentTime
                    };

                    _context.StockTickets.Add(stockTicket);

                    foreach (var item in dto.Items)
                    {
                        var bp = branchProducts.First(x => x.ProductId == item.BranchProductId);
                        var systemQtyBefore = bp.ActualStock;

                        // Trừ kho thực tế và khả dụng
                        bp.ActualStock -= item.Quantity;
                        bp.AvailableStock -= item.Quantity;

                        var ticketItem = new StockTicketItem
                        {
                            TicketItemId = Guid.NewGuid(),
                            StockTicketId = stockTicket.StockTicketId,
                            // SỬA DÒNG NÀY: Lấy chính xác mã tự sinh của chi nhánh từ đối tượng 'bp' đã tìm được trong DB
                            BranchProductId = bp.BranchProductId,
                            SystemQuantity = bp.AvailableStock,
                            ActualQuantity = bp.AvailableStock - item.Quantity,
                            Discrepancy = item.Quantity
                        };

                        _context.StockTicketItems.Add(ticketItem);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Outward inventory created successfully",
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
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Error creating outward inventory", Errors = new() { ex.Message } });
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateOutwardInventory(Guid id, [FromBody] OutwardInventoryUpdateDto dto)
        {
            try
            {
                var branchId = GetUserBranchId();
                if (branchId == null)
                {
                    return Unauthorized(new ApiResponse<object> { Success = false, Message = "JWT token is missing or invalid", Errors = new() { "Authorization header required" } });
                }

                var stockTicket = await _context.StockTickets
                    .FirstOrDefaultAsync(st => st.StockTicketId == id && st.BranchId == branchId.Value &&
                        (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER"));

                if (stockTicket == null)
                {
                    return NotFound(new ApiResponse<object> { Success = false, Message = "Outward inventory not found", Errors = new() { $"No outward inventory with ID: {id}" } });
                }

                if (!string.IsNullOrWhiteSpace(dto.Reason)) stockTicket.Notes = dto.Reason;
                if (!string.IsNullOrWhiteSpace(dto.Status))
                {
                    var normalized = dto.Status.Trim().ToUpperInvariant();
                    if (normalized is "PENDING" or "COMPLETED" or "CANCELLED") stockTicket.Status = normalized;
                }

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<OutwardInventoryResponseDto>
                {
                    Success = true,
                    Message = "Outward inventory updated successfully",
                    Data = MapToOutwardInventoryResponse(stockTicket)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Error updating outward inventory", Errors = new() { ex.Message } });
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteOutwardInventory(Guid id)
        {
            try
            {
                var branchId = GetUserBranchId();
                if (branchId == null)
                {
                    return Unauthorized(new ApiResponse<object> { Success = false, Message = "JWT token is missing or invalid", Errors = new() { "Authorization header required" } });
                }

                var stockTicket = await _context.StockTickets
                    .Include(st => st.StockTicketItems)
                    .FirstOrDefaultAsync(st => st.StockTicketId == id && st.BranchId == branchId.Value &&
                        (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER"));

                if (stockTicket == null)
                {
                    return NotFound(new ApiResponse<object> { Success = false, Message = "Outward inventory not found", Errors = new() { $"No outward inventory with ID: {id}" } });
                }

                if (stockTicket.Status == "CANCELLED")
                {
                    return Ok(new ApiResponse<object> { Success = true, Message = "Outward inventory is already cancelled" });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    if (stockTicket.Status == "COMPLETED")
                    {
                        foreach (var item in stockTicket.StockTicketItems)
                        {
                            var branchProduct = await _context.BranchProducts
                                .FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId && bp.BranchId == branchId.Value);

                            if (branchProduct != null)
                            {
                                // Cộng trả lại số lượng dựa trên cột Discrepancy
                                branchProduct.ActualStock += item.Discrepancy;
                                branchProduct.AvailableStock += item.Discrepancy;
                            }
                        }
                    }

                    stockTicket.Status = "CANCELLED";
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new ApiResponse<object> { Success = true, Message = "Outward inventory cancelled successfully" });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Error cancelling outward inventory", Errors = new() { ex.Message } });
            }
        }

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
                Items = stockTicket.StockTicketItems.Select(item => new OutwardInventoryItemResponseDto
                {
                    TicketItemId = item.TicketItemId,
                    BranchProductId = item.BranchProductId,
                    ProductId = item.BranchProduct?.ProductId ?? Guid.Empty,
                    ProductCode = item.BranchProduct?.Product?.ProductCode ?? string.Empty,
                    ProductName = item.BranchProduct?.Product?.ProductName ?? string.Empty,
                    SystemQuantity = item.SystemQuantity,
                    Quantity = item.Discrepancy, // Lấy từ Discrepancy ra làm Số lượng xuất dữ liệu
                    ActualQuantity = item.ActualQuantity
                }).ToList()
            };
        }
    }
}