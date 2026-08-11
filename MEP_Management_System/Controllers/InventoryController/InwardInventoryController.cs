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

using MEP_Management_System.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Xuất nhập kho (Inventory)")]
    public class InwardInventoryController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly ICurrentBranchAccessor _currentBranch;
        private readonly IHubContext<InventoryHub> _hub;
        private readonly InwardInventoryQueryService _inwardQueryService;

        public InwardInventoryController(
            MEPDatabaseContext context,
            ICurrentBranchAccessor currentBranch,
            IHubContext<InventoryHub> hub,
            InwardInventoryQueryService inwardQueryService)
        {
            _context = context;
            _currentBranch = currentBranch;
            _hub = hub;
            _inwardQueryService = inwardQueryService;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
        }

        private bool HasGlobalAccess()
        {
            return User.IsInRole("Owner") ;
        }

        private bool IsOwner() => User.IsInRole("Owner");

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
                    Source = "InwardInventoryController",
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
        // Mặc định: CHỈ hiển thị PENDING + COMPLETED
        // Query ?status=CANCELLED hoặc ?status=ALL để xem thêm CANCELLED
        // ============================================================
        [HttpGet]
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
                // Quyen xem: STOCK_INWARD_CREATE hoac STOCK_INWARD_UPDATE
                if (!IsOwner() && !User.HasClaim("permission", PermissionConstants.STOCK_INWARD_CREATE)
                    && !User.HasClaim("permission", PermissionConstants.STOCK_INWARD_UPDATE))
                    return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền xem phiếu nhập kho." });

                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;
                    // Delegate branch + status scoping to service, then apply extra filters and pagination here
                    var list = await _inwardQueryService.GetInwardInventoryAsync(status, User);

                    if (!string.IsNullOrWhiteSpace(ticketType))
                        list = list.Where(st => string.Equals(st.TicketType, ticketType, StringComparison.OrdinalIgnoreCase)).ToList();

                    if (fromDate.HasValue)
                    {
                        var localFrom = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Unspecified);
                        var utcFrom = TimeZoneInfo.ConvertTimeToUtc(localFrom, VietnamTime.Tz);
                        list = list.Where(st => st.CreatedAt >= utcFrom).ToList();
                    }

                    if (toDate.HasValue)
                    {
                        var localTo = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Unspecified);
                        var utcTo = TimeZoneInfo.ConvertTimeToUtc(localTo, VietnamTime.Tz);
                        list = list.Where(st => st.CreatedAt < utcTo).ToList();
                    }

                    var totalCount = list.Count;
                    var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                    var items = list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

                    return Ok(new ApiResponse<PaginatedResponse<InwardInventoryResponseDto>>
                    {
                        Success = true,
                        Message = "Inward inventory history retrieved successfully",
                        Data = new PaginatedResponse<InwardInventoryResponseDto>
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
                    Message = "Error retrieving inward inventory history",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // GET /api/inwardinventory/{id}
        // ============================================================
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetInwardInventoryById(Guid id)
        {
            try
            {
                // Quyen xem: STOCK_INWARD_CREATE hoac STOCK_INWARD_UPDATE
                if (!IsOwner() && !User.HasClaim("permission", PermissionConstants.STOCK_INWARD_CREATE)
                    && !User.HasClaim("permission", PermissionConstants.STOCK_INWARD_UPDATE))
                    return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền xem chi tiết phiếu nhập kho." });

                var stockTicketQuery = _context.StockTickets
                    .Include(st => st.User)
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                            .ThenInclude(bp => bp.Product)
                    .Where(st => st.StockTicketId == id &&
                        (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST") && !(st.Notes != null && st.Notes.Contains("INVENTORY_ADJUSTMENT")));

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
                        Message = "Phiếu nhập kho không tồn tại hoặc đã ở trạng thái không thể chỉnh sửa.",
                        Errors = new() { $"Không tìm thấy phiếu nhập kho với ID: {id}" }
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
        // Tạo phiếu mới với Status = PENDING (chưa cập nhật kho)
        // ============================================================
        [HttpPost]
        [HasPermission(PermissionConstants.STOCK_INWARD_CREATE)]
        public async Task<IActionResult> CreateInwardInventory(
            [FromBody] InwardInventoryCreateDto dto,
            [FromQuery] Guid? branchId = null)
        {
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
            // FIX: Ưu tiên DefaultBranchId từ users table
            // Nếu không có thì mới fallback sang logic cũ
            // ============================================================
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == currentUserId.Value);

            Guid? resolvedBranchId = null;

            // Ưu tiên 1: DefaultBranchId từ user
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
                    Message = "Validation failed",
                    Errors = new() { "Phải có ít nhất 1 sản phẩm." }
                });
            }

            if (dto.InwardType == InwardInventoryType.Purchase && (!dto.SupplierId.HasValue || dto.SupplierId == Guid.Empty))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Validation failed",
                    Errors = new() { "Nhà cung cấp là bắt buộc đối với phiếu nhập mua." }
                });
            }

            // Validate NCC phải đang Active (Status = 1) mới cho tạo phiếu nhập mua.
            if (dto.InwardType == InwardInventoryType.Purchase && dto.SupplierId.HasValue && dto.SupplierId != Guid.Empty)
            {
                var supplier = await _context.Suppliers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SupplierId == dto.SupplierId.Value);
                if (supplier == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = new() { "Không tìm thấy nhà cung cấp." }
                    });
                }
                if (supplier.Status != 1)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Supplier is inactive",
                        Errors = new List<string>
                        {
                            $"Nhà cung cấp '{supplier.SupplierName}' hiện đang ngừng hoạt động (inactive).",
                            "Vui lòng chọn NCC khác đang hoạt động hoặc kích hoạt lại NCC trước khi tạo phiếu nhập."
                        }
                    });
                }
            }

            var itemValidationErrors = new List<string>();
            for (int i = 0; i < dto.Items.Count; i++)
            {
                var item = dto.Items[i];
                var hasId = !string.IsNullOrWhiteSpace(item.Id);
                var hasNewProduct = item.NewProduct != null && !string.IsNullOrWhiteSpace(item.NewProduct.ProductName);
                var hasFlatNewProduct = !string.IsNullOrWhiteSpace(item.ProductCode) && !string.IsNullOrWhiteSpace(item.ProductName);
                if (!hasId && !hasNewProduct && !hasFlatNewProduct)
                    itemValidationErrors.Add($"Dòng {i + 1}: Cần Id (mã sản phẩm) hoặc thông tin sản phẩm mới (productCode + productName).");
                if (item.Quantity <= 0)
                    itemValidationErrors.Add($"Dòng {i + 1}: Số lượng phải lớn hơn 0.");
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

            var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            var ticketCode = GenerateTicketCode(dto.InwardType, currentTime);

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
                    SupplierId = dto.InwardType == InwardInventoryType.Purchase ? dto.SupplierId : null,
                    TicketType = GetTicketTypeName(dto.InwardType),
                    Status = "PENDING", // THAY ĐỔI: Từ COMPLETED -> PENDING
                    Notes = dto.Note ?? dto.Reason,
                    CreatedAt = currentTime
                    // TODO: CompletedAt = null
                    // TODO: CancelledAt = null
                    // TODO: ConfirmedBy = null
                    // TODO: CancelledBy = null
                    // TODO: CancelReason = null
                };

                _context.StockTickets.Add(stockTicket);

                decimal totalAmount = 0;

                foreach (var item in dto.Items)
                {
                    Guid parsedId;
                    bool isGuid = Guid.TryParse(item.Id, out parsedId);

                    // Tìm BranchProduct
                    var branchProduct = await _context.BranchProducts
                        .FirstOrDefaultAsync(bp =>
                            ((isGuid && (bp.BranchProductId == parsedId || bp.ProductId == parsedId))
                            || (!isGuid && bp.Product != null && bp.Product.ProductCode == item.Id))
                            && bp.BranchId == resolvedBranchId.Value);

                    Guid actualBranchProductId;
                    var effectiveCostPrice = item.CostPrice > 0 ? item.CostPrice : (branchProduct?.CostPrice ?? 0);

                    if (branchProduct == null)
                    {
                        Product? product = null;
                        bool isNewProduct = false; // track if product was just created

                        if (!string.IsNullOrWhiteSpace(item.Id))
                        {
                            // Tìm sản phẩm có sẵn theo Id / ProductCode
                            product = await _context.Products.FirstOrDefaultAsync(p =>
                                (isGuid && p.ProductId == parsedId) || (!isGuid && p.ProductCode == item.Id));
                        }

                        if (product == null && item.NewProduct != null)
                        {
                            var newCode = item.NewProduct.ProductCode;
                            var existing = await _context.Products
                                .FirstOrDefaultAsync(p => p.ProductCode == newCode);
                            if (existing != null)
                            {
                                // Sản phẩm cũ đã bị xóa (Status = -1) → đổi mã cũ thành rác, giải phóng mã gốc
                                if (existing.Status == -1)
                                {
                                    existing.ProductCode = $"_DEL_{Guid.NewGuid():N}"[..20];
                                    await _context.SaveChangesAsync();
                                }
                                else
                                {
                                    throw new InvalidOperationException(
                                        $"Mã sản phẩm '{newCode}' đã tồn tại và đang hoạt động. Vui lòng dùng mã khác.");
                                }
                            }

                            // Tạo sản phẩm mới từ NewProduct (nested)
                            product = new Product
                            {
                                ProductId = Guid.NewGuid(),
                                ProductCode = item.NewProduct.ProductCode,
                                ProductName = item.NewProduct.ProductName,
                                BaseUnit = item.NewProduct.Unit,
                                BrandName = item.NewProduct.BrandName,
                                CategoryName = item.NewProduct.CategoryName,
                                Barcode = item.NewProduct.Barcode,
                                SupplierId = item.SupplierId ?? item.NewProduct.SupplierId,
                                Status = 1,
                                SalesChannelLinked = 0,
                                CreatedAt = currentTime,
                                CreatedBy = currentUserId,
                                BranchId = resolvedBranchId
                            };
                            _context.Products.Add(product);
                            try
                            {
                                await _context.SaveChangesAsync();
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidOperationException(
                                    $"Không thể tạo sản phẩm mới '{item.NewProduct.ProductCode}': {ex.InnerException?.Message ?? ex.Message}", ex);
                            }
                            isNewProduct = true;
                        }

                        if (product == null && !string.IsNullOrWhiteSpace(item.ProductCode) && !string.IsNullOrWhiteSpace(item.ProductName))
                        {
                            var newCode = item.ProductCode;
                            var existing = await _context.Products
                                .FirstOrDefaultAsync(p => p.ProductCode == newCode);
                            if (existing != null)
                            {
                                // Sản phẩm cũ đã bị xóa (Status = -1) → đổi mã cũ thành rác, giải phóng mã gốc
                                if (existing.Status == -1)
                                {
                                    existing.ProductCode = $"_DEL_{Guid.NewGuid():N}"[..20];
                                    await _context.SaveChangesAsync();
                                }
                                else
                                {
                                    throw new InvalidOperationException(
                                        $"Mã sản phẩm '{newCode}' đã tồn tại và đang hoạt động. Vui lòng dùng mã khác.");
                                }
                            }

                            // Tạo sản phẩm mới từ flat fields (FE gửi trực tiếp)
                            product = new Product
                            {
                                ProductId = Guid.NewGuid(),
                                ProductCode = item.ProductCode,
                                ProductName = item.ProductName,
                                BaseUnit = item.Unit ?? "Cái",
                                BrandName = item.BrandName,
                                CategoryName = item.CategoryName,
                                Barcode = item.Barcode,
                                Status = 1,
                                SalesChannelLinked = 0,
                                CreatedAt = currentTime,
                                CreatedBy = currentUserId,
                                BranchId = resolvedBranchId
                            };
                            _context.Products.Add(product);
                            try
                            {
                                await _context.SaveChangesAsync();
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidOperationException(
                                    $"Không thể tạo sản phẩm mới '{item.ProductCode}': {ex.InnerException?.Message ?? ex.Message}", ex);
                            }
                            isNewProduct = true;
                        }

                        if (product == null)
                        {
                            throw new InvalidOperationException($"Product/BranchProduct with ID/Code '{item.Id}' was not found in system.");
                        }

                        branchProduct = new BranchProduct
                        {
                            BranchProductId = Guid.NewGuid(),
                            BranchId = resolvedBranchId.Value,
                            ProductId = product.ProductId,
                            CostPrice = effectiveCostPrice,
                            SellPrice = 0,
                            ActualStock = 0,
                            ReservedStock = 0,
                            AvailableStock = 0,
                            IsActive = -1, // An khoi danh sach SP cho den khi xac nhan nhap kho
                            DirectSale = 0
                        };

                        _context.BranchProducts.Add(branchProduct);
                        await _context.SaveChangesAsync();
                        actualBranchProductId = branchProduct.BranchProductId;
                    }
                    else
                    {
                        actualBranchProductId = branchProduct.BranchProductId;
                        // Chỉ set CostPrice lần đầu cho SP mới (CostPrice <= 0), không ghi đè giá vốn hiện tại
                        if (branchProduct.CostPrice <= 0)
                        {
                            branchProduct.CostPrice = effectiveCostPrice;
                        }
                    }

                    // Khách trả hàng không tính vào giá trị nhập
                    if (dto.InwardType != InwardInventoryType.CustomerReturn)
                        totalAmount += item.Quantity * effectiveCostPrice;

                    // Snapshot đơn giá tại thời điểm tạo phiếu - giữ nguyên vẹn, không bị ảnh hưởng bởi CostPrice sau này
                    var ticketItem = new StockTicketItem
                    {
                        TicketItemId = Guid.NewGuid(),
                        StockTicketId = stockTicket.StockTicketId,
                        BranchProductId = actualBranchProductId,
                        SystemQuantity = branchProduct.ActualStock,
                        ActualQuantity = branchProduct.ActualStock,
                        Discrepancy = item.Quantity,
                        UnitPrice = effectiveCostPrice // Snapshot giá nhập tại thời điểm tạo phiếu
                    };

                    _context.StockTicketItems.Add(ticketItem);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await TryWriteSystemLog(
                    currentUserId.Value,
                    "CREATE_INWARD_INVENTORY",
                    "stock_tickets",
                    "InwardInventory",
                    stockTicket.StockTicketId,
                    $"Tạo phiếu nhập kho {stockTicket.TicketCode}");

                var userName = User.FindFirst("name")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "Người dùng";
                await BroadcastAndSaveNotification(
                    NotificationType.Created,
                    resolvedBranchId.Value,
                    $"{userName} vừa tạo phiếu nhập kho {stockTicket.TicketCode}.",
                    currentUserId.Value,
                    stockTicket.StockTicketId,
                    "InwardInventory");

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Inward inventory created successfully. Please confirm to update stock.",
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
        // Chỉ cho phép sửa khi Status = PENDING
        // ============================================================
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateInwardInventory(Guid id, [FromBody] InwardInventoryUpdateDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var stockTicketQuery = _context.StockTickets
                    .Include(st => st.StockTicketItems)
                    .Where(st => st.StockTicketId == id &&
                        (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST") && !(st.Notes != null && st.Notes.Contains("INVENTORY_ADJUSTMENT")));

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
                        Message = "Phiếu nhập kho không tồn tại hoặc đã ở trạng thái không thể chỉnh sửa.",
                        Errors = new() { $"Không tìm thấy phiếu nhập kho với ID: {id}" }
                    });
                }

                // Kiem tra quyen: nguoi tao PENDING hoac co UPDATE hoac Owner
                bool canUpdate = IsOwner() || User.HasClaim("permission", PermissionConstants.STOCK_INWARD_UPDATE);
                if (!canUpdate && stockTicket.UserId != currentUserId.Value)
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
                    if (dto.Note != null)
                        stockTicket.Notes = dto.Note;
                    if (dto.Reason != null)
                        stockTicket.Reason = dto.Reason;

                    await _context.SaveChangesAsync();

                    return Ok(new ApiResponse<InwardInventoryResponseDto>
                    {
                        Success = true,
                        Message = "Inward inventory updated (notes only for completed tickets)",
                        Data = MapToInwardInventoryResponse(stockTicket)
                    });
                }

                // PENDING: Cho phép sửa tất cả (Items, Note, Reason, Status)
                if (dto.Note != null)
                    stockTicket.Notes = dto.Note;
                if (dto.Reason != null)
                    stockTicket.Reason = dto.Reason;

                if (!string.IsNullOrWhiteSpace(dto.Status))
                {
                    var normalized = dto.Status.Trim().ToUpperInvariant();
                    // Block "COMPLETED" — bắt buộc phải đi qua /confirm để cộng kho
                    if (normalized is "PENDING" or "CANCELLED")
                        stockTicket.Status = normalized;
                }

                await _context.SaveChangesAsync();

                await TryWriteSystemLog(
                    currentUserId!.Value,
                    "UPDATE_INWARD_INVENTORY",
                    "stock_tickets",
                    "InwardInventory",
                    stockTicket.StockTicketId,
                    $"Cập nhật phiếu nhập kho {stockTicket.TicketCode}");

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
        // POST /api/inwardinventory/{id}/confirm
        // Xác nhận phiếu nhập kho - Cập nhật tồn kho và đổi sang COMPLETED
        // ============================================================
        [HttpPost("{id:guid}/confirm")]
        [HasPermission(PermissionConstants.STOCK_INWARD_UPDATE)]
        public async Task<IActionResult> ConfirmInwardInventory(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var stockTicketQuery = _context.StockTickets
                    .Include(st => st.StockTicketItems)
                        .ThenInclude(item => item.BranchProduct)
                    .Where(st => st.StockTicketId == id &&
                        (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST") && !(st.Notes != null && st.Notes.Contains("INVENTORY_ADJUSTMENT")));

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
                        Message = "Phiếu nhập kho không tồn tại hoặc đã ở trạng thái không thể chỉnh sửa.",
                        Errors = new() { $"Không tìm thấy phiếu nhập kho với ID: {id}" }
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
                if (stockTicket.StockTicketItems == null || !stockTicket.StockTicketItems.Any())
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Cannot confirm ticket without items",
                        Errors = new() { "Phiếu không có sản phẩm nào. Vui lòng thêm sản phẩm trước khi xác nhận." }
                    });
                }

                // ============================================================
                // QUY TẮC MỚI: Cập nhật tồn kho bằng Atomic SQL Update
                // Tránh race condition khi 2 user cùng thao tác trên 1 sản phẩm
                // Đồng thời snapshot UnitPrice và tính giá vốn bình quân di động
                // ============================================================
                var currentUserId = GetCurrentUserId();

                foreach (var item in stockTicket.StockTicketItems)
                {
                    // Snapshot đơn giá nhập nếu chưa có (tương thích ngược với phiếu cũ)
                    var unitPrice = item.UnitPrice > 0 ? item.UnitPrice.Value : 0m;
                    if (unitPrice == 0)
                    {
                        var bpForPrice = await _context.BranchProducts
                            .FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId);
                        unitPrice = bpForPrice?.CostPrice ?? 0;
                        item.UnitPrice = unitPrice;
                    }

                    // Atomic UPDATE: cộng tồn kho trực tiếp trong DB, tránh xung đột
                    try
                    {
                        // CUSTOMER_RETURN: không tính lại cost_price để tránh làm loãng giá vốn bình quân
                        if (stockTicket.TicketType == "CUSTOMER_RETURN")
                        {
                            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                                UPDATE branch_products
                                SET actual_stock = actual_stock + {item.Discrepancy},
                                    available_stock = available_stock + {item.Discrepancy},
                                    is_active = CASE WHEN is_active = -1 THEN 1 ELSE is_active END,
                                    direct_sale = CASE WHEN is_active = -1 THEN 1 ELSE direct_sale END
                                WHERE branch_product_id = {item.BranchProductId}
                            ");
                        }
                        else
                        {
                            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                                UPDATE branch_products
                                SET actual_stock = actual_stock + {item.Discrepancy},
                                    available_stock = available_stock + {item.Discrepancy},
                                    is_active = CASE WHEN is_active = -1 THEN 1 ELSE is_active END,
                                    direct_sale = CASE WHEN is_active = -1 THEN 1 ELSE direct_sale END,
                                    cost_price = CASE
                                        WHEN (actual_stock + {item.Discrepancy}) > 0
                                        THEN ROUND((
                                            (actual_stock * cost_price) + ({item.Discrepancy} * {unitPrice})
                                        ) / (actual_stock + {item.Discrepancy}), 2)
                                        ELSE {unitPrice}
                                    END
                                WHERE branch_product_id = {item.BranchProductId}
                            ");
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

                    // Đọc lại tồn kho sau khi cập nhật để ghi nhận vào ticket item
                    var updatedBp = await _context.BranchProducts
                        .FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId);
                    if (updatedBp != null)
                    {
                        item.ActualQuantity = updatedBp.ActualStock;
                        item.SystemQuantity = updatedBp.ActualStock - item.Discrepancy;

                        // Gán SupplierId cho Product nếu chưa có
                        if (stockTicket.SupplierId.HasValue)
                        {
                            var product = await _context.Products
                                .FirstOrDefaultAsync(p => p.ProductId == updatedBp.ProductId);
                            if (product != null && product.SupplierId == null)
                            {
                                product.SupplierId = stockTicket.SupplierId;
                            }
                        }
                    }

                    // Ghi inventory_ledger
                    var ledger = new InventoryLedger
                    {
                        LedgerId = Guid.NewGuid(),
                        BranchId = stockTicket.BranchId,
                        BranchProductId = item.BranchProductId,
                        DocType = stockTicket.TicketType ?? "PURCHASE",
                        DocId = stockTicket.StockTicketId,
                        DocCode = stockTicket.TicketCode,
                        TransactionTime = DateTime.UtcNow,
                        QtyChange = item.Discrepancy,
                        UnitCostPrice = Math.Round(unitPrice, 4),
                        TotalCostValue = Math.Round(item.Discrepancy * unitPrice, 4),
                        StockAfter = updatedBp?.ActualStock ?? 0,
                        AvgCostAfter = Math.Round(updatedBp?.CostPrice ?? unitPrice, 4),
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.InventoryLedgers.Add(ledger);
                }

                // Chuyển trạng thái sang COMPLETED
                stockTicket.Status = "COMPLETED";
                stockTicket.CompletedAt = DateTime.UtcNow;
                stockTicket.ConfirmedBy = currentUserId;
                if (stockTicket.TicketType == "PURCHASE" && stockTicket.TotalAmount == 0)
                {
                    stockTicket.TotalAmount = stockTicket.StockTicketItems.Sum(item =>
                        item.Discrepancy * (item.UnitPrice ?? 0));
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await TryWriteSystemLog(
                    currentUserId!.Value,
                    "CONFIRM_INWARD_INVENTORY",
                    "stock_tickets",
                    "InwardInventory",
                    stockTicket.StockTicketId,
                    $"Xác nhận phiếu nhập kho {stockTicket.TicketCode}");

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Xác nhận phiếu nhập kho thành công. Tồn kho đã được cập nhật.",
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
                    Message = "Lỗi hệ thống khi xác nhận phiếu nhập kho. Vui lòng thử lại sau.",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // POST /api/inwardinventory/{id}/cancel
        // Hủy phiếu nhập kho - Xử lý rollback an toàn
        // Quyen huy:
        // - PENDING: nguoi tao phieu hoac co STOCK_INWARD_DELETE hoac Owner
        // - COMPLETED: chi co STOCK_INWARD_DELETE hoac Owner
        // ============================================================
        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> CancelInwardInventory(Guid id, [FromBody] CancelTicketDto dto)
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
                        (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST") && !(st.Notes != null && st.Notes.Contains("INVENTORY_ADJUSTMENT")));

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
                        Message = "Phiếu nhập kho không tồn tại hoặc đã ở trạng thái không thể chỉnh sửa.",
                        Errors = new() { $"Không tìm thấy phiếu nhập kho với ID: {id}" }
                    });
                }

                // Kiem tra quyen huy
                bool hasDeletePerm = IsOwner() || User.HasClaim("permission", PermissionConstants.STOCK_INWARD_DELETE)
                    || User.HasClaim("permission", PermissionConstants.STOCK_INWARD_UPDATE);
                bool isCreator = stockTicket.UserId == currentUserId.Value;
                bool isPending = stockTicket.Status?.ToUpperInvariant() == "PENDING";

                if (isPending && !isCreator && !hasDeletePerm)
                {
                    return StatusCode(403, new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Bạn không có quyền hủy phiếu này. Chỉ người tạo phiếu hoặc người có quyền hủy mới được thực hiện."
                    });
                }
                if (!isPending && !hasDeletePerm)
                {
                    return StatusCode(403, new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Bạn không có quyền hủy phiếu này. Chỉ Owner hoặc người có quyền hủy mới được thực hiện."
                    });
                }

                if (stockTicket.Status?.ToUpperInvariant() == "CANCELLED")
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Ticket already cancelled",
                        Errors = new() { "Phiếu đã được hủy trước đó." }
                    });
                }

                // ============================================================
                // QUY TẮC MỚI: Xử lý theo trạng thái hiện tại
                // ============================================================
                if (stockTicket.Status == "COMPLETED")
                {
                    // COMPLETED: Cần rollback tồn kho (trừ lại)
                    // Nhưng phải kiểm tra an toàn: không cho phép ActualStock < 0

                    var unsafeItems = new List<string>();

                    foreach (var item in stockTicket.StockTicketItems)
                    {
                        var branchProduct = await _context.BranchProducts
                            .FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId);

                        if (branchProduct != null)
                        {
                            // Kiểm tra: nếu trừ kho sẽ âm không?
                            if (branchProduct.ActualStock < item.Discrepancy ||
                                branchProduct.AvailableStock < item.Discrepancy)
                            {
                                unsafeItems.Add(branchProduct.Product?.ProductName ?? item.BranchProductId.ToString());
                            }
                        }
                    }

                    if (unsafeItems.Any())
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Không thể hủy vì hàng đã được xuất khỏi kho.",
                            Errors = new List<string>
                            {
                                "Không thể hủy vì hàng đã được xuất khỏi kho.",
                                $"Sản phẩm bị ảnh hưởng: {string.Join(", ", unsafeItems)}"
                            }
                        });
                    }

                    // Đủ điều kiện - Thực hiện rollback
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

                // Lưu trạng thái TRƯỚC KHI đổi để response message chính xác
                var wasCompleted = stockTicket.Status == "COMPLETED";

                // PENDING hoặc COMPLETED (đã rollback): Đều chuyển sang CANCELLED
                stockTicket.Status = "CANCELLED";
                stockTicket.CancelledAt = DateTime.UtcNow;
                stockTicket.CancelledBy = currentUserId;
                stockTicket.CancelReason = dto?.CancelReason;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await TryWriteSystemLog(
                    currentUserId!.Value,
                    "CANCEL_INWARD_INVENTORY",
                    "stock_tickets",
                    "InwardInventory",
                    stockTicket.StockTicketId,
                    $"Hủy phiếu nhập kho {stockTicket.TicketCode} - Lý do: {dto?.CancelReason ?? "—"}");

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = wasCompleted
                        ? "Inward inventory cancelled and stock rolled back successfully"
                        : "Inward inventory cancelled successfully (pending ticket, no stock change)",
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
                    Message = "Error cancelling inward inventory",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // DELETE /api/inwardinventory/{id}
        // ⚠️ DEPRECATED: Chỉ giữ lại cho tương thích FE cũ.
        // FE mới nên dùng: POST /api/inwardinventory/{id}/cancel
        // ============================================================
        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionConstants.STOCK_INWARD_DELETE)]
        public async Task<IActionResult> DeleteInwardInventory(Guid id)
        {
            // Gọi nội bộ vào Cancel với CancelReason mặc định
            var cancelDto = new CancelTicketDto
            {
                CancelReason = "Hủy qua API DELETE (legacy)"
            };
            return await CancelInwardInventory(id, cancelDto);
        }

        // ============================================================
        // GET /api/inwardinventory/{id}/print
        // Trả về HTML phiếu nhập kho
        // ============================================================
        [HttpGet("{id:guid}/print")]
        [HasPermission(PermissionConstants.STOCK_INWARD_CREATE)]
        public async Task<IActionResult> Print(Guid id)
        {
            var branchId = _currentBranch.GetBranchId();
            if (branchId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "JWT token is missing or invalid",
                    Errors = new() { "Authorization header required" }
                });
            }

            var ticket = await _context.StockTickets
                .Include(t => t.Branch)
                .Include(t => t.StockTicketItems).ThenInclude(i => i.BranchProduct).ThenInclude(bp => bp.Product)
                .Include(t => t.Supplier)
                .FirstOrDefaultAsync(t => t.StockTicketId == id && t.BranchId == branchId.Value);

            if (ticket == null)
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Inward inventory not found",
                    Errors = new() { $"No inward inventory with ID: {id}" }
                });

            var items = ticket.StockTicketItems?.Select(i => new
            {
                i.BranchProduct?.Product?.ProductCode,
                i.BranchProduct?.Product?.ProductName,
                Unit = i.BranchProduct?.Product?.BaseUnit,
                Quantity = i.Discrepancy,
                i.BranchProduct?.CostPrice
            }).ToList() ?? new();

            var html = $@"<!DOCTYPE html>
<html>
<head>
<meta charset=""utf-8"">
<title>Phieu Nhap Kho - {ticket.TicketCode}</title>
<style>
body {{ font-family: monospace; font-size: 12px; margin: 20px; }}
h1 {{ text-align: center; }}
table {{ width: 100%; border-collapse: collapse; margin-top: 10px; }}
th, td {{ border: 1px solid #000; padding: 5px; text-align: left; }}
th {{ background: #f0f0f0; }}
</style>
</head>
<body>
<h1>PHIEU NHAP KHO</h1>
<p><strong>Ma phieu:</strong> {ticket.TicketCode}</p>
<p><strong>Chi nhanh:</strong> {ticket.Branch?.BranchName}</p>
<p><strong>Nha cung cap:</strong> {ticket.Supplier?.SupplierName}</p>
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
<td>{item.CostPrice:N0}</td>
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
                SupplierId = stockTicket.SupplierId,
                UserName = stockTicket.User?.FullName ?? stockTicket.User?.Username,
                Reason = stockTicket.Reason,
                Note = stockTicket.Notes,
                CreatedAt = stockTicket.CreatedAt,
                CompletedAt = stockTicket.CompletedAt,
                CancelledAt = stockTicket.CancelledAt,
                ConfirmedBy = stockTicket.ConfirmedBy,
                CancelledBy = stockTicket.CancelledBy,
                CancelReason = stockTicket.CancelReason,
                CostPrice = stockTicket.TicketType == "CUSTOMER_RETURN"
                    ? 0
                    : stockTicket.StockTicketItems?.Sum(i =>
                        (i.UnitPrice ?? 0) > 0 ? (i.UnitPrice ?? 0) : (i.BranchProduct?.CostPrice ?? 0)
                      ) ?? 0,
                Items = stockTicket.StockTicketItems?.Select(item =>
                {
                    var effectivePrice = (item.UnitPrice ?? 0) > 0
                        ? (item.UnitPrice ?? 0)
                        : (item.BranchProduct?.CostPrice ?? 0);
                    return new InwardInventoryItemResponseDto
                    {
                        TicketItemId = item.TicketItemId,
                        BranchProductId = item.BranchProductId,
                        ProductId = item.BranchProduct?.ProductId ?? Guid.Empty,
                        ProductCode = item.BranchProduct?.Product?.ProductCode ?? string.Empty,
                        ProductName = item.BranchProduct?.Product?.ProductName ?? string.Empty,
                        SystemQuantity = item.SystemQuantity,
                        Quantity = item.Discrepancy,
                        ActualQuantity = item.ActualQuantity,
                        CostPrice = stockTicket.TicketType == "CUSTOMER_RETURN"
                            ? 0
                            : effectivePrice,
                        UnitPrice = effectivePrice,
                        UnitName = item.BranchProduct?.Product?.BaseUnit
                    };
                }).ToList() ?? new()
            };
        }

        #region Return-to-Supplier Support

        /// <summary>Lấy danh sách phiếu nhập đã hoàn thành của 1 NCC (để chọn khi xuất trả).</summary>
        [HttpGet("by-supplier/{supplierId:guid}")]
        [HasPermission("STOCK_VIEW")]
        public async Task<IActionResult> GetBySupplier(Guid supplierId, [FromQuery] Guid? branchId)
        {
            var jwtBranchId = _currentBranch.GetBranchId();
            var targetBranchId = (HasGlobalAccess() ? branchId : null) ?? jwtBranchId;
            if (targetBranchId == null || targetBranchId == Guid.Empty)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

            var tickets = await _context.StockTickets
                .Where(st => st.SupplierId == supplierId
                    && st.Status == "COMPLETED"
                    && st.TicketType == "PURCHASE"
                    && st.BranchId == targetBranchId.Value)
                .OrderByDescending(st => st.CreatedAt)
                .Select(st => new InwardTicketForReturnDto
                {
                    StockTicketId = st.StockTicketId,
                    TicketCode = st.TicketCode ?? string.Empty,
                    CreatedAt = st.CreatedAt,
                    TotalAmount = st.TotalAmount,
                    ItemsCount = st.StockTicketItems.Count
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<InwardTicketForReturnDto>>
            {
                Success = true,
                Message = "Lấy danh sách phiếu nhập thành công.",
                Data = tickets
            });
        }

        /// <summary>Lấy danh sách sản phẩm còn có thể trả của 1 phiếu nhập.</summary>
        [HttpGet("{id:guid}/returnable-items")]
        [HasPermission("STOCK_VIEW")]
        public async Task<IActionResult> GetReturnableItems(Guid id)
        {
            var inwardTicket = await _context.StockTickets
                .Include(st => st.StockTicketItems)
                    .ThenInclude(si => si.BranchProduct)
                        .ThenInclude(bp => bp.Product)
                .FirstOrDefaultAsync(st => st.StockTicketId == id
                    && st.Status == "COMPLETED"
                    && st.TicketType == "PURCHASE");

            if (inwardTicket == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy phiếu nhập hoặc phiếu chưa hoàn thành." });

            var inwardItemIds = inwardTicket.StockTicketItems.Select(si => si.TicketItemId).ToList();

            // Tính tổng số lượng đã trả cho từng dòng nhập
            var returnedQuantities = await _context.StockTicketItems
                .Where(si => si.InwardTicketItemId != null && inwardItemIds.Contains(si.InwardTicketItemId.Value)
                    && si.StockTicket!.Status != "CANCELLED")
                .GroupBy(si => si.InwardTicketItemId!.Value)
                .Select(g => new { InwardTicketItemId = g.Key, TotalReturned = g.Sum(si => si.Discrepancy) })
                .ToListAsync();

            var returnedDict = returnedQuantities.ToDictionary(r => r.InwardTicketItemId, r => r.TotalReturned);

            var items = inwardTicket.StockTicketItems.Select(item =>
            {
                var totalReturned = returnedDict.GetValueOrDefault(item.TicketItemId, 0m);
                var maxReturnable = item.Discrepancy - totalReturned;
                return new ReturnableItemDto
                {
                    InwardTicketItemId = item.TicketItemId,
                    BranchProductId = item.BranchProductId,
                    ProductId = item.BranchProduct?.ProductId ?? Guid.Empty,
                    ProductCode = item.BranchProduct?.Product?.ProductCode ?? string.Empty,
                    ProductName = item.BranchProduct?.Product?.ProductName ?? string.Empty,
                    UnitName = item.BranchProduct?.Product?.BaseUnit,
                    OriginalQuantity = item.Discrepancy,
                    UnitPrice = item.UnitPrice ?? 0,
                    MaxReturnableQuantity = maxReturnable > 0 ? maxReturnable : 0,
                    ActualStock = item.BranchProduct?.ActualStock ?? 0
                };
            })
            .Where(i => i.MaxReturnableQuantity > 0)
            .ToList();

            return Ok(new ApiResponse<List<ReturnableItemDto>>
            {
                Success = true,
                Message = "Lấy danh sách sản phẩm có thể trả thành công.",
                Data = items
            });
        }

        #endregion
    }
}