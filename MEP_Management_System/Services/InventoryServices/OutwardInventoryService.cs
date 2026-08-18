using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Hubs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.InventoryServices;

/// <summary>
/// Implementation of OutwardInventory business logic.
/// </summary>
public class OutwardInventoryService : IOutwardInventoryService
{
    private readonly MEPDatabaseContext _context;
    private readonly IHubContext<InventoryHub> _hub;

    private static readonly string[] ValidReturnToSupplierReasons =
        { "Xuất trả nhà cung cấp", "Trả hàng nhà cung cấp", "Xuất trả NCC" };

    private static readonly string[] ValidDamagedHaoHutReasons =
        { "Xuất hủy / Hao hụt", "Xuất hủy" };

    private static readonly string[] ValidInternalUseReasons =
        { "Xuất sử dụng nội bộ", "Xuất nội bộ / Điều chuyển" };

    public OutwardInventoryService(MEPDatabaseContext context, IHubContext<InventoryHub> hub)
    {
        _context = context;
        _hub = hub;
    }

    private static bool HasGlobalAccess(ClaimsPrincipal user) => user.IsInRole("Owner");

    private static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst("userId")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
    }

    private static string GetUserName(ClaimsPrincipal user)
    {
        return user.FindFirst("name")?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value ?? "Người dùng";
    }

    private async Task WriteSystemLogAsync(Guid userId, string action, string tableName,
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
                Source = "OutwardInventoryService",
                Timestamp = DateTime.UtcNow
            };
            _context.SystemLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch { /* Silent */ }
    }

    private async Task BroadcastAndSaveNotification(NotificationType type, Guid branchId,
        string message, Guid triggeredByUserId, Guid? referenceId = null, string? referenceType = null)
    {
        var notif = new Notification
        {
            NotificationId = Guid.NewGuid(),
            Type = type,
            RecipientUserId = null,
            BranchId = branchId,
            InventoryCheckId = null,
            ReferenceId = referenceId,
            ReferenceType = referenceType,
            TriggeredByUserId = triggeredByUserId,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notif);
        await _context.SaveChangesAsync();

        var groupName = $"Branch_{branchId}";
        await _hub.Clients.Group(groupName).SendAsync("ReceiveInventoryNotification", new
        {
            notif.NotificationId,
            notif.Type,
            TypeDisplay = notif.Type.ToString(),
            notif.Message,
            notif.CreatedAt,
            notif.IsRead,
            notif.InventoryCheckId,
            notif.ReferenceId,
            notif.ReferenceType
        });
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

    private static string GetTicketTypeNamePrint(OutwardInventoryType type)
    {
        return type switch
        {
            OutwardInventoryType.ReturnToSupplier => "TRA HANG NHA CUNG CAP",
            OutwardInventoryType.DamagedHaoHut => "HAO HUT / MAT",
            OutwardInventoryType.InternalUse => "SU DUNG NOI BO",
            _ => "UNKNOWN"
        };
    }

    public async Task<ApiResponse<PaginatedResponse<OutwardInventoryResponseDto>>> GetOutwardInventoriesAsync(
        ClaimsPrincipal user, Guid? branchId, string? status, string? ticketType,
        DateTime? fromDate, DateTime? toDate, int pageNumber, int pageSize)
    {
        try
        {
            if (!HasGlobalAccess(user) && !user.HasClaim("permission", PermissionConstants.STOCK_OUTWARD_CREATE)
                && !user.HasClaim("permission", PermissionConstants.STOCK_OUTWARD_CONFIRM))
            {
                return new ApiResponse<PaginatedResponse<OutwardInventoryResponseDto>>
                {
                    Success = false,
                    Message = "Bạn không có quyền xem phiếu xuất kho."
                };
            }

            if (branchId == null)
            {
                return new ApiResponse<PaginatedResponse<OutwardInventoryResponseDto>>
                {
                    Success = false,
                    Message = "Tài khoản của bạn chưa được cấu hình chi nhánh hợp lệ.",
                    Errors = new() { "Branch context is missing in token." }
                };
            }

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var query = _context.StockTickets
                .Include(st => st.User)
                .Include(st => st.StockTicketItems)
                    .ThenInclude(item => item.BranchProduct)
                        .ThenInclude(bp => bp.Product)
                .Where(st => st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF"
                    || st.TicketType == "TRANSFER" || st.TicketType == "SALE")
                .Where(st => st.BranchId == branchId.Value)
                .AsQueryable();

            // Default: hide CANCELLED, only PENDING + COMPLETED
            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = status.Trim().ToUpperInvariant();
                if (normalizedStatus == "ALL")
                {
                    // Show all statuses
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
                query = query.Where(st => st.Status == null || st.Status != "CANCELLED");
            }

            if (!string.IsNullOrWhiteSpace(ticketType))
                query = query.Where(st => st.TicketType != null && st.TicketType.ToLower() == ticketType.Trim().ToLower());

            if (fromDate.HasValue)
            {
                var localFrom = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Unspecified);
                var utcFrom = TimeZoneInfo.ConvertTimeToUtc(localFrom, VietnamTime.Tz);
                query = query.Where(st => st.CreatedAt >= utcFrom);
            }

            if (toDate.HasValue)
            {
                var localTo = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Unspecified);
                var utcTo = TimeZoneInfo.ConvertTimeToUtc(localTo, VietnamTime.Tz);
                query = query.Where(st => st.CreatedAt < utcTo);
            }

            query = query.OrderByDescending(st => st.CreatedAt);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new ApiResponse<PaginatedResponse<OutwardInventoryResponseDto>>
            {
                Success = true,
                Message = "Lấy lịch sử xuất kho thành công.",
                Data = new PaginatedResponse<OutwardInventoryResponseDto>
                {
                    Items = items.Select(st => MapToResponse(st, user)).ToList(),
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    HasNextPage = pageNumber < totalPages,
                    HasPreviousPage = pageNumber > 1
                }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PaginatedResponse<OutwardInventoryResponseDto>>
            {
                Success = false,
                Message = "Lỗi khi lấy dữ liệu xuất kho.",
                Errors = new() { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<OutwardInventoryResponseDto>> GetOutwardInventoryByIdAsync(
        ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        try
        {
            if (!HasGlobalAccess(user) && !user.HasClaim("permission", PermissionConstants.STOCK_OUTWARD_CREATE)
                && !user.HasClaim("permission", PermissionConstants.STOCK_OUTWARD_CONFIRM))
            {
                return new ApiResponse<OutwardInventoryResponseDto>
                {
                    Success = false,
                    Message = "Bạn không có quyền xem chi tiết phiếu xuất kho."
                };
            }

            var stockTicketQuery = _context.StockTickets
                .Include(st => st.User)
                .Include(st => st.StockTicketItems)
                    .ThenInclude(item => item.BranchProduct)
                        .ThenInclude(bp => bp.Product)
                .Where(st => st.StockTicketId == id &&
                    (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF"
                        || st.TicketType == "TRANSFER" || st.TicketType == "SALE"));

            if (!HasGlobalAccess(user))
            {
                if (branchId == null)
                {
                    return new ApiResponse<OutwardInventoryResponseDto>
                    {
                        Success = false,
                        Message = "JWT token không hợp lệ hoặc thiếu thông tin chi nhánh.",
                        Errors = new() { "Authorization header required" }
                    };
                }
                stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
            }

            var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

            if (stockTicket == null)
            {
                return new ApiResponse<OutwardInventoryResponseDto>
                {
                    Success = false,
                    Message = "Không tìm thấy phiếu xuất kho hoặc bạn không có quyền xem phiếu này.",
                    Errors = new() { $"No outward inventory with ID: {id}" }
                };
            }

            Invoice? invoice = null;
            if (stockTicket.TicketType == "SALE")
            {
                invoice = await _context.Invoices
                    .Include(i => i.InvoiceItems)
                    .FirstOrDefaultAsync(i => i.InvoiceCode == stockTicket.TicketCode);
            }

            return new ApiResponse<OutwardInventoryResponseDto>
            {
                Success = true,
                Message = "Lấy chi tiết phiếu xuất kho thành công.",
                Data = MapToResponse(stockTicket, user, invoice)
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<OutwardInventoryResponseDto>
            {
                Success = false,
                Message = "Lỗi hệ thống khi lấy chi tiết phiếu.",
                Errors = new() { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<object>> CreateOutwardInventoryAsync(
        ClaimsPrincipal user, OutwardInventoryCreateDto dto, Guid? branchIdOverride)
    {
        try
        {
            var currentUserId = GetCurrentUserId(user);
            if (currentUserId == null)
            {
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không xác định được định danh người dùng từ Token.",
                    Errors = new() { "User identity missing." }
                };
            }

            var hasGlobalAccess = HasGlobalAccess(user);

            // Resolve branchId
            Guid? resolvedBranchId = null;

            var dbUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == currentUserId.Value);
            if (dbUser?.DefaultBranchId != null && dbUser.DefaultBranchId != Guid.Empty)
            {
                resolvedBranchId = dbUser.DefaultBranchId;
            }
            else if (hasGlobalAccess && branchIdOverride.HasValue)
            {
                resolvedBranchId = branchIdOverride.Value;
            }
            else if (user.FindFirst("branchId")?.Value != null && Guid.TryParse(user.FindFirst("branchId")?.Value, out var tokenBranch))
            {
                resolvedBranchId = tokenBranch;
            }
            else
            {
                var activeBranch = await _context.Branches.AsNoTracking().Where(b => b.IsActive == 1).FirstOrDefaultAsync();
                resolvedBranchId = activeBranch?.BranchId;
            }

            if (resolvedBranchId == null)
            {
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy chi nhánh hợp lệ.",
                    Errors = new() { "No valid branch found." }
                };
            }

            // Validate items
            if (dto.Items == null || dto.Items.Count == 0)
            {
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Dữ liệu không hợp lệ",
                    Errors = new() { "Yêu cầu có ít nhất 1 mặt hàng để xuất kho." }
                };
            }

            if (string.IsNullOrWhiteSpace(dto.Reason))
            {
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Dữ liệu không hợp lệ",
                    Errors = new() { "Trường 'reason' là bắt buộc." }
                };
            }

            // Validate reason
            var normalizedReason = dto.Reason.Trim();
            var isValidReason = dto.OutwardType switch
            {
                OutwardInventoryType.ReturnToSupplier =>
                    ValidReturnToSupplierReasons.Any(r => string.Equals(r, normalizedReason, StringComparison.OrdinalIgnoreCase)),
                OutwardInventoryType.DamagedHaoHut =>
                    ValidDamagedHaoHutReasons.Any(r => string.Equals(r, normalizedReason, StringComparison.OrdinalIgnoreCase)),
                OutwardInventoryType.InternalUse =>
                    ValidInternalUseReasons.Any(r => string.Equals(r, normalizedReason, StringComparison.OrdinalIgnoreCase)),
                _ => false
            };

            if (!isValidReason)
            {
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Dữ liệu không hợp lệ",
                    Errors = new()
                    {
                        "Lý do xuất kho không hợp lệ. Chỉ chấp nhận: Xuất trả nhà cung cấp, Xuất hủy / Hao hụt, Xuất sử dụng nội bộ."
                    }
                };
            }

            // Validate inward ticket reference
            StockTicket? inwardTicket = null;
            Dictionary<Guid, decimal>? inwardUnitPrices = null;
            Dictionary<Guid, decimal>? maxReturnableByInwardItemId = null;

            if (dto.OutwardType == OutwardInventoryType.ReturnToSupplier && dto.InwardTicketId.HasValue)
            {
                inwardTicket = await _context.StockTickets
                    .Include(st => st.StockTicketItems)
                    .FirstOrDefaultAsync(st => st.StockTicketId == dto.InwardTicketId.Value
                        && st.Status == "COMPLETED"
                        && st.TicketType == "PURCHASE");

                if (inwardTicket == null)
                {
                    return new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Phiếu nhập tham chiếu không tồn tại hoặc chưa hoàn thành."
                    };
                }

                if (dto.SupplierId.HasValue && inwardTicket.SupplierId != dto.SupplierId.Value)
                {
                    return new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Phiếu nhập không thuộc về nhà cung cấp đã chọn."
                    };
                }

                inwardUnitPrices = inwardTicket.StockTicketItems
                    .ToDictionary(i => i.TicketItemId, i => i.UnitPrice ?? 0);

                var inwardItemIds = inwardTicket.StockTicketItems.Select(i => i.TicketItemId).ToList();
                var returnedQuantities = await _context.StockTicketItems
                    .Where(si => si.InwardTicketItemId != null && inwardItemIds.Contains(si.InwardTicketItemId.Value)
                        && si.StockTicket!.Status != "CANCELLED")
                    .GroupBy(si => si.InwardTicketItemId!.Value)
                    .Select(g => new { InwardTicketItemId = g.Key, TotalReturned = g.Sum(si => si.Discrepancy) })
                    .ToListAsync();

                maxReturnableByInwardItemId = inwardTicket.StockTicketItems.ToDictionary(
                    i => i.TicketItemId,
                    i => i.Discrepancy - returnedQuantities
                        .Where(r => r.InwardTicketItemId == i.TicketItemId)
                        .Sum(r => r.TotalReturned)
                );

                var validationErrors = new List<string>();
                foreach (var item in dto.Items.Where(i => i.InwardTicketItemId.HasValue))
                {
                    if (!inwardUnitPrices.ContainsKey(item.InwardTicketItemId!.Value))
                    {
                        validationErrors.Add($"Dòng hàng nhập (ID: {item.InwardTicketItemId}) không thuộc phiếu nhập đã chọn.");
                        continue;
                    }

                    var inwardUnitPrice = inwardUnitPrices[item.InwardTicketItemId!.Value];
                    var requestUnitPrice = item.UnitPrice ?? 0;
                    if (requestUnitPrice > 0 && Math.Abs(requestUnitPrice - inwardUnitPrice) > 0.01m)
                    {
                        validationErrors.Add($"Đơn giá xuất ({requestUnitPrice:N0}) không khớp với đơn giá nhập gốc ({inwardUnitPrice:N0}).");
                    }

                    var maxReturnable = maxReturnableByInwardItemId.GetValueOrDefault(item.InwardTicketItemId!.Value, 0);
                    if (item.Quantity > maxReturnable)
                    {
                        validationErrors.Add($"Số lượng xuất trả ({item.Quantity}) vượt quá số lượng còn lại có thể trả ({maxReturnable}).");
                    }
                }

                if (validationErrors.Count > 0)
                {
                    return new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ.",
                        Errors = validationErrors
                    };
                }
            }

            // Validate products
            var productIds = dto.Items.Select(i => i.BranchProductId).Distinct().ToList();
            var branchProducts = await _context.BranchProducts
                .Where(bp => (productIds.Contains(bp.ProductId) || productIds.Contains(bp.BranchProductId))
                    && bp.BranchId == resolvedBranchId.Value)
                .Include(bp => bp.Product)
                .ToListAsync();

            if (branchProducts.Count != productIds.Count)
            {
                var foundProductIds = branchProducts.Select(bp => bp.ProductId).ToHashSet();
                var foundBranchProductIds = branchProducts.Select(bp => bp.BranchProductId).ToHashSet();
                var missingIds = productIds.Where(id => !foundProductIds.Contains(id) && !foundBranchProductIds.Contains(id)).ToList();
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Yêu cầu không hợp lệ",
                    Errors = new() { $"Một hoặc nhiều sản phẩm không tồn tại trong hệ thống. (IDs: {string.Join(", ", missingIds)})" }
                };
            }

            var validationErrors2 = new List<string>();
            var effectiveUnitPriceByBranchProductId = new Dictionary<Guid, decimal>();

            foreach (var item in dto.Items)
            {
                if (item.Quantity <= 0)
                    validationErrors2.Add("Số lượng xuất của sản phẩm phải lớn hơn 0.");

                var bp = branchProducts.FirstOrDefault(x => x.BranchProductId == item.BranchProductId || x.ProductId == item.BranchProductId);
                if (bp == null)
                {
                    validationErrors2.Add($"Sản phẩm '{item.BranchProductId}' không thuộc chi nhánh hiện tại.");
                    continue;
                }

                if (item.Quantity > bp.ActualStock)
                {
                    validationErrors2.Add($"Số lượng tồn kho khả dụng của sản phẩm '{bp.Product?.ProductName ?? bp.ProductId.ToString()}' không đủ để thực hiện xuất kho. Tồn hiện tại: {bp.ActualStock}, cần xuất: {item.Quantity}.");
                }

                var fallbackUnitPrice = item.UnitPrice.HasValue && item.UnitPrice.Value > 0
                    ? item.UnitPrice.Value
                    : (item.InwardTicketItemId.HasValue && inwardUnitPrices != null
                        && inwardUnitPrices.TryGetValue(item.InwardTicketItemId.Value, out var iup) && iup > 0
                        ? iup
                        : bp.CostPrice > 0 ? bp.CostPrice : 0m);

                if (fallbackUnitPrice <= 0)
                    validationErrors2.Add($"Sản phẩm {bp.Product?.ProductName ?? bp.ProductId.ToString()} chưa có giá vốn hợp lệ để xuất kho.");

                effectiveUnitPriceByBranchProductId[bp.BranchProductId] = fallbackUnitPrice;
            }

            if (validationErrors2.Count > 0)
            {
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Dữ liệu không hợp lệ.",
                    Errors = validationErrors2
                };
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
                    Status = "PENDING",
                    Reason = dto.Reason,
                    Notes = dto.Note,
                    SupplierId = dto.SupplierId,
                    InwardTicketId = dto.InwardTicketId,
                    CreatedAt = currentTime
                };

                _context.StockTickets.Add(stockTicket);

                foreach (var item in dto.Items)
                {
                    var bp = branchProducts.First(x => x.BranchProductId == item.BranchProductId || x.ProductId == item.BranchProductId);
                    var effectiveUnitPrice = effectiveUnitPriceByBranchProductId[bp.BranchProductId];

                    var ticketItem = new StockTicketItem
                    {
                        TicketItemId = Guid.NewGuid(),
                        StockTicketId = stockTicket.StockTicketId,
                        BranchProductId = bp.BranchProductId,
                        SystemQuantity = bp.ActualStock,
                        ActualQuantity = bp.ActualStock,
                        Discrepancy = item.Quantity,
                        UnitPrice = effectiveUnitPrice,
                        InwardTicketItemId = item.InwardTicketItemId,
                        UnitName = bp.Product?.BaseUnit ?? ""
                    };

                    _context.StockTicketItems.Add(ticketItem);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await WriteSystemLogAsync(currentUserId.Value, "CREATE_OUTWARD_INVENTORY",
                    "stock_tickets", "OutwardInventory", stockTicket.StockTicketId,
                    $"Tạo phiếu xuất kho {stockTicket.TicketCode}");

                await BroadcastAndSaveNotification(NotificationType.Created, resolvedBranchId.Value,
                    $"{GetUserName(user)} vừa tạo phiếu xuất kho {stockTicket.TicketCode}.",
                    currentUserId.Value, stockTicket.StockTicketId, "OutwardInventory");

                return new ApiResponse<object>
                {
                    Success = true,
                    Message = "Tạo phiếu xuất kho thành công. Vui lòng xác nhận để cập nhật tồn kho.",
                    Data = new
                    {
                        TicketId = stockTicket.StockTicketId,
                        TicketCode = stockTicket.TicketCode,
                        TicketType = stockTicket.TicketType,
                        BranchId = stockTicket.BranchId,
                        Status = stockTicket.Status
                    }
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            return new ApiResponse<object>
            {
                Success = false,
                Message = "Gặp lỗi khi tạo phiếu xuất kho.",
                Errors = new() { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<OutwardInventoryResponseDto>> UpdateOutwardInventoryAsync(
        ClaimsPrincipal user, Guid id, OutwardInventoryUpdateDto dto, Guid? branchId)
    {
        try
        {
            var currentUserId = GetCurrentUserId(user);
            var hasGlobalAccess = HasGlobalAccess(user);

            var stockTicketQuery = _context.StockTickets
                .Include(st => st.StockTicketItems)
                    .ThenInclude(item => item.BranchProduct)
                .Where(st => st.StockTicketId == id &&
                    (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER"));

            if (!hasGlobalAccess)
            {
                if (branchId == null)
                {
                    return new ApiResponse<OutwardInventoryResponseDto>
                    {
                        Success = false,
                        Message = "Không có quyền thực hiện hành động này.",
                        Errors = new() { "Branch context missing" }
                    };
                }
                stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
            }

            var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

            if (stockTicket == null)
            {
                return new ApiResponse<OutwardInventoryResponseDto>
                {
                    Success = false,
                    Message = "Không tìm thấy phiếu cần cập nhật hoặc bạn không có quyền sửa đổi phiếu này.",
                    Errors = new() { $"No outward inventory with ID: {id}" }
                };
            }

            bool hasConfirmPermission = user.HasClaim("permission", PermissionConstants.STOCK_OUTWARD_CONFIRM);
            bool canUpdate = hasGlobalAccess || hasConfirmPermission;
            if (!canUpdate && stockTicket.UserId != currentUserId)
            {
                return new ApiResponse<OutwardInventoryResponseDto>
                {
                    Success = false,
                    Message = "Bạn không có quyền chỉnh sửa phiếu này."
                };
            }

            if (stockTicket.Status == "CANCELLED")
            {
                return new ApiResponse<OutwardInventoryResponseDto>
                {
                    Success = false,
                    Message = "Phiếu đã bị hủy. Không thể chỉnh sửa."
                };
            }

            if (stockTicket.Status == "COMPLETED")
            {
                if (dto.Note != null) stockTicket.Notes = dto.Note;
                if (dto.Reason != null) stockTicket.Reason = dto.Reason;
                await _context.SaveChangesAsync();

                await WriteSystemLogAsync(stockTicket.UserId, "UPDATE_OUTWARD_INVENTORY",
                    "stock_tickets", "OutwardInventory", stockTicket.StockTicketId,
                    $"Cập nhật phiếu xuất kho {stockTicket.TicketCode}");

                return new ApiResponse<OutwardInventoryResponseDto>
                {
                    Success = true,
                    Message = "Cập nhật thông tin phiếu thành công (chỉ ghi chú cho phiếu đã xác nhận).",
                    Data = MapToResponse(stockTicket, user)
                };
            }

            // PENDING
            if (dto.Note != null) stockTicket.Notes = dto.Note;
            if (dto.Reason != null) stockTicket.Reason = dto.Reason;

            if (!string.IsNullOrWhiteSpace(dto.Status))
            {
                var normalized = dto.Status.Trim().ToUpperInvariant();
                if (normalized == "CANCELLED")
                    stockTicket.Status = "CANCELLED";
            }

            await _context.SaveChangesAsync();

            return new ApiResponse<OutwardInventoryResponseDto>
            {
                Success = true,
                Message = "Cập nhật thông tin phiếu thành công.",
                Data = MapToResponse(stockTicket, user)
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<OutwardInventoryResponseDto>
            {
                Success = false,
                Message = "Lỗi khi cập nhật phiếu xuất kho.",
                Errors = new() { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<object>> ConfirmOutwardInventoryAsync(
        ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var currentUserId = GetCurrentUserId(user);
            var hasGlobalAccess = HasGlobalAccess(user);

            var stockTicketQuery = _context.StockTickets
                .Include(st => st.StockTicketItems)
                    .ThenInclude(item => item.BranchProduct)
                        .ThenInclude(bp => bp.Product)
                .Where(st => st.StockTicketId == id &&
                    (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER"));

            if (!hasGlobalAccess)
            {
                if (branchId == null)
                {
                    await transaction.RollbackAsync();
                    return new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không xác định được chi nhánh.",
                        Errors = new() { "Branch context missing" }
                    };
                }
                stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
            }

            var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

            if (stockTicket == null)
            {
                await transaction.RollbackAsync();
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy phiếu xuất kho.",
                    Errors = new() { $"No outward inventory with ID: {id}" }
                };
            }

            if (stockTicket.Status == "COMPLETED")
            {
                await transaction.RollbackAsync();
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu đã được xác nhận trước đó."
                };
            }

            if (stockTicket.Status == "CANCELLED")
            {
                await transaction.RollbackAsync();
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu đã bị hủy. Không thể xác nhận."
                };
            }

            if (stockTicket.StockTicketItems == null || stockTicket.StockTicketItems.Count == 0)
            {
                await transaction.RollbackAsync();
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu không có sản phẩm nào. Vui lòng thêm sản phẩm trước khi xác nhận."
                };
            }

            // Real-time stock validation
            var insufficientStockItems = new List<string>();
            var branchProductIds = stockTicket.StockTicketItems.Select(i => i.BranchProductId).ToList();
            var latestBranchProducts = await _context.BranchProducts
                .Where(bp => branchProductIds.Contains(bp.BranchProductId))
                .Include(bp => bp.Product)
                .ToDictionaryAsync(bp => bp.BranchProductId);

            // Row-level lock
            if (branchProductIds.Any() && _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
            {
                var uuidArray = string.Join(",", branchProductIds.Select(bpid => $"'{bpid.ToString("D")}'"));
#pragma warning disable EF1002
                await _context.Database.ExecuteSqlRawAsync(
                    $"SELECT 1 FROM branch_products WHERE branch_product_id IN ({uuidArray}) FOR UPDATE");
#pragma warning restore EF1002
            }

            foreach (var item in stockTicket.StockTicketItems)
            {
                if (latestBranchProducts.TryGetValue(item.BranchProductId, out var bp))
                {
                    if (bp.ActualStock < item.Discrepancy)
                    {
                        insufficientStockItems.Add(
                            $"{bp.Product?.ProductName ?? item.BranchProductId.ToString()} " +
                            $"(tồn: {bp.ActualStock}, cần xuất: {item.Discrepancy})"
                        );
                    }
                }
            }

            if (insufficientStockItems.Count > 0)
            {
                await transaction.RollbackAsync();
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không đủ tồn kho để xuất",
                    Errors = new List<string>
                    {
                        "Không đủ tồn kho để xuất",
                        $"Sản phẩm không đủ: {string.Join("; ", insufficientStockItems)}"
                    }
                };
            }

            // Update stock
            foreach (var item in stockTicket.StockTicketItems)
            {
                if (!latestBranchProducts.TryGetValue(item.BranchProductId, out var bp))
                    continue;

                if ((item.UnitPrice ?? 0) <= 0 && stockTicket.TicketType != "RETURN_SUPPLIER")
                    item.UnitPrice = bp.CostPrice;

                var effectiveUnitPrice = item.UnitPrice ?? bp.CostPrice;

                try
                {
                    if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
                    {
                        if (bp.ActualStock < item.Discrepancy)
                        {
                            await transaction.RollbackAsync();
                            return new ApiResponse<object>
                            {
                                Success = false,
                                Message = "Số lượng tồn kho khả dụng không đủ.",
                                Errors = new() { $"'{bp.Product?.ProductName ?? item.BranchProductId.ToString()}' - Tồn: {bp.ActualStock}, Cần: {item.Discrepancy}" }
                            };
                        }
                        bp.ActualStock -= item.Discrepancy;
                        bp.AvailableStock -= item.Discrepancy;
                        _context.BranchProducts.Update(bp);
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        var rowsAffected = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                            UPDATE branch_products
                            SET actual_stock = actual_stock - {item.Discrepancy},
                                available_stock = available_stock - {item.Discrepancy}
                            WHERE branch_product_id = {item.BranchProductId}
                              AND actual_stock >= {item.Discrepancy}");

                        if (rowsAffected == 0)
                        {
                            await transaction.RollbackAsync();
                            return new ApiResponse<object>
                            {
                                Success = false,
                                Message = "Số lượng tồn kho khả dụng không đủ.",
                                Errors = new() { $"'{bp.Product?.ProductName ?? item.BranchProductId.ToString()}' - Tồn: {bp.ActualStock}, Cần: {item.Discrepancy}" }
                            };
                        }
                    }
                }
                catch (Exception ex) when (ex is DbUpdateConcurrencyException || ex is DbUpdateException)
                {
                    await transaction.RollbackAsync();
                    return new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Hệ thống đang xử lý một giao dịch kho khác cho sản phẩm này. Vui lòng thử lại sau giây lát.",
                        Errors = new() { ex.Message }
                    };
                }

                var updatedBp = await _context.BranchProducts
                    .FirstOrDefaultAsync(bp2 => bp2.BranchProductId == item.BranchProductId);
                if (updatedBp != null)
                {
                    item.SystemQuantity = updatedBp.ActualStock + item.Discrepancy;
                    item.ActualQuantity = updatedBp.ActualStock;
                }

                var ledger = new InventoryLedger
                {
                    LedgerId = Guid.NewGuid(),
                    BranchId = stockTicket.BranchId,
                    BranchProductId = item.BranchProductId,
                    DocType = stockTicket.TicketType ?? "WRITE_OFF",
                    DocId = stockTicket.StockTicketId,
                    DocCode = stockTicket.TicketCode,
                    TransactionTime = DateTime.UtcNow,
                    QtyChange = -item.Discrepancy,
                    UnitCostPrice = Math.Round(effectiveUnitPrice, 4),
                    TotalCostValue = Math.Round(-item.Discrepancy * effectiveUnitPrice, 4),
                    StockAfter = updatedBp?.ActualStock ?? 0,
                    AvgCostAfter = Math.Round(updatedBp?.CostPrice ?? effectiveUnitPrice, 4),
                    CreatedAt = DateTime.UtcNow
                };
                _context.InventoryLedgers.Add(ledger);
            }

            stockTicket.Status = "COMPLETED";
            stockTicket.CompletedAt = DateTime.UtcNow;
            stockTicket.ConfirmedBy = currentUserId;
            stockTicket.TotalAmount = stockTicket.StockTicketItems.Sum(item =>
                item.Discrepancy * (item.UnitPrice ?? 0));

            // Supplier refund
            if (stockTicket.SupplierId != null && stockTicket.TicketType == "RETURN_SUPPLIER")
            {
                var refundAmount = stockTicket.StockTicketItems.Sum(i => i.Discrepancy * (i.UnitPrice ?? 0));
                if (refundAmount > 0)
                {
                    var supplierPayment = new SupplierPayment
                    {
                        PaymentId = Guid.NewGuid(),
                        SupplierId = stockTicket.SupplierId.Value,
                        Amount = refundAmount,
                        PaymentDate = DateTime.UtcNow,
                        PaymentMethod = "RETURN_REFUND",
                        Note = $"Trả hàng NCC - Phiếu xuất: {stockTicket.TicketCode}",
                        RecordedBy = currentUserId!.Value,
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = 0
                    };
                    _context.SupplierPayments.Add(supplierPayment);
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await WriteSystemLogAsync(currentUserId!.Value, "CONFIRM_OUTWARD_INVENTORY",
                "stock_tickets", "OutwardInventory", stockTicket.StockTicketId,
                $"Xác nhận phiếu xuất kho {stockTicket.TicketCode} - {stockTicket.StockTicketItems.Count} sản phẩm");

            return new ApiResponse<object>
            {
                Success = true,
                Message = "Xác nhận phiếu xuất kho thành công. Tồn kho đã được cập nhật.",
                Data = new
                {
                    TicketId = stockTicket.StockTicketId,
                    TicketCode = stockTicket.TicketCode,
                    Status = stockTicket.Status
                }
            };
        }
        catch (Exception ex) when (ex is not DbUpdateConcurrencyException && ex is not DbUpdateException)
        {
            await transaction.RollbackAsync();
            return new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi hệ thống khi xác nhận phiếu xuất kho. Vui lòng thử lại sau.",
                Errors = new() { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<object>> CancelOutwardInventoryAsync(
        ClaimsPrincipal user, Guid id, CancelTicketDto dto, Guid? branchId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var currentUserId = GetCurrentUserId(user);
            var hasGlobalAccess = HasGlobalAccess(user);

            if (currentUserId == null)
            {
                await transaction.RollbackAsync();
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không thể xác định người dùng từ token."
                };
            }

            var stockTicketQuery = _context.StockTickets
                .Include(st => st.StockTicketItems)
                    .ThenInclude(item => item.BranchProduct)
                .Where(st => st.StockTicketId == id &&
                    (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF"
                        || st.TicketType == "TRANSFER" || st.TicketType == "SALE"));

            if (!hasGlobalAccess)
            {
                if (branchId == null)
                {
                    await transaction.RollbackAsync();
                    return new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không xác định được chi nhánh.",
                        Errors = new() { "Branch context missing" }
                    };
                }
                stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
            }

            var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

            if (stockTicket == null)
            {
                await transaction.RollbackAsync();
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy phiếu xuất kho.",
                    Errors = new() { $"No outward inventory with ID: {id}" }
                };
            }

            bool hasDeletePerm = hasGlobalAccess
                || user.HasClaim("permission", PermissionConstants.STOCK_OUTWARD_DELETE)
                || user.HasClaim("permission", PermissionConstants.STOCK_OUTWARD_CONFIRM);

            bool isCreator = stockTicket.UserId == currentUserId.Value;
            bool isPending = stockTicket.Status?.ToUpperInvariant() == "PENDING";

            if (isPending && !isCreator && !hasDeletePerm)
            {
                await transaction.RollbackAsync();
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Bạn không có quyền hủy phiếu này. Chỉ người tạo phiếu hoặc người có quyền hủy mới được thực hiện."
                };
            }
            if (!isPending && !hasDeletePerm)
            {
                await transaction.RollbackAsync();
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Bạn không có quyền hủy phiếu này. Chỉ Owner hoặc người có quyền hủy mới được thực hiện."
                };
            }

            if (stockTicket.Status?.ToUpperInvariant() == "CANCELLED")
            {
                await transaction.RollbackAsync();
                return new ApiResponse<object>
                {
                    Success = false,
                    Message = "Phiếu đã được hủy trước đó."
                };
            }

            var previousStatus = stockTicket.Status;

            if (stockTicket.Status?.ToUpperInvariant() == "COMPLETED")
            {
                foreach (var item in stockTicket.StockTicketItems)
                {
                    if (item.BranchProduct != null)
                    {
                        item.BranchProduct.ActualStock += item.Discrepancy;
                        item.BranchProduct.AvailableStock += item.Discrepancy;

                        // Ghi bút toán ĐẢO vào inventory_ledger: dòng xuất gốc ghi -discrepancy
                        // -> dòng hủy ghi +discrepancy (nhận hàng về lại kho do hủy phiếu).
                        // Phải cùng DbTransaction với rollback tồn kho. Không có dòng này ->
                        // báo cáo xuất nhập tồn vẫn tính số xuất gốc (sai) & thẻ kho không thấy movement hủy.
                        var cancelUnitPrice = item.UnitPrice ?? 0m;
                        _context.InventoryLedgers.Add(new InventoryLedger
                        {
                            LedgerId = Guid.NewGuid(),
                            BranchId = stockTicket.BranchId,
                            BranchProductId = item.BranchProductId,
                            DocType = stockTicket.TicketType ?? "WRITE_OFF",
                            DocId = stockTicket.StockTicketId,
                            DocCode = stockTicket.TicketCode,
                            TransactionTime = DateTime.UtcNow,
                            QtyChange = item.Discrepancy,
                            UnitCostPrice = Math.Round(cancelUnitPrice, 4),
                            TotalCostValue = Math.Round(item.Discrepancy * cancelUnitPrice, 4),
                            StockAfter = item.BranchProduct.ActualStock,
                            AvgCostAfter = Math.Round(item.BranchProduct.CostPrice, 4),
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            stockTicket.Status = "CANCELLED";
            stockTicket.CancelledAt = DateTime.UtcNow;
            stockTicket.CancelledBy = currentUserId;
            stockTicket.CancelReason = dto?.CancelReason;

            // Nếu hủy phiếu xuất BÁN (SALE) -> đồng bộ hủy hóa đơn + đơn hàng liên quan
            // (SALE ticket.TicketCode = InvoiceCode) để báo cáo doanh thu & thẻ kho nhất quán.
            // Lưu ý: thanh toán (payments) KHÔNG tự hoàn — cần xử lý hoàn tiền riêng qua flow trả hàng.
            if (stockTicket.TicketType == "SALE")
            {
                var linkedInvoice = await _context.Invoices
                    .Include(inv => inv.Order)
                    .FirstOrDefaultAsync(inv => inv.InvoiceCode == stockTicket.TicketCode
                        && inv.Order != null && inv.Order.BranchId == stockTicket.BranchId);
                if (linkedInvoice != null)
                {
                    linkedInvoice.Status = "Cancelled";
                    if (linkedInvoice.Order != null)
                        linkedInvoice.Order.Status = "Cancelled";
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await WriteSystemLogAsync(currentUserId!.Value, "CANCEL_OUTWARD_INVENTORY",
                "stock_tickets", "OutwardInventory", stockTicket.StockTicketId,
                $"Hủy phiếu xuất kho {stockTicket.TicketCode} - Lý do: {dto?.CancelReason ?? "—"}");

            return new ApiResponse<object>
            {
                Success = true,
                Message = previousStatus == "COMPLETED"
                    ? "Hủy phiếu xuất kho và hoàn lại tồn kho thành công."
                    : "Hủy phiếu xuất kho thành công (phiếu chờ, chưa ảnh hưởng tồn kho).",
                Data = new
                {
                    TicketId = stockTicket.StockTicketId,
                    TicketCode = stockTicket.TicketCode,
                    Status = stockTicket.Status
                }
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return new ApiResponse<object>
            {
                Success = false,
                Message = "Lỗi khi thực hiện hủy phiếu xuất kho.",
                Errors = new() { ex.Message }
            };
        }
    }

    public async Task<string> GetPrintHtmlAsync(ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        if (branchId == null)
            throw new UnauthorizedAccessException("JWT token không hợp lệ hoặc thiếu thông tin chi nhánh.");

        var ticket = await _context.StockTickets
            .Include(t => t.Branch)
            .Include(t => t.StockTicketItems).ThenInclude(i => i.BranchProduct).ThenInclude(bp => bp.Product)
            .FirstOrDefaultAsync(t => t.StockTicketId == id && t.BranchId == branchId.Value &&
                (t.TicketType == "RETURN_SUPPLIER" || t.TicketType == "WRITE_OFF" || t.TicketType == "TRANSFER"));

        if (ticket == null)
            throw new KeyNotFoundException($"No outward inventory with ID: {id}");

        var items = ticket.StockTicketItems?.Select(i => new
        {
            i.BranchProduct?.Product?.ProductCode,
            i.BranchProduct?.Product?.ProductName,
            Unit = i.BranchProduct?.Product?.BaseUnit,
            Quantity = i.Discrepancy,
            UnitPrice = i.UnitPrice
        }).ToList() ?? new();

        var ticketTypeName = ticket.TicketType switch
        {
            "RETURN_SUPPLIER" => "TRA HANG NHA CUNG CAP",
            "WRITE_OFF" => "HAO HUT / MAT",
            "TRANSFER" => "SU DUNG NOI BO",
            _ => ticket.TicketType ?? ""
        };

        var html = $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<title>Phieu Xuat Kho - {ticket.TicketCode}</title>
<style>
body {{ font-family: monospace; font-size: 12px; margin: 20px; }}
h1 {{ text-align: center; }}
table {{ width: 100%; border-collapse: collapse; margin-top: 10px; }}
th, td {{ border: 1px solid #000; padding: 5px; text-align: left; }}
th {{ background: #f0f0f0; }}
</style>
</head>
<body>
<h1>PHIEU XUAT KHO</h1>
<p><strong>Loai phieu:</strong> {ticketTypeName}</p>
<p><strong>Ma phieu:</strong> {ticket.TicketCode}</p>
<p><strong>Chi nhanh:</strong> {ticket.Branch?.BranchName}</p>
<p><strong>Ngay tao:</strong> {ticket.CreatedAt:yyyy-MM-dd HH:mm}</p>
<p><strong>Trang thai:</strong> {ticket.Status}</p>
<table>
<tr><th>STT</th><th>Ma SP</th><th>Ten SP</th><th>Don vi</th><th>So luong</th><th>Don gia</th></tr>";

        var stt = 1;
        foreach (var item in items)
        {
            html += $@"<tr>
<td>{stt++}</td>
<td>{item.ProductCode}</td>
<td>{item.ProductName}</td>
<td>{item.Unit}</td>
<td>{item.Quantity}</td>
<td>{item.UnitPrice:N0}</td>
</tr>";
        }

        html += $@"</table>
<p><strong>Ghi chu:</strong> {ticket.Notes ?? ""}</p>
<p style=""margin-top:40px"">Ky ten: __________________</p>
</body>
</html>";

        return html;
    }

    // ========== PRIVATE HELPERS ==========

    private static OutwardInventoryResponseDto MapToResponse(StockTicket stockTicket, ClaimsPrincipal user, Invoice? invoice = null)
    {
        bool canConfirm = false;
        if (stockTicket.Status == "PENDING")
        {
            bool isOwner = user.IsInRole(PermissionConstants.OWNER_ROLE);
            bool hasConfirmPermission = user.HasClaim(
                c => c.Type == PermissionConstants.PERMISSION_CLAIM_TYPE
                  && c.Value == PermissionConstants.STOCK_OUTWARD_CONFIRM);

            canConfirm = isOwner || hasConfirmPermission;
        }

        return new OutwardInventoryResponseDto
        {
            StockTicketId = stockTicket.StockTicketId,
            TicketCode = stockTicket.TicketCode ?? string.Empty,
            TicketType = stockTicket.TicketType ?? string.Empty,
            Status = stockTicket.Status ?? string.Empty,
            BranchId = stockTicket.BranchId,
            UserId = stockTicket.UserId,
            UserName = stockTicket.User?.FullName ?? stockTicket.User?.Username,
            SupplierId = stockTicket.SupplierId,
            SupplierName = stockTicket.Supplier?.SupplierName,
            InwardTicketId = stockTicket.InwardTicketId,
            Reason = stockTicket.Reason,
            Note = stockTicket.Notes,
            CreatedAt = stockTicket.CreatedAt,
            CompletedAt = stockTicket.CompletedAt,
            CancelledAt = stockTicket.CancelledAt,
            ConfirmedBy = stockTicket.ConfirmedBy,
            CancelledBy = stockTicket.CancelledBy,
            CancelReason = stockTicket.CancelReason,
            CanConfirm = canConfirm,
            TotalAmount = invoice != null ? invoice.TotalAmount : stockTicket.TotalAmount,
            Items = stockTicket.StockTicketItems?.Select(item =>
            {
                var invItem = invoice?.InvoiceItems?.FirstOrDefault(ii => ii.BranchProductId == item.BranchProductId);
                var baseUnit = item.BranchProduct?.Product?.BaseUnit ?? item.UnitName ?? "";
                var selectedUnit = invItem?.SelectedUnit ?? item.UnitName ?? baseUnit;
                var convertValue = invItem?.ConvertValue ?? 1m;
                var saleQty = invItem?.Quantity ?? (convertValue > 0 ? item.Discrepancy / convertValue : item.Discrepancy);
                var sellPrice = invItem?.SellPrice ?? (item.UnitPrice.HasValue ? item.UnitPrice.Value * convertValue : 0m);
                var totalPrice = invItem != null ? (invItem.Quantity * invItem.SellPrice) : (item.Discrepancy * (item.UnitPrice ?? 0));

                return new OutwardInventoryItemResponseDto
                {
                    TicketItemId = item.TicketItemId,
                    BranchProductId = item.BranchProductId,
                    ProductId = item.BranchProduct?.ProductId ?? Guid.Empty,
                    ProductCode = item.BranchProduct?.Product?.ProductCode ?? string.Empty,
                    ProductName = item.BranchProduct?.Product?.ProductName ?? string.Empty,
                    SystemQuantity = item.SystemQuantity,
                    Quantity = item.Discrepancy,
                    ActualQuantity = item.ActualQuantity,
                    UnitPrice = invItem != null ? invItem.SellPrice : ((item.UnitPrice ?? 0) > 0 ? item.UnitPrice : (item.BranchProduct?.CostPrice ?? 0)),
                    UnitName = selectedUnit,
                    SelectedUnit = selectedUnit,
                    BaseUnit = baseUnit,
                    ConvertValue = convertValue,
                    SaleQuantity = saleQty,
                    SellPrice = sellPrice,
                    TotalPrice = totalPrice,
                    InwardTicketItemId = item.InwardTicketItemId
                };
            }).ToList() ?? new()
        };
    }
}
