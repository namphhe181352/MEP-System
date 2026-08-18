using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Hubs;
using MEP_Management_System.Models;
using CancelTicketDto = MEP_Management_System.DTOs.InventoryDTOs.CancelInventoryCheckDto;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.InventoryServices;

public class InventoryCheckServiceImpl : IInventoryCheckService
{
    private readonly MEPDatabaseContext _context;
    private readonly ICurrentBranchAccessor _currentBranch;
    private readonly IHubContext<MepHub> _hub;

    public InventoryCheckServiceImpl(
        MEPDatabaseContext context,
        ICurrentBranchAccessor currentBranch,
        IHubContext<MepHub> hub)
    {
        _context = context;
        _currentBranch = currentBranch;
        _hub = hub;
    }

    private Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst("userId")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
    }

    private bool IsOwner(ClaimsPrincipal user) => user.IsInRole("Owner");

    private static DateTime UtcNow() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    private async Task TryWriteSystemLog(Guid userId, string action, string tableName,
        string entityType, Guid entityId, string description)
    {
        try
        {
            var log = new SystemLog
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                Action = action,
                TableName = tableName,
                EntityType = entityType,
                EntityId = entityId,
                Description = description,
                Level = "INFO",
                Source = "InventoryCheckService",
                Timestamp = DateTime.UtcNow
            };
            _context.SystemLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch
        {
            // Silent — khong lam fail request chinh
        }
    }

    public async Task<ApiResponse<PaginatedResponse<InventoryCheckResponseDto>>> GetInventoryChecksAsync(
        ClaimsPrincipal user, Guid? branchId, string? ticketCode, string? status, DateTime? fromDate, DateTime? toDate,
        int pageNumber, int pageSize)
    {
        var list = _context.StockTickets
            .AsNoTracking()
            .Include(st => st.User)
            .Include(st => st.RecountRequestedByUser)
            .Include(st => st.StockTicketItems)
            .Where(st => st.TicketType == "INVENTORY_CHECK");

        if (!IsOwner(user))
        {
            if (!branchId.HasValue)
                throw new UnauthorizedAccessException("JWT token is missing or invalid");
            list = list.Where(st => st.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
            list = list.Where(st => st.Status == status);

        if (!string.IsNullOrWhiteSpace(ticketCode))
        {
            var codeUpper = ticketCode.ToUpper();
            list = list.Where(st => st.TicketCode != null && st.TicketCode.ToUpper().Contains(codeUpper));
        }

        if (fromDate.HasValue)
        {
            var localFrom = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Unspecified);
            var utcFrom = TimeZoneInfo.ConvertTimeToUtc(localFrom, VietnamTime.Tz);
            list = list.Where(st => st.CreatedAt >= utcFrom);
        }

        if (toDate.HasValue)
        {
            var localTo = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Unspecified);
            var utcTo = TimeZoneInfo.ConvertTimeToUtc(localTo, VietnamTime.Tz);
            list = list.Where(st => st.CreatedAt < utcTo);
        }

        list = list.OrderByDescending(st => st.CreatedAt);
        var totalCount = await list.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        return new ApiResponse<PaginatedResponse<InventoryCheckResponseDto>>
        {
            Success = true,
            Message = "Inventory checks retrieved successfully",
            Data = new PaginatedResponse<InventoryCheckResponseDto>
            {
                Items = items.Select(MapToInventoryCheckResponse).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                HasNextPage = pageNumber < totalPages,
                HasPreviousPage = pageNumber > 1
            }
        };
    }

    public async Task<ApiResponse<InventoryCheckResponseDto>> GetInventoryCheckByIdAsync(ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        var stockTicketQuery = _context.StockTickets
            .AsNoTracking()
            .Include(st => st.User)
            .Include(st => st.RecountRequestedByUser)
            .Include(st => st.StockTicketItems).ThenInclude(item => item.BranchProduct).ThenInclude(bp => bp.Product)
            .Where(st => st.StockTicketId == id && st.TicketType == "INVENTORY_CHECK");

        if (!IsOwner(user))
        {
            if (branchId == null)
                throw new UnauthorizedAccessException("JWT token is missing or invalid");
            stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
        }

        var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

        if (stockTicket == null)
            throw new KeyNotFoundException($"Khong tim thay phieu kiem ke voi ID: {id}");

        return new ApiResponse<InventoryCheckResponseDto>
        {
            Success = true,
            Message = "Inventory check retrieved successfully",
            Data = MapToInventoryCheckResponse(stockTicket)
        };
    }

    public async Task<ApiResponse<object>> CreateInventoryCheckAsync(ClaimsPrincipal user, InventoryCheckCreateDto dto, Guid? branchIdOverride)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId == null)
            throw new UnauthorizedAccessException("Khong xac dinh duoc nguoi dung tu Token.");

        var userEntity = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == currentUserId.Value);
        Guid? resolvedBranchId = null;

        if (userEntity?.DefaultBranchId != null && userEntity.DefaultBranchId != Guid.Empty)
            resolvedBranchId = userEntity.DefaultBranchId;
        else if (branchIdOverride.HasValue)
            resolvedBranchId = branchIdOverride.Value;
        else if (_currentBranch.GetBranchId().HasValue)
            resolvedBranchId = _currentBranch.GetBranchId();
        else
        {
            var activeBranch = await _context.Branches.AsNoTracking().Where(b => b.IsActive == 1).FirstOrDefaultAsync();
            resolvedBranchId = activeBranch?.BranchId;
        }

        if (resolvedBranchId == null)
            throw new BusinessException("Khong tim thay chi nhanh hop le.");

        if (dto.Items == null || dto.Items.Count == 0)
            throw new BusinessException("Validation failed", "Phai co it nhat 1 san pham.");

        var currentTime = UtcNow();
        var ticketCode = $"CHK-{currentTime:yyyyMMddHHmmss}";

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var stockTicket = new StockTicket
            {
                StockTicketId = Guid.NewGuid(),
                TicketCode = ticketCode,
                BranchId = resolvedBranchId.Value,
                UserId = currentUserId.Value,
                TicketType = "INVENTORY_CHECK",
                Status = "Draft",
                Notes = dto.Note ?? dto.Reason,
                Reason = dto.Reason,
                CreatedAt = currentTime
            };

            _context.StockTickets.Add(stockTicket);

            foreach (var item in dto.Items)
            {
                Guid parsedId;
                bool isGuid = Guid.TryParse(item.Id, out parsedId);

                var branchProduct = await _context.BranchProducts
                    .Include(bp => bp.Product)
                    .FirstOrDefaultAsync(bp =>
                        ((isGuid && (bp.BranchProductId == parsedId || bp.ProductId == parsedId))
                        || (!isGuid && bp.Product != null && bp.Product.ProductCode == item.Id))
                        && bp.BranchId == resolvedBranchId.Value);

                if (branchProduct == null)
                    throw new KeyNotFoundException($"Product/BranchProduct with ID/Code '{item.Id}' was not found.");

                var ticketItem = new StockTicketItem
                {
                    TicketItemId = Guid.NewGuid(),
                    StockTicketId = stockTicket.StockTicketId,
                    BranchProductId = branchProduct.BranchProductId,
                    SystemQuantity = branchProduct.ActualStock,
                    ActualQuantity = 0m,
                    Discrepancy = 0m,
                    UnitPrice = branchProduct.CostPrice,
                    UnitName = branchProduct.Product?.BaseUnit ?? "",
                    WarrantyPeriod = branchProduct.Product?.WarrantyPeriod ?? 0,
                    WarrantyUnit = branchProduct.Product?.WarrantyUnit ?? "MONTH"
                };

                _context.StockTicketItems.Add(ticketItem);
            }

            await _context.SaveChangesAsync();
            
            // Create a notification for the created inventory check
            // 1. Broadcast notification for everyone in the branch
            var broadcastMsg = $"Phiếu kiểm kê mới {ticketCode} đã được tạo.";
            var broadcastNotification = new Notification
            {
                NotificationId = Guid.NewGuid(),
                Type = NotificationType.Created,
                RecipientUserId = null, // Broadcast to branch
                BranchId = resolvedBranchId.Value,
                ReferenceId = stockTicket.StockTicketId,
                ReferenceType = "InventoryCheck",
                TriggeredByUserId = currentUserId.Value,
                Message = broadcastMsg,
                IsRead = false,
                CreatedAt = currentTime
            };
            
            _context.Notifications.Add(broadcastNotification);

            // 2. Assigned notification specifically for the assignee (if any and not the creator)
            var recipient = dto.AssigneeUserId;
            if (recipient.HasValue && recipient.Value != currentUserId.Value)
            {
                var assignedNotification = new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    Type = NotificationType.Assigned,
                    RecipientUserId = recipient.Value,
                    BranchId = resolvedBranchId.Value,
                    ReferenceId = stockTicket.StockTicketId,
                    ReferenceType = "InventoryCheck",
                    TriggeredByUserId = currentUserId.Value,
                    Message = $"Bạn được phân công phiếu kiểm kê {ticketCode}.",
                    IsRead = false,
                    CreatedAt = currentTime.AddSeconds(1) // slightly after to avoid sorting clashes
                };
                _context.Notifications.Add(assignedNotification);
            }
            
            await _context.SaveChangesAsync();
            
            // Trigger a silent refresh on the frontend without a toast
            await _hub.Clients.All.SendAsync("SilentNotification");
            
            await transaction.CommitAsync();

            await TryWriteSystemLog(
                currentUserId.Value,
                "CREATE_INVENTORY_CHECK",
                "stock_tickets",
                "InventoryCheck",
                stockTicket.StockTicketId,
                $"Tạo phiếu kiểm kê kho {stockTicket.TicketCode}");

            return new ApiResponse<object>
            {
                Success = true,
                Message = "Inventory check created successfully.",
                Data = new { TicketId = stockTicket.StockTicketId, TicketCode = stockTicket.TicketCode, BranchId = stockTicket.BranchId, Status = stockTicket.Status }
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse<InventoryCheckResponseDto>> UpdateInventoryCheckAsync(ClaimsPrincipal user, Guid id, InventoryCheckUpdateDto dto, Guid? branchId)
    {
        var currentUserId = GetCurrentUserId(user);
        var stockTicketQuery = _context.StockTickets
            .Include(st => st.StockTicketItems)
            .Where(st => st.StockTicketId == id && st.TicketType == "INVENTORY_CHECK");

        if (!IsOwner(user))
        {
            if (branchId == null)
                throw new UnauthorizedAccessException("JWT token is missing or invalid");
            stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
        }

        var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

        if (stockTicket == null)
            throw new KeyNotFoundException($"Khong tim thay phieu kiem ke voi ID: {id}");

        if (stockTicket.Status == "Cancelled")
            throw new BusinessException("Cannot update cancelled ticket", "Phieu da bi huy. Khong the chinh sua.");

        if (stockTicket.Status == "Completed")
            throw new BusinessException("Cannot update completed ticket", "Phieu da duyet. Khong the chinh sua.");

        if (dto.Note != null) stockTicket.Notes = dto.Note;
        if (dto.Reason != null) stockTicket.Reason = dto.Reason;

        await _context.SaveChangesAsync();

        if (currentUserId.HasValue)
        {
            await TryWriteSystemLog(
                currentUserId.Value,
                "UPDATE_INVENTORY_CHECK",
                "stock_tickets",
                "InventoryCheck",
                stockTicket.StockTicketId,
                $"Cập nhật phiếu kiểm kê kho {stockTicket.TicketCode}");
        }
        return new ApiResponse<InventoryCheckResponseDto> { Success = true, Message = "Inventory check updated successfully", Data = MapToInventoryCheckResponse(stockTicket) };
    }

    public async Task<ApiResponse<InventoryCheckResponseDto>> FillActualQuantitiesAsync(ClaimsPrincipal user, Guid id, FillActualQuantitiesDto dto, Guid? branchId)
    {
        var currentUserId = GetCurrentUserId(user);
        var stockTicketQuery = _context.StockTickets
            .Include(st => st.StockTicketItems)
            .Where(st => st.StockTicketId == id && st.TicketType == "INVENTORY_CHECK");

        if (!IsOwner(user))
        {
            if (branchId == null)
                throw new UnauthorizedAccessException("JWT token is missing or invalid");
            stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
        }

        var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

        if (stockTicket == null)
            throw new KeyNotFoundException($"Khong tim thay phieu kiem ke voi ID: {id}");

        if (stockTicket.Status == "Cancelled")
            throw new BusinessException("Cannot fill quantities for cancelled ticket", "Phieu da bi huy.");

        if (stockTicket.Status == "Completed")
            throw new BusinessException("Cannot fill quantities for completed ticket", "Phieu da duyet.");

        if (dto.Quantities == null || dto.Quantities.Count == 0)
            throw new BusinessException("Validation failed", "Danh sach so luong khong duoc rong.");

        foreach (var qtyItem in dto.Quantities)
        {
            var ticketItem = stockTicket.StockTicketItems.FirstOrDefault(ti => ti.TicketItemId == qtyItem.TicketItemId);
            if (ticketItem != null)
            {
                ticketItem.ActualQuantity = qtyItem.ActualQuantity;
                ticketItem.Discrepancy = qtyItem.ActualQuantity - ticketItem.SystemQuantity;
                // Notes is not a property of StockTicketItem - store in DiscrepancyNote via a separate mechanism if needed
            }
        }
        
        stockTicket.Status = "WaitingForApproval";

        await _context.SaveChangesAsync();

        if (currentUserId.HasValue)
        {
            await TryWriteSystemLog(
                currentUserId.Value,
                "FILL_INVENTORY_CHECK",
                "stock_tickets",
                "InventoryCheck",
                stockTicket.StockTicketId,
                $"Nhập số lượng kiểm đếm cho phiếu {stockTicket.TicketCode}");
        }
        return new ApiResponse<InventoryCheckResponseDto> { Success = true, Message = "Actual quantities filled successfully", Data = MapToInventoryCheckResponse(stockTicket) };
    }

    public async Task<ApiResponse<object>> ApproveInventoryCheckAsync(ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        var currentUserId = GetCurrentUserId(user);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var stockTicketQuery = _context.StockTickets
                .Include(st => st.StockTicketItems).ThenInclude(item => item.BranchProduct)
                .Where(st => st.StockTicketId == id && st.TicketType == "INVENTORY_CHECK");

            if (!IsOwner(user))
            {
                if (branchId == null)
                    throw new UnauthorizedAccessException("JWT token is missing or invalid");
                stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
            }

            var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

            if (stockTicket == null)
                throw new KeyNotFoundException($"Khong tim thay phieu kiem ke voi ID: {id}");

            if (stockTicket.Status == "Completed")
                throw new BusinessException("Ticket already approved", "Phieu da duoc duyet.");

            if (stockTicket.Status == "Cancelled")
                throw new BusinessException("Cannot approve cancelled ticket", "Phieu da bi huy.");

            var missingActuals = stockTicket.StockTicketItems.Where(i => i.ActualQuantity == null).ToList();
            if (missingActuals.Any())
                throw new BusinessException("Cannot approve: some items still lack actual quantities", $"Con {missingActuals.Count} san pham chua nhap so luong thuc te.");

            foreach (var item in stockTicket.StockTicketItems)
            {
                var qtyChange = item.ActualQuantity - item.SystemQuantity;

                await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE branch_products
                    SET actual_stock = {item.ActualQuantity},
                        available_stock = available_stock + {qtyChange}
                    WHERE branch_product_id = {item.BranchProductId}");

                var updatedBp = await _context.BranchProducts.FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId);

                var ledger = new InventoryLedger
                {
                    LedgerId = Guid.NewGuid(),
                    BranchId = stockTicket.BranchId,
                    BranchProductId = item.BranchProductId,
                    DocType = "INVENTORY_CHECK",
                    DocId = stockTicket.StockTicketId,
                    DocCode = stockTicket.TicketCode,
                    TransactionTime = DateTime.UtcNow,
                    QtyChange = qtyChange,
                    UnitCostPrice = Math.Round(item.UnitPrice ?? 0, 4),
                    TotalCostValue = Math.Round(qtyChange * (item.UnitPrice ?? 0), 4),
                    StockAfter = updatedBp?.ActualStock ?? 0,
                    AvgCostAfter = Math.Round(updatedBp?.CostPrice ?? (item.UnitPrice ?? 0), 4),
                    CreatedAt = DateTime.UtcNow
                };
                _context.InventoryLedgers.Add(ledger);
            }

            stockTicket.Status = "Completed";
            stockTicket.CompletedAt = DateTime.UtcNow;
            stockTicket.ConfirmedBy = currentUserId;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await TryWriteSystemLog(
                currentUserId.Value,
                "APPROVE_INVENTORY_CHECK",
                "stock_tickets",
                "InventoryCheck",
                stockTicket.StockTicketId,
                $"Duyệt phiếu kiểm kê kho {stockTicket.TicketCode}");

            return new ApiResponse<object>
            {
                Success = true,
                Message = "Inventory check approved and stock updated successfully.",
                Data = new { TicketId = stockTicket.StockTicketId, TicketCode = stockTicket.TicketCode, Status = stockTicket.Status }
            };
        }
        catch (BusinessException) { throw; }
        catch (UnauthorizedAccessException) { throw; }
        catch (KeyNotFoundException) { throw; }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse<object>> RejectOrRequestRecountAsync(ClaimsPrincipal user, Guid id, RejectOrRecountDto dto, Guid? branchId)
    {
        var currentUserId = GetCurrentUserId(user);
        if (!currentUserId.HasValue)
            throw new UnauthorizedAccessException("Khong the xac dinh nguoi dung tu token.");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var stockTicketQuery = _context.StockTickets
                .Where(st => st.StockTicketId == id && st.TicketType == "INVENTORY_CHECK");

            if (!IsOwner(user))
            {
                if (branchId == null)
                    throw new UnauthorizedAccessException("JWT token is missing or invalid");
                stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
            }

            var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

            if (stockTicket == null)
                throw new KeyNotFoundException($"Khong tim thay phieu kiem ke voi ID: {id}");

            if (stockTicket.Status == "Cancelled")
                throw new BusinessException("Cannot reject/recount cancelled ticket", "Phieu da bi huy.");

            if (dto.IsRecount)
            {
                stockTicket.Status = "Draft";
                stockTicket.RecountNumber += 1;
                stockTicket.RecountRequestedAt = DateTime.UtcNow;
                stockTicket.RecountRequestedByUserId = currentUserId;
                stockTicket.RecountReason = dto.Note;
                stockTicket.Notes = dto.Note ?? stockTicket.Notes;
            }
            else
            {
                stockTicket.Status = "Cancelled";
                stockTicket.CancelledAt = DateTime.UtcNow;
                stockTicket.CancelledBy = currentUserId;
                stockTicket.CancelReason = dto.Note;
                stockTicket.Notes = dto.Note ?? stockTicket.Notes;
            }

            if (dto.IsRecount)
            {
                var recountNotification = new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    Type = NotificationType.RecountRequested,
                    RecipientUserId = stockTicket.UserId,
                    BranchId = stockTicket.BranchId,
                    ReferenceId = stockTicket.StockTicketId,
                    ReferenceType = "InventoryCheck",
                    TriggeredByUserId = currentUserId.Value,
                    Message = $"Phiếu kiểm kê {stockTicket.TicketCode} đã bị yêu cầu đếm lại (Lần {stockTicket.RecountNumber}). Lý do: {dto.Note}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Notifications.Add(recountNotification);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            if (dto.IsRecount)
            {
                await _hub.Clients.All.SendAsync("SilentNotification");
            }

            await TryWriteSystemLog(
                currentUserId.Value,
                "REJECT_INVENTORY_CHECK",
                "stock_tickets",
                "InventoryCheck",
                stockTicket.StockTicketId,
                dto.IsRecount
                    ? $"Yêu cầu đếm lại phiếu kiểm kê {stockTicket.TicketCode} (Lần {stockTicket.RecountNumber})"
                    : $"Từ chối/hủy phiếu kiểm kê {stockTicket.TicketCode}");

            return new ApiResponse<object>
            {
                Success = true,
                Message = dto.IsRecount
                    ? $"Yêu cầu kiểm kê lại thành công (Lần {stockTicket.RecountNumber}). Phiếu trả về trạng thái Draft."
                    : "Từ chối phiếu kiểm kê thành công.",
                Data = new { TicketId = stockTicket.StockTicketId, TicketCode = stockTicket.TicketCode, Status = stockTicket.Status, RecountNumber = stockTicket.RecountNumber }
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse<object>> CancelInventoryCheckAsync(ClaimsPrincipal user, Guid id, CancelTicketDto dto, Guid? branchId)
    {
        var currentUserId = GetCurrentUserId(user);
        if (!currentUserId.HasValue)
            throw new UnauthorizedAccessException("Khong the xac dinh nguoi dung tu token.");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var stockTicketQuery = _context.StockTickets
                .Where(st => st.StockTicketId == id && st.TicketType == "INVENTORY_CHECK");

            if (!IsOwner(user))
            {
                if (branchId == null)
                    throw new UnauthorizedAccessException("JWT token is missing or invalid");
                stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
            }

            var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

            if (stockTicket == null)
                throw new KeyNotFoundException($"Khong tim thay phieu kiem ke voi ID: {id}");

            if (stockTicket.Status?.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) == true)
                throw new BusinessException("Ticket already cancelled", "Phieu da duoc huy truoc do.");

            stockTicket.Status = "Cancelled";
            stockTicket.CancelledAt = DateTime.UtcNow;
            stockTicket.CancelledBy = currentUserId;
            stockTicket.CancelReason = dto?.CancelReason;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await TryWriteSystemLog(
                currentUserId.Value,
                "CANCEL_INVENTORY_CHECK",
                "stock_tickets",
                "InventoryCheck",
                stockTicket.StockTicketId,
                $"Hủy phiếu kiểm kê {stockTicket.TicketCode}");

            return new ApiResponse<object>
            {
                Success = true,
                Message = "Inventory check cancelled successfully.",
                Data = new { TicketId = stockTicket.StockTicketId, TicketCode = stockTicket.TicketCode, Status = stockTicket.Status }
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse<InventoryCheckResponseDto>> UpdateDiscrepancyReasonsAsync(ClaimsPrincipal user, Guid id, UpdateDiscrepancyReasonsDto dto, Guid? branchId)
    {
        var stockTicketQuery = _context.StockTickets
            .Include(st => st.StockTicketItems)
            .Where(st => st.StockTicketId == id && st.TicketType == "INVENTORY_CHECK");

        if (!IsOwner(user))
        {
            if (branchId == null)
                throw new UnauthorizedAccessException("JWT token is missing or invalid");
            stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
        }

        var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

        if (stockTicket == null)
            throw new KeyNotFoundException($"Khong tim thay phieu kiem ke voi ID: {id}");

        if (stockTicket.Status == "Cancelled")
            throw new BusinessException("Cannot update reasons for cancelled ticket", "Phieu da bi huy.");

        foreach (var reasonItem in dto.DiscrepancyReasons)
        {
            var ticketItem = stockTicket.StockTicketItems.FirstOrDefault(ti => ti.TicketItemId == reasonItem.TicketItemId);
            if (ticketItem != null)
            {
                ticketItem.Notes = reasonItem.Reason;
            }
        }

        await _context.SaveChangesAsync();

        var currentUserId = GetCurrentUserId(user);
        if (currentUserId.HasValue)
        {
            await TryWriteSystemLog(
                currentUserId.Value,
                "UPDATE_INVENTORY_CHECK_REASON",
                "stock_tickets",
                "InventoryCheck",
                stockTicket.StockTicketId,
                $"Cập nhật lý do chênh lệch phiếu kiểm kê {stockTicket.TicketCode}");
        }
        return new ApiResponse<InventoryCheckResponseDto> { Success = true, Message = "Discrepancy reasons updated successfully", Data = MapToInventoryCheckResponse(stockTicket) };
    }

    public async Task<string> GetPrintHtmlAsync(ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        var ticket = await _context.StockTickets
            .Include(t => t.Branch)
            .Include(t => t.StockTicketItems).ThenInclude(i => i.BranchProduct).ThenInclude(bp => bp.Product)
            .FirstOrDefaultAsync(t => t.StockTicketId == id && t.BranchId == branchId.Value && t.TicketType == "INVENTORY_CHECK");

        if (ticket == null)
            throw new KeyNotFoundException($"No inventory check with ID: {id}");

        var items = ticket.StockTicketItems?.Select(i => new
        {
            i.BranchProduct?.Product?.ProductCode,
            i.BranchProduct?.Product?.ProductName,
            Unit = i.BranchProduct?.Product?.BaseUnit,
            SystemQty = i.SystemQuantity,
            ActualQty = i.ActualQuantity,
            Discrepancy = i.Discrepancy,
            Reason = i.Notes
        }).ToList() ?? new();

        var html = $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<title>Phieu Kiem Ke Kho - {ticket.TicketCode}</title>
<style>
body {{ font-family: monospace; font-size: 12px; margin: 20px; }}
h1 {{ text-align: center; }}
table {{ width: 100%; border-collapse: collapse; margin-top: 10px; }}
th, td {{ border: 1px solid #000; padding: 5px; text-align: left; }}
th {{ background: #f0f0f0; }}
</style>
</head>
<body>
<h1>PHIEU KIEM KE KHO</h1>
<p><strong>Ma phieu:</strong> {ticket.TicketCode}</p>
<p><strong>Chi nhanh:</strong> {ticket.Branch?.BranchName}</p>
<p><strong>Ngay tao:</strong> {ticket.CreatedAt:yyyy-MM-dd HH:mm}</p>
<p><strong>Trang thai:</strong> {ticket.Status}</p>
<table>
<tr><th>STT</th><th>Ma SP</th><th>Ten SP</th><th>Don vi</th><th>SL He thong</th><th>SL Thuc te</th><th>Chenh lech</th><th>Ghi chu</th></tr>";

        var stt = 1;
        foreach (var item in items)
        {
            html += $@"<tr>
<td>{stt++}</td>
<td>{item.ProductCode}</td>
<td>{item.ProductName}</td>
<td>{item.Unit}</td>
<td>{item.SystemQty}</td>
<td>{item.ActualQty}</td>
<td>{item.Discrepancy}</td>
<td>{item.Reason}</td>
</tr>";
        }

        html += $@"</table>
<p><strong>Ghi chu:</strong> {ticket.Notes ?? ""}</p>
<p style=""margin-top:40px"">Ky ten: __________________</p>
</body>
</html>";

        return html;
    }

    public async Task<ApiResponse<object>> GetCountersAsync(ClaimsPrincipal user, Guid? branchId)
    {
        var query = _context.StockTickets.Where(st => st.TicketType == "INVENTORY_CHECK");
        if (branchId.HasValue)
            query = query.Where(st => st.BranchId == branchId.Value);

        var pending = await query.CountAsync(st => st.Status == "Draft");
        var completed = await query.CountAsync(st => st.Status == "Completed");
        var waiting = await query.CountAsync(st => st.Status == "WaitingForApproval");
        var cancelled = await query.CountAsync(st => st.Status == "Cancelled");

        return new ApiResponse<object>
        {
            Success = true,
            Message = "Counters retrieved successfully",
            Data = new { Pending = pending, Completed = completed, Waiting = waiting, Cancelled = cancelled, Total = pending + completed + waiting + cancelled }
        };
    }

    public async Task<ApiResponse<object>> GetNotificationsAsync(ClaimsPrincipal user, Guid? branchId)
    {
        var currentUserId = GetCurrentUserId(user);
        if (!currentUserId.HasValue)
            throw new UnauthorizedAccessException("JWT token is missing or invalid");

        // Fetch notifications for the current user and their branch (if applicable)
        var query = _context.Notifications
            .AsNoTracking()
            .Include(n => n.RecipientUser)
            .Include(n => n.TriggeredByUser)
            .Include(n => n.InventoryCheck)
            .Where(n => n.RecipientUserId == currentUserId.Value || (n.RecipientUserId == null && (!branchId.HasValue || n.BranchId == branchId.Value)));

        var unreadCount = await query.CountAsync(n => !n.IsRead);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(50) // Adjust as needed
            .ToListAsync();

        var dtoList = items.Select(n => new NotificationDto
        {
            NotificationId = n.NotificationId,
            Type = n.Type.ToString(),
            TypeDisplay = n.Type.ToString(),
            RecipientUserId = n.RecipientUserId,
            RecipientUserName = n.RecipientUser?.FullName ?? n.RecipientUser?.Username,
            BranchId = n.BranchId,
            InventoryCheckId = n.InventoryCheckId,
            InventoryCheckCode = n.InventoryCheck?.TicketCode,
            ReferenceId = n.ReferenceId,
            ReferenceType = n.ReferenceType,
            TriggeredByUserId = n.TriggeredByUserId,
            TriggeredByUserName = n.TriggeredByUser?.FullName ?? n.TriggeredByUser?.Username,
            Message = n.Message,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();

        return new ApiResponse<object>
        {
            Success = true,
            Message = "Notifications retrieved successfully",
            Data = new { items = dtoList, totalCount = dtoList.Count, unreadCount = unreadCount }
        };
    }

    public async Task<ApiResponse<object>> MarkNotificationsAsReadAsync(ClaimsPrincipal user, List<Guid> notificationIds, Guid? branchId)
    {
        if (notificationIds == null || !notificationIds.Any())
        {
            return new ApiResponse<object> { Success = true, Message = "No notifications to mark as read." };
        }

        var currentUserId = GetCurrentUserId(user);
        
        var notificationsToUpdate = await _context.Notifications
            .Where(n => notificationIds.Contains(n.NotificationId) && (n.RecipientUserId == currentUserId || n.RecipientUserId == null))
            .ToListAsync();

        foreach (var notif in notificationsToUpdate)
        {
            notif.IsRead = true;
        }

        if (notificationsToUpdate.Any())
        {
            await _context.SaveChangesAsync();
        }

        return new ApiResponse<object>
        {
            Success = true,
            Message = $"Marked {notificationsToUpdate.Count} notifications as read.",
            Data = new { Count = notificationsToUpdate.Count }
        };
    }

    // ===== Private helpers =====

    private InventoryCheckResponseDto MapToInventoryCheckResponse(StockTicket stockTicket)
    {
        return new InventoryCheckResponseDto
        {
            TicketId = stockTicket.StockTicketId,
            TicketCode = stockTicket.TicketCode ?? string.Empty,
            Status = stockTicket.Status ?? string.Empty,
            BranchId = stockTicket.BranchId,
            CreatedByUserId = stockTicket.UserId,
            CreatedByUserName = stockTicket.User?.FullName ?? stockTicket.User?.Username,
            Notes = stockTicket.Notes,
            CreatedAt = stockTicket.CreatedAt ?? DateTime.UtcNow,
            CancelledAt = stockTicket.CancelledAt,
            CancelledByUserId = stockTicket.CancelledBy,
            CancelledReason = stockTicket.CancelReason,
            RecountNumber = stockTicket.RecountNumber,
            RecountRequestedAt = stockTicket.RecountRequestedAt,
            RecountRequestedByUserId = stockTicket.RecountRequestedByUserId,
            RecountRequestedByUserName = stockTicket.RecountRequestedByUser?.FullName ?? stockTicket.RecountRequestedByUser?.Username,
            RecountReason = stockTicket.RecountReason,
            TotalDiscrepancy = (stockTicket.Status == "WaitingForApproval" || stockTicket.Status == "Completed")
                                && stockTicket.StockTicketItems != null 
                ? (int?)stockTicket.StockTicketItems.Sum(x => x.Discrepancy)
                : null,
            Details = stockTicket.StockTicketItems?.Select(item => new InventoryCheckDetailDto
            {
                DetailId = item.TicketItemId,
                ProductId = item.BranchProduct?.ProductId ?? Guid.Empty,
                ProductCode = item.BranchProduct?.Product?.ProductCode ?? string.Empty,
                ProductName = item.BranchProduct?.Product?.ProductName ?? string.Empty,
                SystemQuantity = (int)item.SystemQuantity,
                CurrentActualStock = (int)(item.BranchProduct?.ActualStock ?? 0),
                ActualQuantity = (int)item.ActualQuantity,
                Discrepancy = (int)item.Discrepancy,
                IsCounted = stockTicket.Status != "Draft",
                ReasonNote = item.Notes,
                UnitName = item.UnitName ?? item.BranchProduct?.Product?.BaseUnit
            }).ToList() ?? new()
        };
    }
}
