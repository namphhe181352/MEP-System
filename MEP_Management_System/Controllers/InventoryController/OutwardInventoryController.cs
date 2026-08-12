using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

using MEP_Management_System.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Xuất nhập kho (Inventory)")]
    public class OutwardInventoryController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly ICurrentBranchAccessor _currentBranch;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IHubContext<InventoryHub> _hub;

        public OutwardInventoryController(
            MEPDatabaseContext context,
            ICurrentBranchAccessor currentBranch,
            IHttpContextAccessor httpContextAccessor,
            IHubContext<InventoryHub> hub)
        {
            _context = context;
            _currentBranch = currentBranch;
            _httpContextAccessor = httpContextAccessor;
            _hub = hub;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
        }

        private bool HasGlobalAccess()
        {
            return User.IsInRole("Owner");
        }

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
                    Source = "OutwardInventoryController",
                    Timestamp = DateTime.UtcNow
                };
                _context.SystemLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Silent — không làm fail request chính
            }
        }

        private async Task BroadcastAndSaveNotification(
            NotificationType type,
            Guid branchId,
            string message,
            Guid triggeredByUserId,
            Guid? referenceId = null,
            string? referenceType = null)
        {
            var notif = new Notification
            {
                NotificationId = Guid.NewGuid(),
                Type = type,
                RecipientUserId = null, // Broadcast
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

        // ============================================================
        // GET /api/outwardinventory
        // Mặc định: CHỈ hiển thị PENDING + COMPLETED
        // Query ?status=CANCELLED hoặc ?status=ALL để xem thêm CANCELLED
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetOutwardInventories(
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
                // Quyen xem: STOCK_OUTWARD_CREATE hoac STOCK_OUTWARD_CONFIRM
                if (!HasGlobalAccess() && !User.HasClaim("permission", PermissionConstants.STOCK_OUTWARD_CREATE)
                    && !User.HasClaim("permission", PermissionConstants.STOCK_OUTWARD_CONFIRM))
                    return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền xem phiếu xuất kho." });

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var query = _context.StockTickets
                    .Include(st => st.User)
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                            .ThenInclude(bp => bp.Product)
                    .Where(st => st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER")
                    .AsQueryable();

                // Lọc theo branch từ token - áp dụng cho TẤT CẢ user (bao gồm Owner)
                var staffBranchId = _currentBranch.GetBranchId();
                if (staffBranchId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Tài khoản của bạn chưa được cấu hình chi nhánh hợp lệ.",
                        Errors = new() { "Branch context is missing in token." }
                    });
                }
                query = query.Where(st => st.BranchId == staffBranchId.Value);

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

                return Ok(new ApiResponse<PaginatedResponse<OutwardInventoryResponseDto>>
                {
                    Success = true,
                    Message = "Lấy lịch sử xuất kho thành công.",
                    Data = new PaginatedResponse<OutwardInventoryResponseDto>
                    {
                        Items = items.Select(MapToOutwardInventoryResponse).ToList(),
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
                    Message = "Lỗi khi lấy dữ liệu xuất kho.",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // GET /api/outwardinventory/{id}
        // ============================================================
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetOutwardInventoryById(Guid id)
        {
            try
            {
                if (!HasGlobalAccess() && !User.HasClaim("permission", PermissionConstants.STOCK_OUTWARD_CREATE)
                    && !User.HasClaim("permission", PermissionConstants.STOCK_OUTWARD_CONFIRM))
                    return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền xem chi tiết phiếu xuất kho." });

                var stockTicketQuery = _context.StockTickets
                    .Include(st => st.User)
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                            .ThenInclude(bp => bp.Product)
                    .Where(st => st.StockTicketId == id &&
                        (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER"));

                if (!HasGlobalAccess())
                {
                    var staffBranchId = _currentBranch.GetBranchId();
                    if (staffBranchId == null)
                    {
                        return Unauthorized(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "JWT token không hợp lệ hoặc thiếu thông tin chi nhánh.",
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
                        Message = "Không tìm thấy phiếu xuất kho hoặc bạn không có quyền xem phiếu này.",
                        Errors = new() { $"No outward inventory with ID: {id}" }
                    });
                }

                return Ok(new ApiResponse<OutwardInventoryResponseDto>
                {
                    Success = true,
                    Message = "Lấy chi tiết phiếu xuất kho thành công.",
                    Data = MapToOutwardInventoryResponse(stockTicket)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi hệ thống khi lấy chi tiết phiếu.",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // POST /api/outwardinventory
        // Tạo phiếu mới với Status = PENDING (chưa cập nhật kho)
        // ============================================================
        [HttpPost]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_CREATE)]
        public async Task<IActionResult> CreateOutwardInventory(
            [FromBody] OutwardInventoryCreateDto dto,
            [FromQuery] Guid? branchId = null)
        {
            try
            {
                // Lấy currentUserId trước
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không xác định được định danh người dùng từ Token.",
                        Errors = new() { "User identity missing." }
                    });
                }

                // ============================================================
                // FIX: Resolve branchId - Ưu tiên DefaultBranchId từ users table
                // Giống InwardInventory
                // ============================================================
                Guid? resolvedBranchId = null;

                // Ưu tiên 1: DefaultBranchId từ user
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == currentUserId.Value);

                if (user?.DefaultBranchId != null && user.DefaultBranchId != Guid.Empty)
                {
                    resolvedBranchId = user.DefaultBranchId;
                }
                // Ưu tiên 2: branchId từ query param (Owner/Admin)
                else if (HasGlobalAccess() && branchId.HasValue)
                {
                    resolvedBranchId = branchId.Value;
                }
                // Ưu tiên 3: BranchId từ JWT token
                else if (_currentBranch.GetBranchId().HasValue)
                {
                    resolvedBranchId = _currentBranch.GetBranchId();
                }
                // Ưu tiên 4: Lấy chi nhánh active DUY NHẤT trong hệ thống
                else
                {
                    var activeBranch = await _context.Branches
                        .AsNoTracking()
                        .Where(b => b.IsActive == 1)
                        .FirstOrDefaultAsync();
                    resolvedBranchId = activeBranch?.BranchId;
                }

                if (resolvedBranchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ.",
                        Errors = new() { "No valid branch found." }
                    });
                }

                if (dto.Items == null || dto.Items.Count == 0)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ",
                        Errors = new() { "Yêu cầu có ít nhất 1 mặt hàng để xuất kho." }
                    });
                }

                if (string.IsNullOrWhiteSpace(dto.Reason))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ",
                        Errors = new() { "Trường 'reason' là bắt buộc." }
                    });
                }

                var normalizedReason = dto.Reason.Trim();
                var isValidReason = dto.OutwardType switch
                {
                    OutwardInventoryType.ReturnToSupplier => new[]
                    {
                        "Xuất trả nhà cung cấp",
                        "Trả hàng nhà cung cấp",
                        "Xuất trả NCC"
                    }.Any(reason => string.Equals(reason, normalizedReason, StringComparison.OrdinalIgnoreCase)),
                    OutwardInventoryType.DamagedHaoHut => new[]
                    {
                        "Xuất hủy / Hao hụt",
                        "Xuất hủy"
                    }.Any(reason => string.Equals(reason, normalizedReason, StringComparison.OrdinalIgnoreCase)),
                    OutwardInventoryType.InternalUse => new[]
                    {
                        "Xuất sử dụng nội bộ",
                        "Xuất nội bộ / Điều chuyển"
                    }.Any(reason => string.Equals(reason, normalizedReason, StringComparison.OrdinalIgnoreCase)),
                    _ => false
                };

                if (!isValidReason)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ",
                        Errors = new()
                        {
                            "Lý do xuất kho không hợp lệ. Chỉ chấp nhận: Xuất trả nhà cung cấp, Xuất hủy / Hao hụt, Xuất sử dụng nội bộ."
                        }
                    });
                }

                // === Validate phiếu nhập tham chiếu (Xuất trả NCC) ===
                var validationErrors = new List<string>();
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
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Phiếu nhập tham chiếu không tồn tại hoặc chưa hoàn thành."
                        });
                    }

                    if (dto.SupplierId.HasValue && inwardTicket.SupplierId != dto.SupplierId.Value)
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Phiếu nhập không thuộc về nhà cung cấp đã chọn."
                        });
                    }

                    // Lưu đơn giá gốc từ phiếu nhập để validate
                    inwardUnitPrices = inwardTicket.StockTicketItems
                        .ToDictionary(i => i.TicketItemId, i => i.UnitPrice ?? 0);

                    // Tính tổng số lượng đã trả cho từng dòng nhập
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

                    // Validate từng item có InwardTicketItemId
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
                }

                var productIds = dto.Items.Select(i => i.BranchProductId).Distinct().ToList();
                var branchProducts = await _context.BranchProducts
                    .Where(bp => (productIds.Contains(bp.ProductId) || productIds.Contains(bp.BranchProductId)) && bp.BranchId == resolvedBranchId.Value)
                    .Include(bp => bp.Product)
                    .ToListAsync();

                if (branchProducts.Count != productIds.Count)
                {
                    var foundProductIds = branchProducts.Select(bp => bp.ProductId).ToHashSet();
                    var foundBranchProductIds = branchProducts.Select(bp => bp.BranchProductId).ToHashSet();
                    var missingIds = productIds.Where(id => !foundProductIds.Contains(id) && !foundBranchProductIds.Contains(id)).ToList();

                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Yêu cầu không hợp lệ",
                        Errors = new() { $"Một hoặc nhiều sản phẩm không tồn tại trong hệ thống. (IDs: {string.Join(", ", missingIds)})" }
                    });
                }

                // Kiểm tra số lượng hợp lệ
                var effectiveUnitPriceByBranchProductId = new Dictionary<Guid, decimal>();

                foreach (var item in dto.Items)
                {
                    if (item.Quantity <= 0)
                    {
                        validationErrors.Add($"Số lượng xuất của sản phẩm phải lớn hơn 0.");
                    }

                    var bp = branchProducts.FirstOrDefault(x => x.BranchProductId == item.BranchProductId || x.ProductId == item.BranchProductId);
                    if (bp == null)
                    {
                        validationErrors.Add($"Sản phẩm '{item.BranchProductId}' không thuộc chi nhánh hiện tại.");
                        continue;
                    }

                    if (item.Quantity > bp.ActualStock)
                    {
                        validationErrors.Add($"Số lượng tồn kho khả dụng của sản phẩm '{bp.Product?.ProductName ?? bp.ProductId.ToString()}' không đủ để thực hiện xuất kho. Tồn hiện tại: {bp.ActualStock}, cần xuất: {item.Quantity}.");
                    }

                    var fallbackUnitPrice = item.UnitPrice.HasValue && item.UnitPrice.Value > 0
                        ? item.UnitPrice.Value
                        : (item.InwardTicketItemId.HasValue && inwardUnitPrices != null
                            && inwardUnitPrices.TryGetValue(item.InwardTicketItemId.Value, out var iup) && iup > 0
                            ? iup
                            : bp.CostPrice > 0
                                ? bp.CostPrice
                                : 0m);

                    if (fallbackUnitPrice <= 0)
                    {
                        validationErrors.Add($"Sản phẩm {bp.Product?.ProductName ?? bp.ProductId.ToString()} chưa có giá vốn hợp lệ để xuất kho.");
                    }

                    effectiveUnitPriceByBranchProductId[bp.BranchProductId] = fallbackUnitPrice;
                }

                if (validationErrors.Count > 0)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ.",
                        Errors = validationErrors
                    });
                }

                var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                var ticketCode = GenerateTicketCode(dto.OutwardType, currentTime);

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
                            UnitName = bp.Product?.BaseUnit ?? "" // Snapshot tên đơn vị cơ bản
                        };

                        _context.StockTicketItems.Add(ticketItem);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await TryWriteSystemLog(
                        currentUserId.Value,
                        "CREATE_OUTWARD_INVENTORY",
                        "stock_tickets",
                        "OutwardInventory",
                        stockTicket.StockTicketId,
                        $"Tạo phiếu xuất kho {stockTicket.TicketCode}");

                    var userName = User.FindFirst("name")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "Người dùng";
                    await BroadcastAndSaveNotification(
                        NotificationType.Created,
                        resolvedBranchId.Value,
                        $"{userName} vừa tạo phiếu xuất kho {stockTicket.TicketCode}.",
                        currentUserId.Value,
                        stockTicket.StockTicketId,
                        "OutwardInventory");

                    return Ok(new ApiResponse<object>
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
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Gặp lỗi khi tạo phiếu xuất kho.",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // PUT /api/outwardinventory/{id}
        // Chỉ cho phép sửa khi Status = PENDING
        // ============================================================
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateOutwardInventory(Guid id, [FromBody] OutwardInventoryUpdateDto dto)
        {
            try
            {
                var stockTicketQuery = _context.StockTickets
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                    .Where(st => st.StockTicketId == id &&
                        (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER"));

                if (!HasGlobalAccess())
                {
                    var staffBranchId = _currentBranch.GetBranchId();
                    if (staffBranchId == null)
                    {
                        return Unauthorized(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Không có quyền thực hiện hành động này.",
                            Errors = new() { "Branch context missing" }
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
                        Message = "Không tìm thấy phiếu cần cập nhật hoặc bạn không có quyền sửa đổi phiếu này.",
                        Errors = new() { $"No outward inventory with ID: {id}" }
                    });
                }

                // Kiem tra quyen: nguoi tao PENDING hoac co CONFIRM hoac Owner
                var currentUserId = GetCurrentUserId();
                bool canUpdate = HasGlobalAccess() || User.HasClaim("permission", PermissionConstants.STOCK_OUTWARD_CONFIRM);
                if (!canUpdate && stockTicket.UserId != currentUserId)
                    return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền chỉnh sửa phiếu này." });

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
                    if (dto.Note != null)
                        stockTicket.Notes = dto.Note;
                    if (dto.Reason != null)
                        stockTicket.Reason = dto.Reason;

                    await _context.SaveChangesAsync();

                    await TryWriteSystemLog(
                        stockTicket.UserId,
                        "UPDATE_OUTWARD_INVENTORY",
                        "stock_tickets",
                        "OutwardInventory",
                        stockTicket.StockTicketId,
                        $"Cập nhật phiếu xuất kho {stockTicket.TicketCode}");

                    return Ok(new ApiResponse<OutwardInventoryResponseDto>
                    {
                        Success = true,
                        Message = "Cập nhật thông tin phiếu thành công (chỉ ghi chú cho phiếu đã xác nhận).",
                        Data = MapToOutwardInventoryResponse(stockTicket)
                    });
                }

                // PENDING: Cho phép sửa tất cả (Items, Note, Reason, Status)
                if (dto.Note != null)
                    stockTicket.Notes = dto.Note;
                if (dto.Reason != null)
                    stockTicket.Reason = dto.Reason;

                // ============================================================
                // BUG 2 FIX: Chỉ cho phép set CANCELLED, KHÔNG cho set COMPLETED
                // Bypass /confirm sẽ không cập nhật được tồn kho
                // ============================================================
                if (!string.IsNullOrWhiteSpace(dto.Status))
                {
                    var normalized = dto.Status.Trim().ToUpperInvariant();
                    if (normalized == "CANCELLED")
                        stockTicket.Status = "CANCELLED";
                }

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<OutwardInventoryResponseDto>
                {
                    Success = true,
                    Message = "Cập nhật thông tin phiếu thành công.",
                    Data = MapToOutwardInventoryResponse(stockTicket)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi cập nhật phiếu xuất kho.",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // POST /api/outwardinventory/{id}/confirm
        // Xác nhận phiếu xuất kho - Kiểm tra tồn kho & cập nhật kho
        // ============================================================
        [HttpPost("{id:guid}/confirm")]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_CONFIRM)]
        public async Task<IActionResult> ConfirmOutwardInventory(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var stockTicketQuery = _context.StockTickets
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                            .ThenInclude(bp => bp.Product)
                    .Where(st => st.StockTicketId == id &&
                        (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER"));

                if (!HasGlobalAccess())
                {
                    var staffBranchId = _currentBranch.GetBranchId();
                    if (staffBranchId == null)
                    {
                        return Unauthorized(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Không xác định được chi nhánh.",
                            Errors = new() { "Branch context missing" }
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
                        Message = "Không tìm thấy phiếu xuất kho.",
                        Errors = new() { $"No outward inventory with ID: {id}" }
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
                if (stockTicket.StockTicketItems == null || stockTicket.StockTicketItems.Count == 0)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Cannot confirm ticket without items",
                        Errors = new() { "Phiếu không có sản phẩm nào. Vui lòng thêm sản phẩm trước khi xác nhận." }
                    });
                }

                // ============================================================
                // QUY TẮC MỚI: Kiểm tra tồn kho thời gian thực (Real-time Validation)
                // ============================================================
                var insufficientStockItems = new List<string>();

                // Query lại dữ liệu tồn kho mới nhất từ Database
                var branchProductIds = stockTicket.StockTicketItems.Select(i => i.BranchProductId).ToList();
                var latestBranchProducts = await _context.BranchProducts
                    .Where(bp => branchProductIds.Contains(bp.BranchProductId))
                    .Include(bp => bp.Product)
                    .ToDictionaryAsync(bp => bp.BranchProductId);

                // ============================================================
                // BUG 3 FIX: Race condition - Thêm row-level lock FOR UPDATE
                // Khóa các dòng để ngăn 2 request đồng thời vượt qua validation
                // ============================================================
                if (branchProductIds.Any() && _context.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
                {
                    // EF Core cannot parameterize a list of Guids for the IN clause via FromSqlInterpolated
                    // in a way that's compatible with the FOR UPDATE locking hint. We build the SQL
                    // explicitly, but each Guid is validated as a real Guid and string-formatted with
                    // the standard "D" format (lowercase 32-hex + hyphens), making injection impossible
                    // (the values cannot contain SQL meta-characters).
                    var uuidArray = string.Join(",", branchProductIds.Select(id => $"'{id.ToString("D")}'"));
#pragma warning disable EF1002 // SQL injection risk is mitigated by Guid.ToString("D") format
                    await _context.Database.ExecuteSqlRawAsync(
                        $"SELECT 1 FROM branch_products WHERE branch_product_id IN ({uuidArray}) FOR UPDATE");
#pragma warning restore EF1002
                }

                foreach (var item in stockTicket.StockTicketItems)
                {
                    if (latestBranchProducts.TryGetValue(item.BranchProductId, out var bp))
                    {
                        var currentStock = bp.ActualStock;
                        if (currentStock < item.Discrepancy)
                        {
                            insufficientStockItems.Add(
                                $"{bp.Product?.ProductName ?? item.BranchProductId.ToString()} " +
                                $"(tồn: {currentStock}, cần xuất: {item.Discrepancy})"
                            );
                        }
                    }
                }

                if (insufficientStockItems.Count > 0)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không đủ tồn kho để xuất",
                        Errors = new List<string>
                        {
                            "Không đủ tồn kho để xuất",
                            $"Sản phẩm không đủ: {string.Join("; ", insufficientStockItems)}"
                        }
                    });
                }

                // ============================================================
                // QUY TẮC MỚI: Cập nhật tồn kho bằng Atomic SQL Update
                // Tránh race condition khi 2 user cùng thao tác trên 1 sản phẩm
                // ============================================================
                foreach (var item in stockTicket.StockTicketItems)
                {
                    if (!latestBranchProducts.TryGetValue(item.BranchProductId, out var bp))
                        continue;

                    // Snapshot unit_price từ BQ CostPrice cho WRITE_OFF / TRANSFER nếu chưa có
                    if ((item.UnitPrice ?? 0) <= 0
                        && stockTicket.TicketType != "RETURN_SUPPLIER")
                    {
                        item.UnitPrice = bp.CostPrice;
                    }
                    var effectiveUnitPrice = item.UnitPrice ?? bp.CostPrice;

                    // Atomic UPDATE: trừ tồn kho trực tiếp trong DB, có kiểm tra tồn >= SL xuất
                    try
                    {
                        if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
                        {
                            if (bp.ActualStock < item.Discrepancy)
                            {
                                await transaction.RollbackAsync();
                                return BadRequest(new ApiResponse<object>
                                {
                                    Success = false,
                                    Message = "Số lượng tồn kho khả dụng của sản phẩm không đủ để thực hiện xuất kho.",
                                    Errors = new()
                                    {
                                        $"Sản phẩm '{bp.Product?.ProductName ?? item.BranchProductId.ToString()}' không đủ tồn kho. Tồn hiện tại: {bp.ActualStock}, cần xuất: {item.Discrepancy}"
                                    }
                                });
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
                                  AND actual_stock >= {item.Discrepancy}
                            ");

                            if (rowsAffected == 0)
                            {
                                await transaction.RollbackAsync();
                                return BadRequest(new ApiResponse<object>
                                {
                                    Success = false,
                                    Message = "Số lượng tồn kho khả dụng của sản phẩm không đủ để thực hiện xuất kho.",
                                    Errors = new()
                                    {
                                        $"Sản phẩm '{bp.Product?.ProductName ?? item.BranchProductId.ToString()}' không đủ tồn kho. Tồn hiện tại: {bp.ActualStock}, cần xuất: {item.Discrepancy}"
                                    }
                                });
                            }
                        }
                    }
                    catch (Exception ex) when (ex is DbUpdateConcurrencyException || ex is DbUpdateException)
                    {
                        await transaction.RollbackAsync();
                        return StatusCode(409, new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Hệ thống đang xử lý một giao dịch kho khác cho sản phẩm này. Vui lòng thử lại sau giây lát.",
                            Errors = new() { ex.Message }
                        });
                    }

                    // Đọc lại tồn kho sau khi cập nhật
                    var updatedBp = await _context.BranchProducts
                        .FirstOrDefaultAsync(bp2 => bp2.BranchProductId == item.BranchProductId);
                    if (updatedBp != null)
                    {
                        item.SystemQuantity = updatedBp.ActualStock + item.Discrepancy;
                        item.ActualQuantity = updatedBp.ActualStock;
                    }

                    // Ghi inventory_ledger (qty_change âm cho xuất kho)
                    var ledger = new InventoryLedger
                    {
                        LedgerId = Guid.NewGuid(),
                        BranchId = stockTicket.BranchId,
                        BranchProductId = item.BranchProductId,
                        DocType = stockTicket.TicketType ?? "WRITE_OFF",
                        DocId = stockTicket.StockTicketId,
                        DocCode = stockTicket.TicketCode,
                        TransactionTime = DateTime.UtcNow,
                        QtyChange = -item.Discrepancy, // ÂM cho xuất kho
                        UnitCostPrice = Math.Round(effectiveUnitPrice, 4),
                        TotalCostValue = Math.Round(-item.Discrepancy * effectiveUnitPrice, 4),
                        StockAfter = updatedBp?.ActualStock ?? 0,
                        AvgCostAfter = Math.Round(updatedBp?.CostPrice ?? effectiveUnitPrice, 4),
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.InventoryLedgers.Add(ledger);
                }

                // Chuyển trạng thái sang COMPLETED
                var currentUserId = GetCurrentUserId();
                stockTicket.Status = "COMPLETED";
                stockTicket.CompletedAt = DateTime.UtcNow;
                stockTicket.ConfirmedBy = currentUserId;
                stockTicket.TotalAmount = stockTicket.StockTicketItems.Sum(item =>
                    item.Discrepancy * (item.UnitPrice ?? 0));

                // ============================================================
                // TRỪ NỢ NCC: Tạo SupplierPayment khi xuất trả hàng cho NCC
                // ============================================================
                if (stockTicket.SupplierId != null && stockTicket.TicketType == "RETURN_SUPPLIER")
                {
                    var refundAmount = stockTicket.StockTicketItems
                        .Sum(i => i.Discrepancy * (i.UnitPrice ?? 0));

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

                await TryWriteSystemLog(
                    currentUserId!.Value,
                    "CONFIRM_OUTWARD_INVENTORY",
                    "stock_tickets",
                    "OutwardInventory",
                    stockTicket.StockTicketId,
                    $"Xác nhận phiếu xuất kho {stockTicket.TicketCode} - {stockTicket.StockTicketItems.Count} sản phẩm");

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Xác nhận phiếu xuất kho thành công. Tồn kho đã được cập nhật.",
                    Data = new
                    {
                        TicketId = stockTicket.StockTicketId,
                        TicketCode = stockTicket.TicketCode,
                        Status = stockTicket.Status
                    }
                });
            }
            catch (Exception ex) when (ex is not DbUpdateConcurrencyException && ex is not DbUpdateException)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi hệ thống khi xác nhận phiếu xuất kho. Vui lòng thử lại sau.",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // POST /api/outwardinventory/{id}/cancel
        // Hủy phiếu xuất kho - Xử lý rollback tồn kho
        // Quyen huy:
        // - PENDING: nguoi tao phieu hoac co STOCK_OUTWARD_DELETE hoac Owner
        // - COMPLETED: chi co STOCK_OUTWARD_DELETE hoac Owner
        // ============================================================
        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> CancelOutwardInventory(Guid id, [FromBody] CancelTicketDto dto)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không thể xác định người dùng từ token."
                });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var stockTicketQuery = _context.StockTickets
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                    .Where(st => st.StockTicketId == id &&
                        (st.TicketType == "RETURN_SUPPLIER" || st.TicketType == "WRITE_OFF" || st.TicketType == "TRANSFER"));

                if (!HasGlobalAccess())
                {
                    var staffBranchId = _currentBranch.GetBranchId();
                    if (staffBranchId == null)
                    {
                        return Unauthorized(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Không xác định được chi nhánh.",
                            Errors = new() { "Branch context missing" }
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
                        Message = "Không tìm thấy phiếu xuất kho.",
                        Errors = new() { $"No outward inventory with ID: {id}" }
                    });
                }

                // ============================================================
                // KIEM TRA QUYEN HUY
                // ============================================================
                bool hasDeletePerm = HasGlobalAccess() || User.HasClaim("permission", PermissionConstants.STOCK_OUTWARD_DELETE)
                    || User.HasClaim("permission", PermissionConstants.STOCK_OUTWARD_CONFIRM);
                bool isCreator = stockTicket.UserId == currentUserId.Value;
                bool isPending = stockTicket.Status?.ToUpperInvariant() == "PENDING";

                if (isPending && !isCreator && !hasDeletePerm)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(403, new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Bạn không có quyền hủy phiếu này. Chỉ người tạo phiếu hoặc người có quyền hủy mới được thực hiện."
                    });
                }
                if (!isPending && !hasDeletePerm)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(403, new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Bạn không có quyền hủy phiếu này. Chỉ Owner hoặc người có quyền hủy mới được thực hiện."
                    });
                }

                // ============================================================
                // QUY TẮC MỚI: Kiểm tra trạng thái
                // ============================================================
                if (stockTicket.Status?.ToUpperInvariant() == "CANCELLED")
                {
                    await transaction.RollbackAsync();
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Ticket already cancelled",
                        Errors = new() { "Phiếu đã được hủy trước đó." }
                    });
                }

                var previousStatus = stockTicket.Status;

                // COMPLETED: đã trừ tồn kho → phải cộng lại
                if (stockTicket.Status?.ToUpperInvariant() == "COMPLETED")
                {
                    foreach (var item in stockTicket.StockTicketItems)
                    {
                        if (item.BranchProduct != null)
                        {
                            item.BranchProduct.ActualStock += item.Discrepancy;
                            item.BranchProduct.AvailableStock += item.Discrepancy;
                        }
                    }
                }
                stockTicket.Status = "CANCELLED";
                stockTicket.CancelledAt = DateTime.UtcNow;
                stockTicket.CancelledBy = currentUserId;
                stockTicket.CancelReason = dto?.CancelReason;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await TryWriteSystemLog(
                    currentUserId!.Value,
                    "CANCEL_OUTWARD_INVENTORY",
                    "stock_tickets",
                    "OutwardInventory",
                    stockTicket.StockTicketId,
                    $"Hủy phiếu xuất kho {stockTicket.TicketCode} - Lý do: {dto?.CancelReason ?? "—"}");

                return Ok(new ApiResponse<object>
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
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi thực hiện hủy phiếu xuất kho.",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // DELETE /api/outwardinventory/{id}
        // ⚠️ DEPRECATED: Chỉ giữ lại cho tương thích FE cũ.
        // FE mới nên dùng: POST /api/outwardinventory/{id}/cancel
        // ============================================================
        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_DELETE)]
        public async Task<IActionResult> DeleteOutwardInventory(Guid id)
        {
            // Gọi nội bộ vào Cancel với CancelReason mặc định
            var cancelDto = new CancelTicketDto
            {
                CancelReason = "Hủy qua API DELETE (legacy)"
            };
            return await CancelOutwardInventory(id, cancelDto);
        }

        // ============================================================
        // GET /api/outwardinventory/{id}/print
        // Tra ve HTML phieu xuat kho
        // ============================================================
        [HttpGet("{id:guid}/print")]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_CREATE)]
        public async Task<IActionResult> Print(Guid id)
        {
            var staffBranchId = _currentBranch.GetBranchId();
            if (staffBranchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "JWT token khong hop le hoac thieu thong tin chi nhanh.",
                    Errors = new() { "Authorization header required" }
                });
            }

            var ticket = await _context.StockTickets
                .Include(t => t.Branch)
                .Include(t => t.StockTicketItems).ThenInclude(i => i.BranchProduct).ThenInclude(bp => bp.Product)
                .FirstOrDefaultAsync(t => t.StockTicketId == id && t.BranchId == staffBranchId.Value &&
                    (t.TicketType == "RETURN_SUPPLIER" || t.TicketType == "WRITE_OFF" || t.TicketType == "TRANSFER"));

            if (ticket == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Khong tim thay phieu xuat kho hoac ban khong co quyen xem phieu nay.",
                    Errors = new() { $"No outward inventory with ID: {id}" }
                });

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

            return Content(html, "text/html");
        }

        // ========== HELPER METHODS ==========

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
            // ============================================================
            // THAY ĐỔI NGHIỆP VỤ: Tính CanConfirm dựa trên permission
            // ============================================================
            bool canConfirm = false;
            if (stockTicket.Status == "PENDING")
            {
                // Owner luôn được confirm
                bool isOwner = User.IsInRole(PermissionConstants.OWNER_ROLE);

                // Hoặc có permission STOCK_OUTWARD_CONFIRM
                bool hasConfirmPermission = User.HasClaim(
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
                Items = stockTicket.StockTicketItems?.Select(item => new OutwardInventoryItemResponseDto
                {
                    TicketItemId = item.TicketItemId,
                    BranchProductId = item.BranchProductId,
                    ProductId = item.BranchProduct?.ProductId ?? Guid.Empty,
                    ProductCode = item.BranchProduct?.Product?.ProductCode ?? string.Empty,
                    ProductName = item.BranchProduct?.Product?.ProductName ?? string.Empty,
                    SystemQuantity = item.SystemQuantity,
                    Quantity = item.Discrepancy,
                    ActualQuantity = item.ActualQuantity,
                    UnitPrice = (item.UnitPrice ?? 0) > 0 ? item.UnitPrice : (item.BranchProduct?.CostPrice ?? 0),
                    UnitName = item.UnitName ?? item.BranchProduct?.Product?.BaseUnit,
                    InwardTicketItemId = item.InwardTicketItemId
                }).ToList() ?? new()
            };
        }
    }
}
