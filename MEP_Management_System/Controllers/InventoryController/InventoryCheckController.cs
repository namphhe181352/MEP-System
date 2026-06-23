using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.InventoryController;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InventoryCheckController : ControllerBase
{
    private readonly MEPDatabaseContext _context;

    public InventoryCheckController(MEPDatabaseContext context)
    {
        _context = context;
    }

    private Guid? GetUserBranchId()
    {
        var branchClaim = User.FindFirst("branchId")?.Value ?? User.FindFirst(ClaimTypes.GroupSid)?.Value;
        return Guid.TryParse(branchClaim, out var branchId) ? branchId : null;
    }

    private Guid? GetUserId()
    {
        var userClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userClaim, out var userId) ? userId : null;
    }

    [HttpGet]
    public async Task<IActionResult> GetInventoryChecks(
        [FromQuery] string? status,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var branchId = GetUserBranchId();
            if (branchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy thông tin chi nhánh trong Token.",
                    Errors = new() { "branchId claim is required." }
                });
            }

            if (pageNumber < 1 || pageSize < 1 || pageSize > 200)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Tham số phân trang không hợp lệ.",
                    Errors = new() { "pageNumber must be >= 1 and pageSize must be between 1 and 200." }
                });
            }

            var query = _context.InventoryCheckTickets
                .Where(ticket => ticket.BranchId == branchId.Value)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<InventoryCheckTicketStatus>(status, true, out var parsedStatus))
                {
                    query = query.Where(ticket => ticket.Status == parsedStatus);
                }
                else
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Giá trị Status không hợp lệ.",
                        Errors = new() { "Status phải thuộc một trong các giá trị: Draft, WaitingForApproval, Completed, Cancelled." }
                    });
                }
            }

            if (startDate.HasValue)
            {
                query = query.Where(ticket => ticket.CreatedAt >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                var inclusiveEnd = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(ticket => ticket.CreatedAt <= inclusiveEnd);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .OrderByDescending(ticket => ticket.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(ticket => new InventoryCheckListItemDto
                {
                    TicketId = ticket.TicketId,
                    TicketCode = ticket.TicketCode,
                    CreatedAt = ticket.CreatedAt,
                    Status = ticket.Status.ToString(),
                    DetailCount = ticket.Details.Count,
                    BranchId = ticket.BranchId,
                    CreatedByUserId = ticket.CreatedByUserId
                })
                .ToListAsync();

            var response = new PaginatedResponse<InventoryCheckListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                HasNextPage = pageNumber < totalPages,
                HasPreviousPage = pageNumber > 1
            };

            return Ok(new ApiResponse<PaginatedResponse<InventoryCheckListItemDto>>
            {
                Success = true,
                Message = "Danh sách phiếu kiểm kê đã được tải.",
                Data = response
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi tải danh sách phiếu kiểm kê.",
                Errors = new() { ex.Message }
            });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetInventoryCheckById(Guid id)
    {
        try
        {
            var branchId = GetUserBranchId();
            if (branchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy thông tin chi nhánh trong Token.",
                    Errors = new() { "branchId claim is required." }
                });
            }

            var ticket = await _context.InventoryCheckTickets
                .Include(ticket => ticket.Details)
                    .ThenInclude(detail => detail.Product)
                .FirstOrDefaultAsync(ticket => ticket.TicketId == id && ticket.BranchId == branchId.Value);

            if (ticket == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu kiểm kê không tồn tại.",
                    Errors = new() { $"Không tìm thấy phiếu với ID: {id}." }
                });
            }

            return Ok(new ApiResponse<InventoryCheckResponseDto>
            {
                Success = true,
                Message = "Chi tiết phiếu kiểm kê đã được tải.",
                Data = MapToResponseDto(ticket)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi tải phiếu kiểm kê.",
                Errors = new() { ex.Message }
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateInventoryCheck([FromBody] CreateInventoryCheckDto dto)
    {
        try
        {
            if (dto == null || dto.ProductIds == null || dto.ProductIds.Count == 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Dữ liệu tạo phiếu kiểm kê không hợp lệ.",
                    Errors = new() { "ProductIds là bắt buộc và không thể rỗng." }
                });
            }

            var branchId = GetUserBranchId();
            if (branchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy thông tin chi nhánh trong Token.",
                    Errors = new() { "branchId claim is required." }
                });
            }

            var userId = GetUserId();
            if (userId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy thông tin người dùng trong Token.",
                    Errors = new() { "User claim is required." }
                });
            }

            var branchProducts = await _context.BranchProducts
                .Where(bp => bp.BranchId == branchId.Value && dto.ProductIds.Contains(bp.ProductId))
                .ToListAsync();

            var missingProductIds = dto.ProductIds.Except(branchProducts.Select(bp => bp.ProductId)).ToList();
            if (missingProductIds.Any())
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Một số sản phẩm không tồn tại trong kho chi nhánh.",
                    Errors = missingProductIds.Select(id => $"Sản phẩm không tồn tại hoặc không thuộc chi nhánh của bạn: {id}").ToList()
                });
            }

            var ticket = new InventoryCheckTicket
            {
                TicketId = Guid.NewGuid(),
                TicketCode = await GenerateTicketCodeAsync(),
                CreatedAt = DateTime.UtcNow,
                Status = InventoryCheckTicketStatus.Draft,
                BranchId = branchId.Value,
                CreatedByUserId = userId.Value,
                Notes = dto.Notes
            };

            foreach (var branchProduct in branchProducts)
            {
                var systemQuantity = Convert.ToInt32(Math.Floor(branchProduct.ActualStock));
                ticket.Details.Add(new InventoryCheckDetail
                {
                    DetailId = Guid.NewGuid(),
                    ProductId = branchProduct.ProductId,
                    SystemQuantity = systemQuantity,
                    ActualQuantity = 0,
                    // At draft time we haven't counted yet — store a placeholder discrepancy
                    // so the UI can show it's uncounted. We'll treat Draft as "no discrepancy shown" in DTO mapping.
                    Discrepancy = 0 - systemQuantity
                });
            }

            await _context.InventoryCheckTickets.AddAsync(ticket);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetInventoryCheckById), new { id = ticket.TicketId }, new ApiResponse<InventoryCheckResponseDto>
            {
                Success = true,
                Message = "Phiếu kiểm kê đã được tạo.",
                Data = MapToResponseDto(ticket)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi tạo phiếu kiểm kê.",
                Errors = new() { ex.Message }
            });
        }
    }

    [HttpPut("{id:guid}/fill")]
    public async Task<IActionResult> FillActualQuantities(Guid id, [FromBody] FillActualQuantityDto dto)
    {
        try
        {
            if (dto == null || dto.Details == null || dto.Details.Count == 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Dữ liệu cập nhật số đếm thực tế không hợp lệ.",
                    Errors = new() { "Details là bắt buộc và không thể rỗng." }
                });
            }

            var branchId = GetUserBranchId();
            if (branchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy thông tin chi nhánh trong Token.",
                    Errors = new() { "branchId claim is required." }
                });
            }

            var ticket = await _context.InventoryCheckTickets
                .Include(t => t.Details)
                .FirstOrDefaultAsync(t => t.TicketId == id && t.BranchId == branchId.Value);

            if (ticket == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu kiểm kê không tồn tại.",
                    Errors = new() { $"Không tìm thấy phiếu với ID: {id}." }
                });
            }

            if (ticket.Status == InventoryCheckTicketStatus.Completed || ticket.Status == InventoryCheckTicketStatus.Cancelled)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu kiểm kê đã hoàn tất hoặc đã hủy và không thể cập nhật.",
                    Errors = new() { "Only Draft or WaitingForApproval tickets can be updated." }
                });
            }

            var detailMap = ticket.Details.ToDictionary(detail => detail.DetailId);
            var invalidIds = dto.Details.Where(item => !detailMap.ContainsKey(item.DetailId)).Select(item => item.DetailId).ToList();
            if (invalidIds.Any())
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Một số chi tiết không tồn tại trong phiếu.",
                    Errors = invalidIds.Select(id => $"DetailId không hợp lệ: {id}").ToList()
                });
            }

            foreach (var update in dto.Details)
            {
                var detail = detailMap[update.DetailId];
                detail.ActualQuantity = update.ActualQuantity;
                detail.Discrepancy = detail.ActualQuantity - detail.SystemQuantity;
            }

            ticket.Status = InventoryCheckTicketStatus.WaitingForApproval;

            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<InventoryCheckResponseDto>
            {
                Success = true,
                Message = "Số lượng đếm thực tế đã được cập nhật.",
                Data = MapToResponseDto(ticket)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi cập nhật số lượng thực tế.",
                Errors = new() { ex.Message }
            });
        }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> ApproveInventoryCheck(Guid id)
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
                    Message = "Không tìm thấy thông tin chi nhánh trong Token.",
                    Errors = new() { "branchId claim is required." }
                });
            }

            var ticket = await _context.InventoryCheckTickets
                .Include(t => t.Details)
                .FirstOrDefaultAsync(t => t.TicketId == id && t.BranchId == branchId.Value);

            if (ticket == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu kiểm kê không tồn tại.",
                    Errors = new() { $"Không tìm thấy phiếu với ID: {id}." }
                });
            }

            if (ticket.Status == InventoryCheckTicketStatus.Completed)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu kiểm kê đã hoàn tất.",
                    Errors = new() { "Không thể duyệt lại phiếu đã hoàn thành." }
                });
            }

            if (ticket.Status == InventoryCheckTicketStatus.Cancelled)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu kiểm kê đã hủy.",
                    Errors = new() { "Không thể duyệt phiếu đã hủy." }
                });
            }

            if (!ticket.Details.Any())
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu kiểm kê không có chi tiết.",
                    Errors = new() { "Không có sản phẩm nào để điều chỉnh tồn kho." }
                });
            }

            var productIds = ticket.Details.Select(detail => detail.ProductId).Distinct().ToList();
            var branchProducts = await _context.BranchProducts
                .Where(bp => bp.BranchId == branchId.Value && productIds.Contains(bp.ProductId))
                .ToDictionaryAsync(bp => bp.ProductId);

            foreach (var detail in ticket.Details)
            {
                if (!branchProducts.TryGetValue(detail.ProductId, out var branchProduct))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Dữ liệu tồn kho không đầy đủ.",
                        Errors = new() { $"Không tìm thấy tồn kho chi nhánh cho sản phẩm {detail.ProductId}." }
                    });
                }

                // Use the counted actual quantity as the single source of truth when approving.
                var actualQty = detail.ActualQuantity;
                if (actualQty < 0)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Số lượng đếm thực tế không hợp lệ.",
                        Errors = new() { $"Sản phẩm {detail.ProductId} có ActualQuantity < 0." }
                    });
                }

                // Assign actual quantities directly — this prevents issues when other operations
                // (sales/receipts) happened during the counting window.
                branchProduct.ActualStock = Convert.ToDecimal(actualQty);
                branchProduct.AvailableStock = Convert.ToDecimal(actualQty);
            }

            ticket.Status = InventoryCheckTicketStatus.Completed;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new ApiResponse<InventoryCheckResponseDto>
            {
                Success = true,
                Message = "Phiếu kiểm kê đã được duyệt và tồn kho đã được điều chỉnh.",
                Data = MapToResponseDto(ticket)
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi duyệt phiếu kiểm kê.",
                Errors = new() { ex.Message }
            });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelInventoryCheck(Guid id)
    {
        try
        {
            var branchId = GetUserBranchId();
            if (branchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy thông tin chi nhánh trong Token.",
                    Errors = new() { "branchId claim is required." }
                });
            }

            var ticket = await _context.InventoryCheckTickets
                .FirstOrDefaultAsync(t => t.TicketId == id && t.BranchId == branchId.Value);

            if (ticket == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu kiểm kê không tồn tại.",
                    Errors = new() { $"Không tìm thấy phiếu với ID: {id}." }
                });
            }

            if (ticket.Status == InventoryCheckTicketStatus.Completed)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu kiểm kê đã hoàn tất.",
                    Errors = new() { "Không thể hủy phiếu đã hoàn thành." }
                });
            }

            if (ticket.Status == InventoryCheckTicketStatus.Cancelled)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu kiểm kê đã được hủy trước đó.",
                    Errors = new() { "Phiếu đã ở trạng thái Cancelled." }
                });
            }

            ticket.Status = InventoryCheckTicketStatus.Cancelled;
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Phiếu kiểm kê đã được hủy.",
                Data = null
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi hủy phiếu kiểm kê.",
                Errors = new() { ex.Message }
            });
        }
    }

    private InventoryCheckResponseDto MapToResponseDto(InventoryCheckTicket ticket)
    {
        return new InventoryCheckResponseDto
        {
            TicketId = ticket.TicketId,
            TicketCode = ticket.TicketCode,
            CreatedAt = ticket.CreatedAt,
            Status = ticket.Status.ToString(),
            BranchId = ticket.BranchId,
            CreatedByUserId = ticket.CreatedByUserId,
            Notes = ticket.Notes,
            Details = ticket.Details.Select(detail => new InventoryCheckDetailDto
            {
                DetailId = detail.DetailId,
                ProductId = detail.ProductId,
                ProductCode = detail.Product?.ProductCode,
                ProductName = detail.Product?.ProductName,
                SystemQuantity = detail.SystemQuantity,
                ActualQuantity = detail.ActualQuantity,
                // For Draft tickets we don't want to surface an actual discrepancy — treat as not-yet-counted
                Discrepancy = ticket.Status == InventoryCheckTicketStatus.Draft ? 0 : detail.Discrepancy
            }).ToList()
        };
    }

    private async Task<string> GenerateTicketCodeAsync()
    {
        const string prefix = "KKK";
        var lastCode = await _context.InventoryCheckTickets
            .Where(t => t.TicketCode.StartsWith(prefix))
            .OrderByDescending(t => t.TicketCode)
            .Select(t => t.TicketCode)
            .FirstOrDefaultAsync();

        var nextSequence = 1;
        if (!string.IsNullOrWhiteSpace(lastCode) && lastCode.Length > prefix.Length)
        {
            var numberPart = lastCode[prefix.Length..];
            if (int.TryParse(numberPart, out var parsedNumber))
            {
                nextSequence = parsedNumber + 1;
            }
        }

        return prefix + nextSequence.ToString("D5");
    }
}