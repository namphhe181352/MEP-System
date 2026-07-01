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
[Tags("Kiểm Kê Kho (Check Inventory)")]
public class InventoryCheckController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly IHubContext<InventoryHub> _hub;
    private readonly ICurrentBranchAccessor _currentBranch;

    public InventoryCheckController(
        MEPDatabaseContext context,
        IHubContext<InventoryHub> hub,
        ICurrentBranchAccessor currentBranch)
    {
        _context = context;
        _hub = hub;
        _currentBranch = currentBranch;
    }

    #region Helper Methods

    private Guid? GetUserId()
    {
        var userClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(userClaim, out var userId) ? userId : null;
    }

    private bool IsOwner()
    {
        return User.IsInRole(PermissionConstants.OWNER_ROLE);
    }

    private string? GetUserName()
    {
        return User.FindFirst("name")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
    }

    private async Task<Notification> CreateAndSaveNotification(
        NotificationType type,
        Guid? recipientUserId,
        Guid branchId,
        Guid? inventoryCheckId,
        string message,
        Guid triggeredByUserId)
    {
        var notification = new Notification
        {
            NotificationId = Guid.NewGuid(),
            Type = type,
            RecipientUserId = recipientUserId,
            BranchId = branchId,
            InventoryCheckId = inventoryCheckId,
            TriggeredByUserId = triggeredByUserId,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        return notification;
    }

    private async Task BroadcastAndSaveNotification(
        NotificationType type,
        Guid branchId,
        Guid? inventoryCheckId,
        string message,
        Guid triggeredByUserId)
    {
        var notification = await CreateAndSaveNotification(type, null, branchId, inventoryCheckId, message, triggeredByUserId);
        await _context.SaveChangesAsync();

        // SignalR broadcast to branch group
        await _hub.Clients.Group($"Branch_{branchId}").SendAsync("ReceiveNotification", new
        {
            notificationId = notification.NotificationId,
            type = type.ToString(),
            message = message,
            inventoryCheckId = inventoryCheckId,
            createdAt = notification.CreatedAt
        });
    }

    private async Task SendNotificationToUser(
        Guid recipientUserId,
        NotificationType type,
        Guid branchId,
        Guid inventoryCheckId,
        string message,
        Guid triggeredByUserId)
    {
        var notification = await CreateAndSaveNotification(type, recipientUserId, branchId, inventoryCheckId, message, triggeredByUserId);
        await _context.SaveChangesAsync();

        // SignalR send to specific user
        await _hub.Clients.User(recipientUserId.ToString()).SendAsync("ReceiveNotification", new
        {
            notificationId = notification.NotificationId,
            type = type.ToString(),
            message = message,
            inventoryCheckId = inventoryCheckId,
            createdAt = notification.CreatedAt
        });
    }

    #endregion

    #region Inventory Check CRUD

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
                .Include(t => t.Details)
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
                query = query.Where(ticket => ticket.CreatedAt >= startDate.Value.Date);

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
                    CreatedByUserName = ticket.CreatedByUser != null ? ticket.CreatedByUser.Username : null,
                    AssigneeUserId = ticket.AssigneeUserId,
                    AssigneeUserName = ticket.AssigneeUser != null ? ticket.AssigneeUser.Username : null,
                    RecountNumber = 0
                })
                .ToListAsync();

            return Ok(new ApiResponse<PaginatedResponse<InventoryCheckListItemDto>>
            {
                Success = true,
                Message = "Danh sách phiếu kiểm kê đã được tải.",
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
                .Include(t => t.Details)
                    .ThenInclude(d => d.Product)
                .Include(t => t.CreatedByUser)
                .Include(t => t.AssigneeUser)
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
        using var transaction = await _context.Database.BeginTransactionAsync();
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

            var userName = GetUserName();
            var isOwner = IsOwner();

            // ============================================================
            // QUY TẮC 1: Xử lý AssigneeUserId
            // Owner: được phép gán cho người khác
            // Staff: phải tự gán cho mình, không được gán cho người khác
            // ============================================================
            Guid? resolvedAssigneeUserId = dto.AssigneeUserId;

            if (!isOwner)
            {
                // Staff: chỉ được gán cho chính mình
                if (resolvedAssigneeUserId.HasValue && resolvedAssigneeUserId != userId)
                {
                    // Reject: Staff cố tình gán cho người khác
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Bạn không có quyền gán phiếu kiểm kê cho người khác.",
                        Errors = new() { "Staff chỉ có thể tự gán phiếu cho mình." }
                    });
                }
                // Staff luôn tự gán cho mình
                resolvedAssigneeUserId = userId;
            }

            // Validate: Check if any products are already in active inventory checks
            var existingActiveChecks = await _context.InventoryCheckTickets
                .Where(t => t.BranchId == branchId.Value &&
                           (t.Status == InventoryCheckTicketStatus.Draft || t.Status == InventoryCheckTicketStatus.WaitingForApproval))
                .SelectMany(t => t.Details)
                .Where(d => dto.ProductIds.Contains(d.ProductId))
                .Select(d => d.ProductId)
                .Distinct()
                .ToListAsync();

            if (existingActiveChecks.Count > 0)
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
            if (missingProductIds.Count > 0)
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
                AssigneeUserId = resolvedAssigneeUserId,
                Notes = dto.Notes,
                RecountNumber = 0
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

            _context.InventoryCheckTickets.Add(ticket);
            await _context.SaveChangesAsync();

            // ============================================================
            // QUY TẮC 4: Tạo thông báo
            // ============================================================
            // a) Thông báo cho Assignee (nếu giao cho người khác)
            if (resolvedAssigneeUserId.HasValue && resolvedAssigneeUserId != userId)
            {
                await SendNotificationToUser(
                    resolvedAssigneeUserId.Value,
                    NotificationType.Assigned,
                    branchId.Value,
                    ticket.TicketId,
                    $"Bạn được giao kiểm kê phiếu {ticket.TicketCode}.",
                    userId.Value);
            }

            // b) Broadcast cho cả chi nhánh
            await BroadcastAndSaveNotification(
                NotificationType.Created,
                branchId.Value,
                ticket.TicketId,
                $"{userName} vừa tạo phiếu kiểm kê {ticket.TicketCode}.",
                userId.Value);

            await transaction.CommitAsync();

            // Reload với navigation properties
            var createdTicket = await _context.InventoryCheckTickets
                .Include(t => t.Details).ThenInclude(d => d.Product)
                .Include(t => t.CreatedByUser)
                .Include(t => t.AssigneeUser)
                .FirstAsync(t => t.TicketId == ticket.TicketId);

            return CreatedAtAction(nameof(GetInventoryCheckById), new { id = ticket.TicketId },
                new ApiResponse<InventoryCheckResponseDto>
                {
                    Success = true,
                    Message = "Phiếu kiểm kê đã được tạo.",
                    Data = MapToResponseDto(createdTicket)
                });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
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

            var isOwner = IsOwner();

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

            // Update assignee - Staff không được gán cho người khác
            if (dto.AssigneeUserId.HasValue)
            {
                if (!isOwner && dto.AssigneeUserId != userId)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Bạn không có quyền gán phiếu kiểm kê cho người khác.",
                        Errors = new() { "Staff chỉ có thể tự gán phiếu cho mình." }
                    });
                }
                ticket.AssigneeUserId = dto.AssigneeUserId;
            }

            // Add products
            if (dto.AddProductIds != null && dto.AddProductIds.Count > 0)
            {
                var existingProductIds = ticket.Details.Select(d => d.ProductId).ToHashSet();
                var newProductIds = dto.AddProductIds.Except(existingProductIds).ToList();

                if (newProductIds.Count > 0)
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
                    if (conflictingIds.Count > 0)
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
            if (dto.RemoveProductIds != null && dto.RemoveProductIds.Count > 0)
            {
                var detailsToRemove = ticket.Details.Where(d => dto.RemoveProductIds.Contains(d.ProductId)).ToList();
                _context.InventoryCheckDetails.RemoveRange(detailsToRemove);
            }

            await _context.SaveChangesAsync();

            // Reload with product details
            var updatedTicket = await _context.InventoryCheckTickets
                .Include(t => t.Details).ThenInclude(d => d.Product)
                .Include(t => t.CreatedByUser)
                .Include(t => t.AssigneeUser)
                .FirstAsync(t => t.TicketId == id);

            return Ok(new ApiResponse<InventoryCheckResponseDto>
            {
                Success = true,
                Message = "Phiếu kiểm kê đã được cập nhật.",
                Data = MapToResponseDto(updatedTicket)
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

            var isOwner = IsOwner();

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
                Data = (object?)null
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

    #endregion

    #region Fill / Submit

    [HttpPut("{id:guid}/fill")]
    [HasPermission(PermissionConstants.STOCK_CHECK_CREATE)]
    public async Task<IActionResult> FillActualQuantities(Guid id, [FromBody] FillActualQuantityDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
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

            var userName = GetUserName();
            var isOwner = IsOwner();

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

            // ============================================================
            // QUY TẮC 2: Chỉ Assignee hoặc Owner mới được fill
            // ============================================================
            if (ticket.Status != InventoryCheckTicketStatus.Draft)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu kiểm kê đã chuyển trạng thái và không thể cập nhật.",
                    Errors = new() { "Chỉ phiếu Draft mới có thể nhập số lượng." }
                });
            }

            if (ticket.AssigneeUserId.HasValue && ticket.AssigneeUserId != userId && !isOwner)
            {
                return Forbid();
            }

            var detailMap = ticket.Details.ToDictionary(detail => detail.DetailId);
            var invalidIds = dto.Details.Where(item => !detailMap.ContainsKey(item.DetailId)).Select(item => item.DetailId).ToList();
            if (invalidIds.Count > 0)
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

                // ============================================================
                // QUY TẮC QUAN TRỌNG: Chốt SystemQuantity TẠI THỜI ĐIỂM FILL
                // Vì khoảng cách giữa tạo phiếu (Create) và lúc thực sự đi đếm (Fill)
                // có thể đã phát sinh giao dịch bán/nhập hàng.
                // SystemQuantity phải phản ánh tồn kho THỰC TẾ tại thời điểm đếm,
                // không phải tồn kho tại thời điểm tạo phiếu.
                // ============================================================
                var branchProduct = await _context.BranchProducts
                    .FirstOrDefaultAsync(bp => bp.ProductId == detail.ProductId && bp.BranchId == branchId.Value);

                if (branchProduct != null)
                {
                    // Cập nhật SystemQuantity = tồn kho THỰC TẾ tại thời điểm Fill
                    detail.SystemQuantity = Convert.ToInt32(Math.Floor(branchProduct.ActualStock));
                }

                detail.ActualQuantity = update.ActualQuantity;
                // Discrepancy = Số đếm được - Tồn kho tại thời điểm đếm
                detail.Discrepancy = detail.ActualQuantity - detail.SystemQuantity;
                detail.IsCounted = true;
                detail.CountedByUserId = userId;
                detail.CountedAt = DateTime.UtcNow;
            }

            ticket.Status = InventoryCheckTicketStatus.WaitingForApproval;
            ticket.FilledByUserId = userId;
            ticket.FilledAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Tính tổng chênh lệch để thông báo
            var totalDiscrepancy = ticket.Details.Sum(d => Math.Abs(d.Discrepancy));

            // ============================================================
            // QUY TẮC 4c: Thông báo khi Submit
            // ============================================================
            // c) Thông báo cho Owner/người có quyền approve trong chi nhánh
            // Lấy danh sách Owner trong chi nhánh
            var ownerUserIds = await _context.UserPermissions
                .Where(up => up.Permission != null && up.Permission.Code == PermissionConstants.STOCK_CHECK_APPROVE)
                .Select(up => up.UserId)
                .Distinct()
                .ToListAsync();

            // Broadcast cho tất cả Owner trong chi nhánh
            foreach (var ownerId in ownerUserIds)
            {
                await SendNotificationToUser(
                    ownerId,
                    NotificationType.Submitted,
                    branchId.Value,
                    ticket.TicketId,
                    $"{userName} vừa gửi duyệt phiếu {ticket.TicketCode}, chênh lệch: {totalDiscrepancy}.",
                    userId.Value);
            }

            await transaction.CommitAsync();

            var updatedTicket = await _context.InventoryCheckTickets
                .Include(t => t.Details).ThenInclude(d => d.Product)
                .Include(t => t.CreatedByUser)
                .Include(t => t.AssigneeUser)
                .FirstAsync(t => t.TicketId == id);

            return Ok(new ApiResponse<InventoryCheckResponseDto>
            {
                Success = true,
                Message = "Số lượng đếm thực tế đã được cập nhật và gửi duyệt.",
                Data = MapToResponseDto(updatedTicket)
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi cập nhật số lượng thực tế.",
                Errors = new() { ex.Message }
            });
        }
    }

    #endregion

    #region Approve / Reject / Cancel

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

            var userName = GetUserName();

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

            // ============================================================
            // QUY TẮC 2: Chỉ được Approve khi Status == WaitingForApproval
            // ============================================================
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

            if (ticket.Status == InventoryCheckTicketStatus.Draft)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu chưa được nhập số lượng.",
                    Errors = new() { "Vui lòng nhập số lượng thực tế trước khi duyệt." }
                });
            }

            if (ticket.Details.Count == 0)
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
            if (uncountedDetails.Count > 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Còn sản phẩm chưa được nhập số lượng đếm thực tế.",
                    Errors = new() { $"{uncountedDetails.Count} sản phẩm chưa được count. Vui lòng nhập đầy đủ trước khi duyệt." }
                });
            }

            // ============================================================
            // QUY TẮC 3: Xử lý chênh lệch tồn kho AN TOÀN
            //
            // Tại sao cần công thức này:
            // - Giữa lúc nhân viên Fill (đếm xong) và Owner bấm Approve,
            //   có thể phát sinh giao dịch BÁN/NHẬP mới.
            // - Ta KHÔNG muốn mất những giao dịch đó.
            //
            // Công thức ĐÚNG:
            //   discrepancy = counted - system_at_count_time
            //   new_stock = current_stock_at_approve + discrepancy
            //
            // Ví dụ KiotViet:
            //   Tạo phiếu lúc tồn=100 (SystemQuantity=100)
            //   Bán -20 trước khi đếm (tồn còn 80)
            //   Đếm ra 80 (SystemQuantity tại Fill = 80, Counted=80, Discrepancy=0)
            //   Owner bấm Approve lúc tồn đã là 75 (bán thêm -5 trong lúc chờ duyệt)
            //   → new_stock = 75 + 0 = 75 (KHÔNG phải 80)
            // ============================================================
            var productIds = ticket.Details.Select(d => d.ProductId).Distinct().ToList();

            // Query lại dữ liệu tồn kho mới nhất tại thời điểm Approve
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
                Notes = $"Điều chỉnh tồn kho từ phiếu kiểm kê {ticket.TicketCode} (Phiên đếm #{ticket.RecountNumber + 1})",
                CreatedAt = DateTime.UtcNow
            };
            _context.StockTickets.Add(stockTicket);

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

                if (detail.ActualQuantity < 0)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Số lượng đếm thực tế không hợp lệ.",
                        Errors = new() { $"Sản phẩm {detail.ProductId} có ActualQuantity < 0." }
                    });
                }

                // ============================================================
                // Tính toán chênh lệch AN TOÀN - CÔNG THỨC ĐÚNG:
                //
                // detail.SystemQuantity = tồn kho tại thời điểm FILL (đã được chốt)
                // detail.ActualQuantity = số lượng NHÂN VIÊN ĐẾM ĐƯỢC
                // branchProduct.ActualStock = tồn kho HIỆN TẠI tại thời điểm APPROVE
                //
                // Bước 1: Tính discrepancy = số đếm được - tồn lúc đếm
                //   (đây là chênh lệch DO KIỂM KÊ phát hiện ra)
                var discrepancy = detail.ActualQuantity - detail.SystemQuantity;

                // Bước 2: new_stock = tồn hiện tại + discrepancy
                //   CỘNG DỒN, không ghi đè
                //   Điều này giữ nguyên mọi giao dịch phát sinh giữa Fill và Approve
                var newActualStock = branchProduct.ActualStock + discrepancy;

                // Bước 3: Cập nhật tồn kho
                branchProduct.ActualStock = newActualStock;
                branchProduct.AvailableStock = newActualStock;

                // Ghi nhận điều chỉnh vào StockTicket nếu có chênh lệch
                if (discrepancy != 0)
                {
                    var stockTicketItem = new StockTicketItem
                    {
                        TicketItemId = Guid.NewGuid(),
                        StockTicketId = stockTicket.StockTicketId,
                        BranchProductId = branchProduct.BranchProductId,
                        SystemQuantity = detail.SystemQuantity,
                        ActualQuantity = detail.ActualQuantity,
                        Discrepancy = discrepancy // Phần chênh lệch do kiểm kê
                    };
                    _context.StockTicketItems.Add(stockTicketItem);
                }
            }

            ticket.Status = InventoryCheckTicketStatus.Completed;
            ticket.ApprovedByUserId = userId;
            ticket.ApprovedAt = DateTime.UtcNow;
            ticket.LinkedStockTicketId = stockTicket.StockTicketId;

            await _context.SaveChangesAsync();

            // ============================================================
            // QUY TẮC 4e: Thông báo khi Approve
            // ============================================================
            // e) Thông báo cho Assignee: "Phiếu {code} đã được duyệt"
            if (ticket.AssigneeUserId.HasValue)
            {
                await SendNotificationToUser(
                    ticket.AssigneeUserId.Value,
                    NotificationType.Approved,
                    branchId.Value,
                    ticket.TicketId,
                    $"Phiếu {ticket.TicketCode} đã được duyệt bởi {userName}.",
                    userId.Value);
            }
            else
            {
                // Broadcast nếu không có assignee cụ thể
                await BroadcastAndSaveNotification(
                    NotificationType.Approved,
                    branchId.Value,
                    ticket.TicketId,
                    $"Phiếu {ticket.TicketCode} đã được duyệt bởi {userName}.",
                    userId.Value);
            }

            await transaction.CommitAsync();

            var updatedTicket = await _context.InventoryCheckTickets
                .Include(t => t.Details).ThenInclude(d => d.Product)
                .Include(t => t.CreatedByUser)
                .Include(t => t.AssigneeUser)
                .FirstAsync(t => t.TicketId == id);

            return Ok(new ApiResponse<InventoryCheckResponseDto>
            {
                Success = true,
                Message = "Phiếu kiểm kê đã được duyệt và tồn kho đã được điều chỉnh.",
                Data = MapToResponseDto(updatedTicket)
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

    [HttpPost("{id:guid}/reject")]
    [HasPermission(PermissionConstants.STOCK_CHECK_APPROVE)]
    public async Task<IActionResult> RejectOrRequestRecount(Guid id, [FromBody] RequestRecountDto dto)
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

            var userName = GetUserName();

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

            // ============================================================
            // QUY TẮC 2: Chỉ được Reject/RequestRecount khi Status == WaitingForApproval
            // ============================================================
            if (ticket.Status != InventoryCheckTicketStatus.WaitingForApproval)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Chỉ phiếu đang chờ duyệt mới có thể yêu cầu đếm lại.",
                    Errors = new() { "Chỉ phiếu WaitingForApproval mới có thể yêu cầu đếm lại." }
                });
            }

            // Kiểm tra lý do bắt buộc
            if (string.IsNullOrWhiteSpace(dto?.Reason))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lý do đếm lại là bắt buộc.",
                    Errors = new() { "Vui lòng nhập lý do yêu cầu đếm lại." }
                });
            }

            // Cập nhật thông tin recount
            ticket.RecountNumber += 1;
            ticket.RecountRequestedAt = DateTime.UtcNow;
            ticket.RecountRequestedByUserId = userId;
            ticket.RecountReason = dto.Reason;

            // Reset trạng thái về Draft để đếm lại
            // Nhưng vẫn giữ lại số lượng đã đếm trước đó để tham khảo
            ticket.Status = InventoryCheckTicketStatus.Draft;

            // Reset IsCounted để buộc phải đếm lại tất cả
            foreach (var detail in ticket.Details)
            {
                detail.IsCounted = false;
            }

            ticket.FilledByUserId = null;
            ticket.FilledAt = null;

            await _context.SaveChangesAsync();

            // ============================================================
            // QUY TẮC 4d: Thông báo khi Reject/RequestRecount
            // ============================================================
            // d) Thông báo cho Assignee: "Phiếu {code} cần đếm lại: {lý do}"
            if (ticket.AssigneeUserId.HasValue)
            {
                await SendNotificationToUser(
                    ticket.AssigneeUserId.Value,
                    NotificationType.RecountRequested,
                    branchId.Value,
                    ticket.TicketId,
                    $"Phiếu {ticket.TicketCode} cần đếm lại: {dto.Reason}",
                    userId.Value);
            }
            else if (ticket.CreatedByUserId != userId)
            {
                // Thông báo cho người tạo nếu không phải người reject
                await SendNotificationToUser(
                    ticket.CreatedByUserId,
                    NotificationType.RecountRequested,
                    branchId.Value,
                    ticket.TicketId,
                    $"Phiếu {ticket.TicketCode} cần đếm lại: {dto.Reason}",
                    userId.Value);
            }

            await transaction.CommitAsync();

            var updatedTicket = await _context.InventoryCheckTickets
                .Include(t => t.Details).ThenInclude(d => d.Product)
                .Include(t => t.CreatedByUser)
                .Include(t => t.AssigneeUser)
                .FirstAsync(t => t.TicketId == id);

            return Ok(new ApiResponse<InventoryCheckResponseDto>
            {
                Success = true,
                Message = $"Yêu cầu đếm lại đã được gửi. Phiếu đã chuyển về trạng thái Draft (Phiếu đếm lần #{ticket.RecountNumber}).",
                Data = MapToResponseDto(updatedTicket)
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi yêu cầu đếm lại.",
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
                Data = (object?)null
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

    #endregion

    #region Discrepancy Reasons

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
            if (invalidIds.Count > 0)
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
                .Include(t => t.Details).ThenInclude(d => d.Product)
                .Include(t => t.CreatedByUser)
                .Include(t => t.AssigneeUser)
                .FirstAsync(t => t.TicketId == id);

            return Ok(new ApiResponse<InventoryCheckResponseDto>
            {
                Success = true,
                Message = "Giải trình chênh lệch đã được cập nhật.",
                Data = MapToResponseDto(updatedTicket)
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

    #endregion

    #region Notifications

    [HttpGet("notifications")]
    [HasPermission(PermissionConstants.STOCK_CHECK_VIEW)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool unreadOnly = false)
    {
        try
        {
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

            if (pageNumber < 1 || pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Tham số phân trang không hợp lệ.",
                    Errors = new() { "pageNumber must be >= 1 and pageSize must be between 1 and 100." }
                });
            }

            // Query: lấy thông báo của user HOẶC broadcast cho chi nhánh
            var query = _context.Notifications
                .Include(n => n.TriggeredByUser)
                .Include(n => n.InventoryCheck)
                .Where(n => n.BranchId == branchId.Value &&
                           (n.RecipientUserId == userId || n.RecipientUserId == null))
                .AsQueryable();

            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            var totalCount = await query.CountAsync();
            var unreadCount = await _context.Notifications
                .Where(n => n.BranchId == branchId.Value &&
                           (n.RecipientUserId == userId || n.RecipientUserId == null) &&
                           !n.IsRead)
                .CountAsync();

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new NotificationDto
                {
                    NotificationId = n.NotificationId,
                    Type = n.Type.ToString(),
                    TypeDisplay = GetNotificationTypeDisplay(n.Type),
                    RecipientUserId = n.RecipientUserId,
                    RecipientUserName = n.RecipientUser != null ? n.RecipientUser.Username : null,
                    BranchId = n.BranchId,
                    InventoryCheckId = n.InventoryCheckId,
                    InventoryCheckCode = n.InventoryCheck != null ? n.InventoryCheck.TicketCode : null,
                    TriggeredByUserId = n.TriggeredByUserId,
                    TriggeredByUserName = n.TriggeredByUser != null ? n.TriggeredByUser.Username : null,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();

            return Ok(new ApiResponse<NotificationListDto>
            {
                Success = true,
                Message = "Danh sách thông báo đã được tải.",
                Data = new NotificationListDto
                {
                    Items = items,
                    TotalCount = totalCount,
                    UnreadCount = unreadCount,
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
                Message = "Lỗi máy chủ khi tải thông báo.",
                Errors = new() { ex.Message }
            });
        }
    }

    [HttpPut("notifications/read")]
    [HasPermission(PermissionConstants.STOCK_CHECK_VIEW)]
    public async Task<IActionResult> MarkNotificationsAsRead([FromBody] MarkNotificationsReadDto dto)
    {
        try
        {
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

            if (dto.MarkAllAsRead)
            {
                // Đánh dấu tất cả là đã đọc
                var unreadNotifications = await _context.Notifications
                    .Where(n => n.BranchId == branchId.Value &&
                               (n.RecipientUserId == userId || n.RecipientUserId == null) &&
                               !n.IsRead)
                    .ToListAsync();

                foreach (var n in unreadNotifications)
                {
                    n.IsRead = true;
                }
            }
            else if (dto.NotificationIds != null && dto.NotificationIds.Count > 0)
            {
                // Đánh dấu các notification được chọn
                var notifications = await _context.Notifications
                    .Where(n => dto.NotificationIds.Contains(n.NotificationId) &&
                               (n.RecipientUserId == userId || n.RecipientUserId == null))
                    .ToListAsync();

                foreach (var n in notifications)
                {
                    n.IsRead = true;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = dto.MarkAllAsRead ? "Tất cả thông báo đã được đánh dấu là đã đọc." : "Thông báo đã được đánh dấu là đã đọc.",
                Data = (object?)null
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi máy chủ khi cập nhật thông báo.",
                Errors = new() { ex.Message }
            });
        }
    }

    private static string GetNotificationTypeDisplay(NotificationType type)
    {
        return type switch
        {
            NotificationType.Assigned => "Được giao việc",
            NotificationType.Submitted => "Gửi duyệt",
            NotificationType.Approved => "Đã duyệt",
            NotificationType.RecountRequested => "Yêu cầu đếm lại",
            NotificationType.Created => "Tạo mới",
            _ => type.ToString()
        };
    }

    #endregion

    #region Helper Methods

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
            CreatedByUserName = ticket.CreatedByUser?.Username,
            FilledByUserId = ticket.FilledByUserId,
            FilledByUserName = ticket.FilledByUser?.Username,
            FilledAt = ticket.FilledAt,
            ApprovedByUserId = ticket.ApprovedByUserId,
            ApprovedByUserName = ticket.ApprovedByUser?.Username,
            ApprovedAt = ticket.ApprovedAt,
            CancelledByUserId = ticket.CancelledByUserId,
            CancelledByUserName = ticket.CancelledByUser?.Username,
            CancelledAt = ticket.CancelledAt,
            CancelledReason = ticket.CancelledReason,
            AssigneeUserId = ticket.AssigneeUserId,
            AssigneeUserName = ticket.AssigneeUser?.Username,
            LinkedStockTicketId = ticket.LinkedStockTicketId,
            Notes = ticket.Notes,
            RecountNumber = ticket.RecountNumber,
            RecountRequestedAt = ticket.RecountRequestedAt,
            RecountRequestedByUserId = ticket.RecountRequestedByUserId,
            RecountRequestedByUserName = ticket.RecountRequestedByUser?.Username,
            RecountReason = ticket.RecountReason,
            Details = ticket.Details.Select(d => new InventoryCheckDetailDto
            {
                DetailId = d.DetailId,
                ProductId = d.ProductId,
                ProductCode = d.Product?.ProductCode,
                ProductName = d.Product?.ProductName,
                SystemQuantity = d.SystemQuantity,
                ActualQuantity = d.ActualQuantity,
                Discrepancy = d.Discrepancy,
                IsCounted = d.IsCounted,
                ReasonNote = d.ReasonNote,
                CountedByUserId = d.CountedByUserId,
                CountedAt = d.CountedAt
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

                var exists = await _context.InventoryCheckTickets
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
                if (!string.IsNullOrWhiteSpace(lastCode))
                {
                    var lastSeqPart = lastCode.Split('-').LastOrDefault();
                    if (!string.IsNullOrEmpty(lastSeqPart) && int.TryParse(lastSeqPart, out var parsedNumber))
                    {
                        nextSequence = parsedNumber + 1;
                    }
                }

                var newCode = $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{nextSequence:D4}";

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

        return $"{prefix}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    #endregion
}

#region DTOs for backward compatibility

public class CancelInventoryCheckDto
{
    public string? Reason { get; set; }
}

#endregion
