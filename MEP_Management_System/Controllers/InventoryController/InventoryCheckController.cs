using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;

namespace MEP_Management_System.Controllers.InventoryController;

[Authorize]
[ApiController]
[Route("api/inventory-check")]
public class InventoryCheckController : ControllerBase
{
    private readonly MEPDatabaseContext _context;

    public InventoryCheckController(MEPDatabaseContext context)
    {
        _context = context;
    }

    private Guid? GetUserBranchId()
    {
        var branchIdClaim = User.FindFirst("branchId")?.Value ?? User.FindFirst(ClaimTypes.GroupSid)?.Value;
        return Guid.TryParse(branchIdClaim, out var branchId) ? branchId : null;
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    [HttpPost]
    public async Task<IActionResult> CreateInventoryCheck([FromBody] CreateInventoryCheckDto dto)
    {
        if (dto == null || dto.Details == null || !dto.Details.Any())
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Dữ liệu kiểm kê không hợp lệ.",
                Errors = new() { "At least one inventory detail is required." }
            });
        }

        var branchId = GetUserBranchId();
        if (branchId == null)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "JWT token không có thông tin kho.",
                Errors = new() { "BranchId claim is required." }
            });
        }

        if (dto.BranchId.HasValue && dto.BranchId.Value != branchId.Value)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "BranchId không hợp lệ.",
                Errors = new() { "BranchId must match the current user branch." }
            });
        }

        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "JWT token không chứa userId.",
                Errors = new() { "User identity is required." }
            });
        }

        var validationErrors = ValidateInventoryCheckDetails(dto.Details);
        if (validationErrors.Any())
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Dữ liệu kiểm kê không hợp lệ.",
                Errors = validationErrors
            });
        }

        var ticket = new InventoryCheckTicket
        {
            TicketId = Guid.NewGuid(),
            TicketCode = await GenerateTicketCodeAsync(),
            BranchId = branchId.Value,
            CreatedByUserId = userId.Value,
            CreatedAt = DateTime.UtcNow,
            Status = InventoryCheckStatus.Pending,
            Notes = dto.Notes
        };

        foreach (var detail in dto.Details)
        {
            ticket.Details.Add(new InventoryCheckDetail
            {
                DetailId = Guid.NewGuid(),
                ProductId = detail.ProductId,
                SystemQuantity = detail.SystemQuantity,
                ActualQuantity = detail.ActualQuantity,
                Discrepancy = detail.ActualQuantity - detail.SystemQuantity
            });
        }

        await _context.InventoryCheckTickets.AddAsync(ticket);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetInventoryCheckById), new { id = ticket.TicketId }, new ApiResponse<InventoryCheckResponseDto>
        {
            Success = true,
            Message = "Phiếu kiểm kê đã được tạo.",
            Data = MapToResponse(ticket)
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetInventoryChecks([FromQuery] string? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        if (pageNumber < 1 || pageSize < 1 || pageSize > 200)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Tham số phân trang không hợp lệ.",
                Errors = new() { "pageNumber and pageSize must be positive." }
            });
        }

        var branchId = GetUserBranchId();
        if (branchId == null)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "JWT token không có thông tin kho.",
                Errors = new() { "BranchId claim is required." }
            });
        }

        var query = _context.InventoryCheckTickets
            .Include(t => t.Details)
            .Where(t => t.BranchId == branchId.Value)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InventoryCheckStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(t => t.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query.OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new InventoryCheckListItemDto
            {
                TicketId = t.TicketId,
                TicketCode = t.TicketCode,
                BranchId = t.BranchId,
                Status = t.Status.ToString(),
                CreatedAt = t.CreatedAt,
                DetailCount = t.Details.Count
            })
            .ToListAsync();

        return Ok(new ApiResponse<PaginatedResponse<InventoryCheckListItemDto>>
        {
            Success = true,
            Message = "Danh sách phiếu kiểm kê.",
            Data = new PaginatedResponse<InventoryCheckListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                HasNextPage = pageNumber < totalPages,
                HasPreviousPage = pageNumber > 1
            }
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetInventoryCheckById(Guid id)
    {
        var branchId = GetUserBranchId();
        if (branchId == null)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "JWT token không có thông tin kho.",
                Errors = new() { "BranchId claim is required." }
            });
        }

        var ticket = await _context.InventoryCheckTickets
            .Include(t => t.Details)
            .ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(t => t.TicketId == id && t.BranchId == branchId.Value);

        if (ticket == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Phiếu kiểm kê không tồn tại." });
        }

        return Ok(new ApiResponse<InventoryCheckResponseDto>
        {
            Success = true,
            Message = "Chi tiết phiếu kiểm kê.",
            Data = MapToResponse(ticket)
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateInventoryCheck(Guid id, [FromBody] CreateInventoryCheckDto dto)
    {
        if (dto == null || dto.Details == null || !dto.Details.Any())
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Dữ liệu kiểm kê không hợp lệ.",
                Errors = new() { "At least one inventory detail is required." }
            });
        }

        var branchId = GetUserBranchId();
        if (branchId == null)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "JWT token không có thông tin kho.",
                Errors = new() { "BranchId claim is required." }
            });
        }

        var ticket = await _context.InventoryCheckTickets
            .Include(t => t.Details)
            .FirstOrDefaultAsync(t => t.TicketId == id && t.BranchId == branchId.Value);

        if (ticket == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Phiếu kiểm kê không tồn tại." });
        }

        if (ticket.Status != InventoryCheckStatus.Pending)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Chỉ có phiếu Pending mới có thể chỉnh sửa." });
        }

        var validationErrors = ValidateInventoryCheckDetails(dto.Details);
        if (validationErrors.Any())
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Dữ liệu kiểm kê không hợp lệ.",
                Errors = validationErrors
            });
        }

        ticket.Notes = dto.Notes;
        ticket.Details.Clear();

        foreach (var detail in dto.Details)
        {
            ticket.Details.Add(new InventoryCheckDetail
            {
                DetailId = Guid.NewGuid(),
                ProductId = detail.ProductId,
                SystemQuantity = detail.SystemQuantity,
                ActualQuantity = detail.ActualQuantity,
                Discrepancy = detail.ActualQuantity - detail.SystemQuantity
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<InventoryCheckResponseDto>
        {
            Success = true,
            Message = "Phiếu kiểm kê đã được cập nhật.",
            Data = MapToResponse(ticket)
        });
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteInventoryCheck(Guid id)
    {
        var branchId = GetUserBranchId();
        if (branchId == null)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "JWT token không có thông tin kho.",
                Errors = new() { "BranchId claim is required." }
            });
        }

        var ticket = await _context.InventoryCheckTickets.FirstOrDefaultAsync(t => t.TicketId == id && t.BranchId == branchId.Value);
        if (ticket == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Phiếu kiểm kê không tồn tại." });
        }

        if (ticket.Status != InventoryCheckStatus.Pending)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Chỉ có phiếu Pending mới có thể hoàn tất." });
        }

        ticket.Status = InventoryCheckStatus.Completed;
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<InventoryCheckResponseDto>
        {
            Success = true,
            Message = "Phiếu kiểm kê đã hoàn tất.",
            Data = MapToResponse(ticket)
        });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelInventoryCheck(Guid id)
    {
        var branchId = GetUserBranchId();
        if (branchId == null)
        {
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "JWT token không có thông tin kho.",
                Errors = new() { "BranchId claim is required." }
            });
        }

        var ticket = await _context.InventoryCheckTickets.FirstOrDefaultAsync(t => t.TicketId == id && t.BranchId == branchId.Value);
        if (ticket == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Phiếu kiểm kê không tồn tại." });
        }

        if (ticket.Status != InventoryCheckStatus.Pending)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Chỉ có phiếu Pending mới có thể hủy." });
        }

        ticket.Status = InventoryCheckStatus.Cancelled;
        await _context.SaveChangesAsync();

        return Ok(new ApiResponse<object> { Success = true, Message = "Phiếu kiểm kê đã được hủy." });
    }

    private static List<string> ValidateInventoryCheckDetails(IEnumerable<InventoryCheckDetailCreateDto> details)
    {
        var errors = new List<string>();
        var productIds = new HashSet<Guid>();

        foreach (var detail in details)
        {
            if (detail.ProductId == Guid.Empty)
            {
                errors.Add("ProductId is required.");
            }

            if (detail.SystemQuantity < 0)
            {
                errors.Add($"SystemQuantity must be non-negative for product {detail.ProductId}.");
            }

            if (detail.ActualQuantity < 0)
            {
                errors.Add($"ActualQuantity must be non-negative for product {detail.ProductId}.");
            }

            if (!productIds.Add(detail.ProductId))
            {
                errors.Add($"Duplicate product found: {detail.ProductId}.");
            }
        }

        return errors;
    }

    private InventoryCheckResponseDto MapToResponse(InventoryCheckTicket ticket)
    {
        return new InventoryCheckResponseDto
        {
            TicketId = ticket.TicketId,
            TicketCode = ticket.TicketCode,
            BranchId = ticket.BranchId,
            CreatedByUserId = ticket.CreatedByUserId,
            CreatedAt = ticket.CreatedAt,
            Status = ticket.Status.ToString(),
            Notes = ticket.Notes,
            Details = ticket.Details.Select(d => new InventoryCheckDetailDto
            {
                DetailId = d.DetailId,
                ProductId = d.ProductId,
                ProductCode = d.Product?.ProductCode,
                ProductName = d.Product?.ProductName,
                SystemQuantity = d.SystemQuantity,
                ActualQuantity = d.ActualQuantity,
                Discrepancy = d.Discrepancy
            }).ToList()
        };
    }

    private async Task<string> GenerateTicketCodeAsync()
    {
        const string prefix = "KK";
        var last = await _context.InventoryCheckTickets
            .Where(t => t.TicketCode.StartsWith(prefix))
            .OrderByDescending(t => t.TicketCode)
            .Select(t => t.TicketCode)
            .FirstOrDefaultAsync();

        var next = 1;
        if (!string.IsNullOrWhiteSpace(last) && last.Length > prefix.Length)
        {
            var raw = last[prefix.Length..];
            if (int.TryParse(raw, out var parsed))
            {
                next = parsed + 1;
            }
        }

        return prefix + next.ToString("D5");
    }
}
