using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Hubs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.SignalR;
using MEP_Management_System.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.InventoryController;

[ApiController]
[Route("api/[controller]")]
[Tags("Quản lý Kho (Inventory)")]
public class InventoryCheckController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly IHubContext<InventoryHub> _hub;
    private readonly ICurrentBranchAccessor _currentBranch;

    public InventoryCheckController(MEPDatabaseContext context, IHubContext<InventoryHub> hub, ICurrentBranchAccessor currentBranch)
    {
        _context = context;
        _hub = hub;
        _currentBranch = currentBranch;
    }

    private Guid? GetUserId()
    {
        var userClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userClaim, out var userId) ? userId : null;
    }

    [HttpGet]
    [HasPermission(PermissionConstants.STOCK_CHECK_VIEW)]
    public async Task<IActionResult> GetInventoryChecks(
        [FromQuery] string? status,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? branchId = null)
    {
        try
        {
            var resolvedBranchId = branchId ?? _currentBranch.GetBranchId();
            if (resolvedBranchId == null)
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
                .Where(ticket => ticket.BranchId == resolvedBranchId.Value)
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
                    CreatedByUserId = ticket.CreatedByUserId,
                    AssigneeUserId = ticket.AssigneeUserId
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
    [HasPermission(PermissionConstants.STOCK_CHECK_VIEW)]
    public async Task<IActionResult> GetInventoryCheckById(Guid id)
    {
        try
        {
            var branchId = _currentBranch.GetBranchId();
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
    [HasPermission(PermissionConstants.STOCK_CHECK_CREATE)]
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

            var branchId = _currentBranch.GetBranchId();
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

            // Validate: Check if any products are already in active inventory checks (Draft/WaitingForApproval)
            var existingActiveChecks = await _context.InventoryCheckTickets
                .Where(t => t.BranchId == branchId.Value &&
                           (t.Status == InventoryCheckTicketStatus.Draft || t.Status == InventoryCheckTicketStatus.WaitingForApproval))
                .SelectMany(t => t.Details)
                .Where(d => dto.ProductIds.Contains(d.ProductId))
                .Select(d => d.ProductId)
                .Distinct()
                .ToListAsync();

            if (existingActiveChecks.Any())
            {
                var productNames = await _context.Products
                    .Where(p => existingActiveChecks.Contains(p.ProductId))
                    .Select(p => p.ProductName)
                    .Take(5)
                    .ToListAsync();

                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Một số sản phẩm đang nằm trong phiếu kiểm kê chưa hoàn tất.",
                    Errors = new() { $"Các sản phẩm sau đang có phiếu kiểm kê Draft/WaitingForApproval: {string.Join(", ", productNames)}" }
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
                AssigneeUserId = dto.AssigneeUserId,
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
                    IsCounted = false,
                    Discrepancy = 0 - systemQuantity
                });
            }

            await _context.InventoryCheckTickets.AddAsync(ticket);
            await _context.SaveChangesAsync();

            // SignalR notification
            await _hub.Clients.Group($"Branch_{branchId.Value}")
                .SendAsync("InventoryCheckCreated", new
                {
                    ticketId = ticket.TicketId,
                    ticketCode = ticket.TicketCode,
                    branchId = ticket.BranchId,
                    createdBy = ticket.CreatedByUserId
                });

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

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionConstants.STOCK_CHECK_CREATE)]
    public async Task<IActionResult> UpdateInventoryCheck(Guid id, [FromBody] UpdateInventoryCheckDto dto)
    {
        try
        {
            var branchId = _currentBranch.GetBranchId();
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

            if (ticket.Status != InventoryCheckTicketStatus.Draft)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Chỉ có thể cập nhật phiếu ở trạng thái Draft.",
                    Errors = new() { "Chỉ phiếu Draft mới có thể được cập nhật." }
                });
            }

            // Update notes
            if (dto.Notes != null)
            {
                ticket.Notes = dto.Notes;
            }

            // Update assignee
            if (dto.AssigneeUserId.HasValue)
            {
                ticket.AssigneeUserId = dto.AssigneeUserId;
            }

            // Add products
            if (dto.AddProductIds != null && dto.AddProductIds.Any())
            {
                var existingProductIds = ticket.Details.Select(d => d.ProductId).ToHashSet();
                var newProductIds = dto.AddProductIds.Except(existingProductIds).ToList();

                if (newProductIds.Any())
                {
                    // Check for duplicate in other active checks
                    var activeProductIds = await _context.InventoryCheckTickets
                        .Where(t => t.BranchId == branchId.Value && t.TicketId != id &&
                                   (t.Status == InventoryCheckTicketStatus.Draft || t.Status == InventoryCheckTicketStatus.WaitingForApproval))
                        .SelectMany(t => t.Details)
                        .Select(d => d.ProductId)
                        .Distinct()
                        .ToListAsync();

                    var conflictingIds = newProductIds.Intersect(activeProductIds).ToList();
                    if (conflictingIds.Any())
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Một số sản phẩm đang nằm trong phiếu kiểm kê chưa hoàn tất.",
                            Errors = new() { $"Các sản phẩm sau đang có phiếu kiểm kê Draft/WaitingForApproval: {string.Join(", ", conflictingIds)}" }
                        });
                    }

                    var branchProducts = await _context.BranchProducts
                        .Where(bp => bp.BranchId == branchId.Value && newProductIds.Contains(bp.ProductId))
                        .ToListAsync();

                    foreach (var bp in branchProducts)
                    {
                        var systemQuantity = Convert.ToInt32(Math.Floor(bp.ActualStock));
                        ticket.Details.Add(new InventoryCheckDetail
                        {
                            DetailId = Guid.NewGuid(),
                            ProductId = bp.ProductId,
                            SystemQuantity = systemQuantity,
                            ActualQuantity = 0,
                            IsCounted = false,
                            Discrepancy = 0 - systemQuantity
                        });
                    }
                }
            }

            // Remove products
            if (dto.RemoveProductIds != null && dto.RemoveProductIds.Any())
            {
                var detailsToRemove = ticket.Details.Where(d => dto.RemoveProductIds.Contains(d.ProductId)).ToList();
                foreach (var detail in detailsToRemove)
                {
                    _context.InventoryCheckDetails.Remove(detail);
                }
            }

            await _context.SaveChangesAsync();

            // Reload with product details
            var updatedTicket = await _context.InventoryCheckTickets
                .Include(t => t.Details)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(t => t.TicketId == id);

            return Ok(new ApiResponse<InventoryCheckResponseDto>
            {
                Success = true,
                Message = "Phiếu kiểm kê đã được cập nhật.",
                Data = MapToResponseDto(updatedTicket!)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi cập nhật phiếu kiểm kê.",
                Errors = new() { ex.Message }
            });
        }
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionConstants.STOCK_CHECK_CREATE)]
    public async Task<IActionResult> DeleteInventoryCheck(Guid id)
    {
        try
        {
            var branchId = _currentBranch.GetBranchId();
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

            if (ticket.Status != InventoryCheckTicketStatus.Draft)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Chỉ có thể xóa phiếu ở trạng thái Draft.",
                    Errors = new() { "Chỉ phiếu Draft mới có thể được xóa." }
                });
            }

            // Only allow creator or Owner to delete
            var isOwner = User.HasClaim(c => c.Type == "IsOwner" && c.Value == "true");
            if (ticket.CreatedByUserId != userId && !isOwner)
            {
                return Forbid();
            }

            // Delete details first
            var details = await _context.InventoryCheckDetails
                .Where(d => d.TicketId == id)
                .ToListAsync();
            _context.InventoryCheckDetails.RemoveRange(details);

            // Delete ticket
            _context.InventoryCheckTickets.Remove(ticket);
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Phiếu kiểm kê đã được xóa.",
                Data = null
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi xóa phiếu kiểm kê.",
                Errors = new() { ex.Message }
            });
        }
    }

    [HttpPut("{id:guid}/fill")]
    [HasPermission(PermissionConstants.STOCK_CHECK_CREATE)]
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

            var branchId = _currentBranch.GetBranchId();
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

            if (ticket.Status != InventoryCheckTicketStatus.Draft)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu kiểm kê đã chuyển trạng thái và không thể cập nhật.",
                    Errors = new() { "Chỉ phiếu Draft mới có thể nhập số lượng." }
                });
            }

            // Check assignee permission
            if (ticket.AssigneeUserId.HasValue && ticket.AssigneeUserId != userId)
            {
                var isOwner = User.HasClaim(c => c.Type == "IsOwner" && c.Value == "true");
                if (!isOwner)
                {
                    return Forbid();
                }
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
                detail.IsCounted = true;
                detail.CountedByUserId = userId;
                detail.CountedAt = DateTime.UtcNow;
            }

            ticket.Status = InventoryCheckTicketStatus.WaitingForApproval;
            ticket.FilledByUserId = userId;
            ticket.FilledAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // SignalR notification
            await _hub.Clients.Group($"Branch_{branchId.Value}")
                .SendAsync("InventoryCheckFilled", new
                {
                    ticketId = ticket.TicketId,
                    ticketCode = ticket.TicketCode,
                    branchId = ticket.BranchId,
                    filledBy = ticket.FilledByUserId
                });

            var updatedTicket = await _context.InventoryCheckTickets
                .Include(t => t.Details)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(t => t.TicketId == id);

            return Ok(new ApiResponse<InventoryCheckResponseDto>
            {
                Success = true,
                Message = "Số lượng đếm thực tế đã được cập nhật.",
                Data = MapToResponseDto(updatedTicket!)
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
    [HasPermission(PermissionConstants.STOCK_CHECK_APPROVE)]
    public async Task<IActionResult> ApproveInventoryCheck(Guid id)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var branchId = _currentBranch.GetBranchId();
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

            // VALIDATION: Check if all details are counted
            var uncountedDetails = ticket.Details.Where(d => !d.IsCounted).ToList();
            if (uncountedDetails.Any())
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Còn sản phẩm chưa được nhập số lượng đếm thực tế.",
                    Errors = new() { $"{uncountedDetails.Count} sản phẩm chưa được count. Vui lòng nhập đầy đủ trước khi duyệt." }
                });
            }

            var productIds = ticket.Details.Select(detail => detail.ProductId).Distinct().ToList();
            var branchProducts = await _context.BranchProducts
                .Where(bp => bp.BranchId == branchId.Value && productIds.Contains(bp.ProductId))
                .ToDictionaryAsync(bp => bp.ProductId);

            // Generate StockTicket for adjustments
            var stockTicket = new StockTicket
            {
                StockTicketId = Guid.NewGuid(),
                TicketCode = await GenerateStockTicketCodeAsync(),
                BranchId = branchId.Value,
                UserId = userId.Value,
                TicketType = "ADJUSTMENT",
                Status = "COMPLETED",
                Notes = $"Điều chỉnh tồn kho từ phiếu kiểm kê {ticket.TicketCode}",
                CreatedAt = DateTime.UtcNow
            };
            await _context.StockTickets.AddAsync(stockTicket);

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

                // Update branch product stock
                branchProduct.ActualStock = Convert.ToDecimal(actualQty);
                branchProduct.AvailableStock = Convert.ToDecimal(actualQty);

                // Create StockTicketItem if there's a discrepancy
                if (detail.Discrepancy != 0)
                {
                    var stockTicketItem = new StockTicketItem
                    {
                        TicketItemId = Guid.NewGuid(),
                        StockTicketId = stockTicket.StockTicketId,
                        BranchProductId = branchProduct.BranchProductId,
                        SystemQuantity = Convert.ToDecimal(detail.SystemQuantity),
                        ActualQuantity = Convert.ToDecimal(detail.ActualQuantity),
                        Discrepancy = Convert.ToDecimal(detail.Discrepancy)
                    };
                    await _context.StockTicketItems.AddAsync(stockTicketItem);
                }
            }

            ticket.Status = InventoryCheckTicketStatus.Completed;
            ticket.ApprovedByUserId = userId;
            ticket.ApprovedAt = DateTime.UtcNow;
            ticket.LinkedStockTicketId = stockTicket.StockTicketId;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // SignalR notification
            await _hub.Clients.Group($"Branch_{branchId.Value}")
                .SendAsync("InventoryCheckApproved", new
                {
                    ticketId = ticket.TicketId,
                    ticketCode = ticket.TicketCode,
                    branchId = ticket.BranchId,
                    approvedBy = ticket.ApprovedByUserId,
                    linkedStockTicketId = stockTicket.StockTicketId
                });

            var updatedTicket = await _context.InventoryCheckTickets
                .Include(t => t.Details)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(t => t.TicketId == id);

            return Ok(new ApiResponse<InventoryCheckResponseDto>
            {
                Success = true,
                Message = "Phiếu kiểm kê đã được duyệt và tồn kho đã được điều chỉnh.",
                Data = MapToResponseDto(updatedTicket!)
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
    [HasPermission(PermissionConstants.STOCK_CHECK_CANCEL)]
    public async Task<IActionResult> CancelInventoryCheck(Guid id, [FromBody] CancelInventoryCheckDto? dto)
    {
        try
        {
            var branchId = _currentBranch.GetBranchId();
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
            ticket.CancelledByUserId = userId;
            ticket.CancelledAt = DateTime.UtcNow;
            ticket.CancelledReason = dto?.Reason;

            await _context.SaveChangesAsync();

            // SignalR notification
            await _hub.Clients.Group($"Branch_{branchId.Value}")
                .SendAsync("InventoryCheckCancelled", new
                {
                    ticketId = ticket.TicketId,
                    ticketCode = ticket.TicketCode,
                    branchId = ticket.BranchId,
                    cancelledBy = ticket.CancelledByUserId
                });

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

    [HttpPost("{id:guid}/reasons")]
    [HasPermission(PermissionConstants.STOCK_CHECK_APPROVE)]
    public async Task<IActionResult> UpdateDiscrepancyReasons(Guid id, [FromBody] InventoryCheckReasonDto dto)
    {
        try
        {
            var branchId = _currentBranch.GetBranchId();
            if (branchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy thông tin chi nhánh trong Token.",
                    Errors = new() { "branchId claim is required." }
                });
            }

            if (dto == null || dto.Details == null || dto.Details.Count == 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Dữ liệu cập nhật giải trình không hợp lệ.",
                    Errors = new() { "Details là bắt buộc và không thể rỗng." }
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

            if (ticket.Status != InventoryCheckTicketStatus.WaitingForApproval && ticket.Status != InventoryCheckTicketStatus.Completed)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Chỉ có thể nhập giải trình cho phiếu đang chờ duyệt hoặc đã hoàn tất.",
                    Errors = new() { "Phiếu phải ở trạng thái WaitingForApproval hoặc Completed." }
                });
            }

            var detailMap = ticket.Details.ToDictionary(d => d.DetailId);
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
                detail.ReasonNote = update.ReasonNote;
            }

            await _context.SaveChangesAsync();

            var updatedTicket = await _context.InventoryCheckTickets
                .Include(t => t.Details)
                    .ThenInclude(d => d.Product)
                .FirstOrDefaultAsync(t => t.TicketId == id);

            return Ok(new ApiResponse<InventoryCheckResponseDto>
            {
                Success = true,
                Message = "Giải trình chênh lệch đã được cập nhật.",
                Data = MapToResponseDto(updatedTicket!)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi cập nhật giải trình.",
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
            FilledByUserId = ticket.FilledByUserId,
            FilledAt = ticket.FilledAt,
            ApprovedByUserId = ticket.ApprovedByUserId,
            ApprovedAt = ticket.ApprovedAt,
            CancelledByUserId = ticket.CancelledByUserId,
            CancelledAt = ticket.CancelledAt,
            CancelledReason = ticket.CancelledReason,
            AssigneeUserId = ticket.AssigneeUserId,
            LinkedStockTicketId = ticket.LinkedStockTicketId,
            Notes = ticket.Notes,
            Details = ticket.Details.Select(detail => new InventoryCheckDetailDto
            {
                DetailId = detail.DetailId,
                ProductId = detail.ProductId,
                ProductCode = detail.Product?.ProductCode,
                ProductName = detail.Product?.ProductName,
                SystemQuantity = detail.SystemQuantity,
                ActualQuantity = detail.ActualQuantity,
                Discrepancy = ticket.Status == InventoryCheckTicketStatus.Draft ? 0 : detail.Discrepancy,
                IsCounted = detail.IsCounted,
                ReasonNote = detail.ReasonNote,
                CountedByUserId = detail.CountedByUserId,
                CountedAt = detail.CountedAt
            }).ToList()
        };
    }

    private async Task<string> GenerateTicketCodeAsync()
    {
        const string prefix = "KKK";
        const int maxRetries = 5;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
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

                var newCode = prefix + nextSequence.ToString("D5");

                // Check if exists (race condition check)
                var exists = await _context.InventoryCheckTickets
                    .AnyAsync(t => t.TicketCode == newCode);

                if (!exists)
                {
                    return newCode;
                }
            }
            catch (DbUpdateException)
            {
                // Retry on conflict
                if (attempt == maxRetries - 1) throw;
            }
        }

        // Fallback: use GUID-based code
        return prefix + Guid.NewGuid().ToString("N")[..8].ToUpper();
    }

    private async Task<string> GenerateStockTicketCodeAsync()
    {
        const string prefix = "ADJ";
        const int maxRetries = 5;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var lastCode = await _context.StockTickets
                    .Where(t => t.TicketCode != null && t.TicketCode.StartsWith(prefix))
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

                var newCode = prefix + "-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + nextSequence.ToString("D4");

                var exists = await _context.StockTickets
                    .AnyAsync(t => t.TicketCode == newCode);

                if (!exists)
                {
                    return newCode;
                }
            }
            catch (DbUpdateException)
            {
                if (attempt == maxRetries - 1) throw;
            }
        }

        return prefix + "-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
    }
}

public class CancelInventoryCheckDto
{
    public string? Reason { get; set; }
}
