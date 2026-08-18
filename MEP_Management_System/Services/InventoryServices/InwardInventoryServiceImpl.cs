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

public class InwardInventoryServiceImpl : IInwardInventoryService
{
    private readonly MEPDatabaseContext _context;
    private readonly ICurrentBranchAccessor _currentBranch;
    private readonly IHubContext<InventoryHub> _hub;
    private readonly InwardInventoryQueryService _inwardQueryService;

    public InwardInventoryServiceImpl(
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

    private Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst("userId")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
    }

    private bool IsOwner(ClaimsPrincipal user) => user.IsInRole("Owner");

    private bool HasAnyPermission(ClaimsPrincipal user, params string[] permissions)
        => permissions.Any(p => user.HasClaim("permission", p));

    private bool CanViewInward(ClaimsPrincipal user)
        => IsOwner(user) || HasAnyPermission(user, PermissionConstants.STOCK_INWARD_CREATE, PermissionConstants.STOCK_INWARD_UPDATE);

    private static DateTime UtcNow() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    private static DateTime? ComputeWarrantyExpiry(DateTime baseDate, int period, string unit)
    {
        if (period <= 0) return null;
        return (unit?.ToUpper()) switch
        {
            "DAY" => baseDate.AddDays(period),
            "YEAR" => baseDate.AddYears(period),
            _ => baseDate.AddMonths(period)
        };
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
                Source = "InwardInventoryService",
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

    public async Task<ApiResponse<PaginatedResponse<InwardInventoryResponseDto>>> GetInwardInventoriesAsync(
        ClaimsPrincipal user, Guid? branchId, string? status, string? ticketType,
        DateTime? fromDate, DateTime? toDate, int pageNumber, int pageSize)
    {
        if (!CanViewInward(user))
            throw new BusinessException("Bạn không có quyền xem phiếu nhập kho.");

        var list = await _inwardQueryService.GetInwardInventoryAsync(status, user);

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

        return new ApiResponse<PaginatedResponse<InwardInventoryResponseDto>>
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
        };
    }

    public async Task<ApiResponse<InwardInventoryResponseDto>> GetInwardInventoryByIdAsync(ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        if (!CanViewInward(user))
            throw new BusinessException("Bạn không có quyền xem chi tiết phiếu nhập kho.");

        var stockTicketQuery = _context.StockTickets
            .Include(st => st.User)
            .Include(st => st.StockTicketItems).ThenInclude(item => item.BranchProduct).ThenInclude(bp => bp.Product)
            .Where(st => st.StockTicketId == id &&
                (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST"));

        if (!IsOwner(user))
        {
            if (branchId == null)
                throw new UnauthorizedAccessException("JWT token is missing or invalid");
            stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
        }

        var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

        if (stockTicket == null)
            throw new KeyNotFoundException($"Không tìm thấy phiếu nhập kho với ID: {id}");

        return new ApiResponse<InwardInventoryResponseDto>
        {
            Success = true,
            Message = "Inward inventory retrieved successfully",
            Data = MapToInwardInventoryResponse(stockTicket)
        };
    }

    public async Task<ApiResponse<object>> CreateInwardInventoryAsync(ClaimsPrincipal user, InwardInventoryCreateDto dto, Guid? branchIdOverride)
    {
        var currentUserId = GetCurrentUserId(user);
        if (currentUserId == null)
            throw new UnauthorizedAccessException("Không xác định được định danh người dùng từ Token.");

        // ============================================================
        // FIX: Uu tien DefaultBranchId tu users table
        // Neu khong co thi moi fallback sang logic cu
        // ============================================================
        var userEntity = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == currentUserId.Value);
        Guid? resolvedBranchId = null;

        // Uu tien 1: DefaultBranchId tu user
        if (userEntity?.DefaultBranchId != null && userEntity.DefaultBranchId != Guid.Empty)
            resolvedBranchId = userEntity.DefaultBranchId;
        // Uu tien 2: branchId tu query param (Owner/Admin)
        else if (IsOwner(user) && branchIdOverride.HasValue)
            resolvedBranchId = branchIdOverride.Value;
        // Uu tien 3: BranchId tu JWT token
        else if (_currentBranch.GetBranchId().HasValue)
            resolvedBranchId = _currentBranch.GetBranchId();
        // Uu tien 4: Lay chi nhanh active DUY NHAT trong he thong
        else
        {
            var activeBranch = await _context.Branches.AsNoTracking().Where(b => b.IsActive == 1).FirstOrDefaultAsync();
            resolvedBranchId = activeBranch?.BranchId;
        }

        if (resolvedBranchId == null)
            throw new BusinessException("Không tìm thấy chi nhánh hợp lệ.");

        if (dto.Items == null || dto.Items.Count == 0)
            throw new BusinessException("Validation failed", "Phải có ít nhất 1 sản phẩm.");

        if (dto.InwardType == InwardInventoryType.Purchase && (!dto.SupplierId.HasValue || dto.SupplierId == Guid.Empty))
            throw new BusinessException("Validation failed", "Nhà cung cấp là bắt buộc đối với phiếu nhập mua.");

        // Validate NCC phai dang Active (Status = 1) moi cho tao phieu nhap mua.
        if (dto.InwardType == InwardInventoryType.Purchase && dto.SupplierId.HasValue && dto.SupplierId != Guid.Empty)
        {
            var supplier = await _context.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.SupplierId == dto.SupplierId.Value);
            if (supplier == null)
                throw new BusinessException("Không tìm thấy nhà cung cấp.");
            if (supplier.Status != 1)
                throw new BusinessException("Supplier is inactive", $"Nhà cung cấp '{supplier.SupplierName}' hiện đang ngừng hoạt động (inactive). Vui lòng chọn NCC khác đang hoạt động hoặc kích hoạt lại NCC trước khi tạo phiếu nhập.");
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
            throw new BusinessException("Validation failed", itemValidationErrors);

        var currentTime = UtcNow();
        var ticketCode = GenerateTicketCode(dto.InwardType, currentTime);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // ============================================================
            // QUY TAC MOI: Tao phieu voi Status = PENDING
            // KHONG cap nhat ton kho tai day
            // ============================================================
            var stockTicket = new StockTicket
            {
                StockTicketId = Guid.NewGuid(),
                TicketCode = ticketCode,
                BranchId = resolvedBranchId.Value,
                UserId = currentUserId.Value,
                SupplierId = dto.InwardType == InwardInventoryType.Purchase ? dto.SupplierId : null,
                TicketType = GetTicketTypeName(dto.InwardType),
                Status = "PENDING",
                Notes = dto.Note ?? dto.Reason,
                CreatedAt = currentTime
            };

            _context.StockTickets.Add(stockTicket);

            foreach (var item in dto.Items)
            {
                Guid parsedId;
                bool isGuid = Guid.TryParse(item.Id, out parsedId);

                // FE (dac biet Excel import) co the gui flat productCode/productName thay vi id
                // Chuẩn hoa: thieu id thi dung productCode lam id de resolve BranchProduct/Product theo ma
                if (string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.ProductCode))
                {
                    item.Id = item.ProductCode;
                    isGuid = Guid.TryParse(item.Id, out parsedId);
                }

                var branchProduct = await _context.BranchProducts
                    .Include(bp => bp.Product)
                    .FirstOrDefaultAsync(bp =>
                        ((isGuid && (bp.BranchProductId == parsedId || bp.ProductId == parsedId))
                        || (!isGuid && bp.Product != null && bp.Product.ProductCode == item.Id))
                        && bp.BranchId == resolvedBranchId.Value);

                Guid actualBranchProductId;
                var effectiveCostPrice = item.CostPrice > 0 ? item.CostPrice : (branchProduct?.CostPrice ?? 0);

                if (branchProduct == null)
                {
                    Product? product = null;

                    if (!string.IsNullOrWhiteSpace(item.Id))
                        product = await _context.Products.FirstOrDefaultAsync(p =>
                            (isGuid && p.ProductId == parsedId) || (!isGuid && p.ProductCode == item.Id));

                    if (product == null && item.NewProduct != null)
                    {
                        var newCode = item.NewProduct.ProductCode;
                        var existing = await _context.Products.FirstOrDefaultAsync(p => p.ProductCode == newCode);
                        if (existing != null)
                        {
                            // San pham cu da bi xoa (Status = -1) -> doi ma cu thanh rac, giai phong ma goc
                            if (existing.Status == -1)
                            {
                                existing.ProductCode = $"_DEL_{Guid.NewGuid():N}"[..20];
                                await _context.SaveChangesAsync();
                            }
                            else
                            {
                                throw new InvalidOperationException($"Mã sản phẩm '{newCode}' đã tồn tại và đang hoạt động. Vui lòng dùng mã khác.");
                            }
                        }

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
                    }

                    if (product == null && !string.IsNullOrWhiteSpace(item.ProductCode) && !string.IsNullOrWhiteSpace(item.ProductName))
                    {
                        var newCode = item.ProductCode;
                        var existing = await _context.Products.FirstOrDefaultAsync(p => p.ProductCode == newCode);
                        if (existing != null)
                        {
                            // San pham cu da bi xoa (Status = -1) -> doi ma cu thanh rac, giai phong ma goc
                            if (existing.Status == -1)
                            {
                                existing.ProductCode = $"_DEL_{Guid.NewGuid():N}"[..20];
                                await _context.SaveChangesAsync();
                            }
                            else
                            {
                                throw new InvalidOperationException($"Mã sản phẩm '{newCode}' đã tồn tại và đang hoạt động. Vui lòng dùng mã khác.");
                            }
                        }

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
                    }

                    if (product == null)
                        throw new InvalidOperationException($"Product/BranchProduct with ID/Code '{item.Id}' was not found in system.");

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
                    // Chi set CostPrice lan dau cho SP moi (CostPrice <= 0), khong ghi de gia von hien tai
                    if (branchProduct.CostPrice <= 0)
                        branchProduct.CostPrice = effectiveCostPrice;
                }

                // Snapshot don gia tai thoi diem tao phieu - giu nguyen ven
                var ticketItem = new StockTicketItem
                {
                    TicketItemId = Guid.NewGuid(),
                    StockTicketId = stockTicket.StockTicketId,
                    BranchProductId = actualBranchProductId,
                    SystemQuantity = branchProduct.ActualStock,
                    ActualQuantity = branchProduct.ActualStock,
                    Discrepancy = item.Quantity,
                    UnitPrice = effectiveCostPrice,
                    UnitName = branchProduct.Product?.BaseUnit ?? "",
                    WarrantyPeriod = item.WarrantyPeriod > 0 ? item.WarrantyPeriod : (branchProduct.Product?.WarrantyPeriod ?? 0),
                    WarrantyUnit = !string.IsNullOrEmpty(item.WarrantyUnit) ? item.WarrantyUnit : (branchProduct.Product?.WarrantyUnit ?? "MONTH"),
                    WarrantyExpiryDate = ComputeWarrantyExpiry(DateTime.UtcNow,
                        item.WarrantyPeriod > 0 ? item.WarrantyPeriod : (branchProduct.Product?.WarrantyPeriod ?? 0),
                        !string.IsNullOrEmpty(item.WarrantyUnit) ? item.WarrantyUnit : (branchProduct.Product?.WarrantyUnit ?? "MONTH"))
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

            var userName = user.FindFirst("name")?.Value ?? user.FindFirst(ClaimTypes.Name)?.Value ?? "Người dùng";
            await BroadcastAndSaveNotification(
                NotificationType.Created,
                resolvedBranchId.Value,
                $"{userName} vừa tạo phiếu nhập kho {stockTicket.TicketCode}.",
                currentUserId.Value,
                stockTicket.StockTicketId,
                "InwardInventory");

            return new ApiResponse<object>
            {
                Success = true,
                Message = "Inward inventory created successfully. Please confirm to update stock.",
                Data = new { TicketId = stockTicket.StockTicketId, TicketCode = stockTicket.TicketCode, TicketType = stockTicket.TicketType, BranchId = stockTicket.BranchId, Status = stockTicket.Status }
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse<InwardInventoryResponseDto>> UpdateInwardInventoryAsync(ClaimsPrincipal user, Guid id, InwardInventoryUpdateDto dto, Guid? branchId)
    {
        var currentUserId = GetCurrentUserId(user);
        var stockTicketQuery = _context.StockTickets
            .Include(st => st.StockTicketItems)
            .Where(st => st.StockTicketId == id &&
                (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST"));

        if (!IsOwner(user))
        {
            if (branchId == null)
                throw new UnauthorizedAccessException("JWT token is missing or invalid");
            stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
        }

        var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

        if (stockTicket == null)
            throw new KeyNotFoundException($"Không tìm thấy phiếu nhập kho với ID: {id}");

        // Kiem tra quyen: nguoi tao PENDING hoac co UPDATE hoac Owner
        bool canUpdate = IsOwner(user) || user.HasClaim("permission", PermissionConstants.STOCK_INWARD_UPDATE);
        if (!canUpdate && currentUserId.HasValue && stockTicket.UserId != currentUserId.Value)
            throw new BusinessException("Bạn không có quyền chỉnh sửa phiếu này.");

        if (stockTicket.Status == "CANCELLED")
            throw new BusinessException("Cannot update cancelled ticket", "Phiếu đã bị hủy. Không thể chỉnh sửa.");

        if (stockTicket.Status == "COMPLETED")
        {
            if (dto.Note != null) stockTicket.Notes = dto.Note;
            if (dto.Reason != null) stockTicket.Reason = dto.Reason;
            await _context.SaveChangesAsync();
            return new ApiResponse<InwardInventoryResponseDto> { Success = true, Message = "Inward inventory updated (notes only for completed tickets)", Data = MapToInwardInventoryResponse(stockTicket) };
        }

        // PENDING: Cho phep sua tat ca (Items, Note, Reason, Status)
        if (dto.Note != null) stockTicket.Notes = dto.Note;
        if (dto.Reason != null) stockTicket.Reason = dto.Reason;

        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            var normalized = dto.Status.Trim().ToUpperInvariant();
            // Block "COMPLETED" - bat buoc phai di qua /confirm de cong kho
            if (normalized is "PENDING" or "CANCELLED")
                stockTicket.Status = normalized;
        }

        await _context.SaveChangesAsync();

        if (currentUserId.HasValue)
        {
            await TryWriteSystemLog(
                currentUserId.Value,
                "UPDATE_INWARD_INVENTORY",
                "stock_tickets",
                "InwardInventory",
                stockTicket.StockTicketId,
                $"Cập nhật phiếu nhập kho {stockTicket.TicketCode}");
        }

        return new ApiResponse<InwardInventoryResponseDto> { Success = true, Message = "Inward inventory updated successfully", Data = MapToInwardInventoryResponse(stockTicket) };
    }

    public async Task<ApiResponse<object>> ConfirmInwardInventoryAsync(ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        var currentUserId = GetCurrentUserId(user);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var stockTicketQuery = _context.StockTickets
                .Include(st => st.StockTicketItems).ThenInclude(item => item.BranchProduct)
                .Where(st => st.StockTicketId == id &&
                    (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST"));

            if (!IsOwner(user))
            {
                if (branchId == null)
                    throw new UnauthorizedAccessException("JWT token is missing or invalid");
                stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
            }

            var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

            if (stockTicket == null)
                throw new KeyNotFoundException($"Không tìm thấy phiếu nhập kho với ID: {id}");

            if (stockTicket.Status == "COMPLETED")
                throw new BusinessException("Ticket already confirmed", "Phiếu đã được xác nhận trước đó.");

            if (stockTicket.Status == "CANCELLED")
                throw new BusinessException("Cannot confirm cancelled ticket", "Phiếu đã bị hủy. Không thể xác nhận.");

            if (stockTicket.StockTicketItems == null || !stockTicket.StockTicketItems.Any())
                throw new BusinessException("Cannot confirm ticket without items", "Phiếu không có sản phẩm nào. Vui lòng thêm sản phẩm trước khi xác nhận.");

            foreach (var item in stockTicket.StockTicketItems)
            {
                // Snapshot don gia nhap neu chua co (tuong thich nguoc voi phieu cu)
                var unitPrice = item.UnitPrice > 0 ? item.UnitPrice.Value : 0m;
                if (unitPrice == 0)
                {
                    var bpForPrice = await _context.BranchProducts.FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId);
                    unitPrice = bpForPrice?.CostPrice ?? 0;
                    item.UnitPrice = unitPrice;
                }

                // Atomic UPDATE: cong ton kho truc tiep trong DB, tranh xung dot
                try
                {
                    // CUSTOMER_RETURN: khong tinh lai cost_price de tranh lam loang gia von binh quan
                    if (stockTicket.TicketType == "CUSTOMER_RETURN")
                    {
                        await _context.Database.ExecuteSqlInterpolatedAsync($@"
                            UPDATE branch_products
                            SET actual_stock = actual_stock + {item.Discrepancy},
                                available_stock = available_stock + {item.Discrepancy},
                                is_active = CASE WHEN is_active = -1 THEN 1 ELSE is_active END
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
                                cost_price = CASE
                                    WHEN (actual_stock + {item.Discrepancy}) > 0
                                    THEN ROUND(((actual_stock * cost_price) + ({item.Discrepancy} * {unitPrice})) / (actual_stock + {item.Discrepancy}), 2)
                                    ELSE {unitPrice}
                                END
                            WHERE branch_product_id = {item.BranchProductId}
                        ");
                    }
                }
                catch (Exception ex) when (ex is DbUpdateConcurrencyException || ex is DbUpdateException)
                {
                    await transaction.RollbackAsync();
                    throw new BusinessException("Hệ thống đang xử lý một giao dịch kho khác cho sản phẩm này. Vui lòng thử lại sau giây lát.");
                }

                // Doc lai ton kho sau khi cap nhat de ghi nhan vao ticket item
                var updatedBp = await _context.BranchProducts.FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId);
                if (updatedBp != null)
                {
                    item.ActualQuantity = updatedBp.ActualStock;
                    item.SystemQuantity = updatedBp.ActualStock - item.Discrepancy;

                    if (stockTicket.SupplierId.HasValue)
                    {
                        var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == updatedBp.ProductId);
                        if (product != null && product.SupplierId == null)
                            product.SupplierId = stockTicket.SupplierId;
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

            // Chuyen trang thai sang COMPLETED
            stockTicket.Status = "COMPLETED";
            stockTicket.CompletedAt = DateTime.UtcNow;
            stockTicket.ConfirmedBy = currentUserId;
            if (stockTicket.TicketType == "PURCHASE" && stockTicket.TotalAmount == 0)
                stockTicket.TotalAmount = stockTicket.StockTicketItems.Sum(item => item.Discrepancy * (item.UnitPrice ?? 0));

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            if (currentUserId.HasValue)
            {
                await TryWriteSystemLog(
                    currentUserId.Value,
                    "CONFIRM_INWARD_INVENTORY",
                    "stock_tickets",
                    "InwardInventory",
                    stockTicket.StockTicketId,
                    $"Xác nhận phiếu nhập kho {stockTicket.TicketCode}");
            }

            return new ApiResponse<object>
            {
                Success = true,
                Message = "Xác nhận phiếu nhập kho thành công. Tồn kho đã được cập nhật.",
                Data = new { TicketId = stockTicket.StockTicketId, TicketCode = stockTicket.TicketCode, Status = stockTicket.Status }
            };
        }
        catch (BusinessException) { throw; }
        catch (UnauthorizedAccessException) { throw; }
        catch (KeyNotFoundException) { throw; }
        catch (Exception ex) when (ex is not DbUpdateConcurrencyException && ex is not DbUpdateException)
        {
            await transaction.RollbackAsync();
            throw new BusinessException("Lỗi hệ thống khi xác nhận phiếu nhập kho. Vui lòng thử lại sau.", ex.Message);
        }
    }

    public async Task<ApiResponse<object>> CancelInwardInventoryAsync(ClaimsPrincipal user, Guid id, CancelTicketDto dto, Guid? branchId)
    {
        var currentUserId = GetCurrentUserId(user);
        if (!currentUserId.HasValue)
            throw new UnauthorizedAccessException("Không thể xác định người dùng từ token.");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var stockTicketQuery = _context.StockTickets
                .Include(st => st.StockTicketItems).ThenInclude(item => item.BranchProduct)
                .Where(st => st.StockTicketId == id &&
                    (st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST"));

            if (!IsOwner(user))
            {
                if (branchId == null)
                    throw new UnauthorizedAccessException("JWT token is missing or invalid");
                stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
            }

            var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

            if (stockTicket == null)
                throw new KeyNotFoundException($"Không tìm thấy phiếu nhập kho với ID: {id}");

            // Kiem tra quyen huy
            bool hasDeletePerm = IsOwner(user) || user.HasClaim("permission", PermissionConstants.STOCK_INWARD_DELETE)
                || user.HasClaim("permission", PermissionConstants.STOCK_INWARD_UPDATE);
            bool isCreator = stockTicket.UserId == currentUserId.Value;
            bool isPending = stockTicket.Status?.ToUpperInvariant() == "PENDING";

            if (isPending && !isCreator && !hasDeletePerm)
                throw new BusinessException("Bạn không có quyền hủy phiếu này. Chỉ người tạo phiếu hoặc người có quyền hủy mới được thực hiện.");
            if (!isPending && !hasDeletePerm)
                throw new BusinessException("Bạn không có quyền hủy phiếu này. Chỉ Owner hoặc người có quyền hủy mới được thực hiện.");

            if (stockTicket.Status?.ToUpperInvariant() == "CANCELLED")
                throw new BusinessException("Ticket already cancelled", "Phiếu đã được hủy trước đó.");

            // ============================================================
            // QUY TAC MOI: Xu ly theo trang thai hien tai
            // ============================================================
            if (stockTicket.Status == "COMPLETED")
            {
                // COMPLETED: Can rollback ton kho (tru lai)
                // Nhung phai kiem tra an toan: khong cho phep ActualStock < 0
                var unsafeItems = new List<string>();

                foreach (var item in stockTicket.StockTicketItems)
                {
                    var branchProduct = await _context.BranchProducts.FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId);
                    if (branchProduct != null)
                    {
                        if (branchProduct.ActualStock < item.Discrepancy || branchProduct.AvailableStock < item.Discrepancy)
                            unsafeItems.Add(branchProduct.Product?.ProductName ?? item.BranchProductId.ToString());
                    }
                }

                if (unsafeItems.Any())
                {
                    await transaction.RollbackAsync();
                    throw new BusinessException("Không thể hủy vì hàng đã được xuất khỏi kho.", $"Sản phẩm bị ảnh hưởng: {string.Join(", ", unsafeItems)}");
                }

                // Du dieu kien - Thuc hien rollback
                foreach (var item in stockTicket.StockTicketItems)
                {
                    var branchProduct = await _context.BranchProducts.FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId);
                    if (branchProduct != null)
                    {
                        branchProduct.ActualStock -= item.Discrepancy;
                        branchProduct.AvailableStock -= item.Discrepancy;

                        // Ghi bút toán ĐẢO vào inventory_ledger: dòng nhập gốc ghi +discrepancy
                        // -> dòng hủy ghi -discrepancy (trả hàng ra khỏi kho do hủy phiếu).
                        // Phải cùng DbTransaction với rollback tồn kho (xem note InventoryLedgerService).
                        // Không có dòng này -> báo cáo xuất nhập tồn vẫn tính số nhập gốc (sai) &
                        // thẻ kho không thấy movement hủy.
                        var cancelUnitPrice = item.UnitPrice ?? 0m;
                        _context.InventoryLedgers.Add(new InventoryLedger
                        {
                            LedgerId = Guid.NewGuid(),
                            BranchId = stockTicket.BranchId,
                            BranchProductId = item.BranchProductId,
                            DocType = stockTicket.TicketType ?? "PURCHASE",
                            DocId = stockTicket.StockTicketId,
                            DocCode = stockTicket.TicketCode,
                            TransactionTime = DateTime.UtcNow,
                            QtyChange = -item.Discrepancy,
                            UnitCostPrice = Math.Round(cancelUnitPrice, 4),
                            TotalCostValue = Math.Round(-item.Discrepancy * cancelUnitPrice, 4),
                            StockAfter = branchProduct.ActualStock,
                            AvgCostAfter = Math.Round(branchProduct.CostPrice, 4),
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            // Luu trang thai TRUOC KHI doi de response message chinh xac
            var wasCompleted = stockTicket.Status == "COMPLETED";

            stockTicket.Status = "CANCELLED";
            stockTicket.CancelledAt = DateTime.UtcNow;
            stockTicket.CancelledBy = currentUserId;
            stockTicket.CancelReason = dto?.CancelReason;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await TryWriteSystemLog(
                currentUserId.Value,
                "CANCEL_INWARD_INVENTORY",
                "stock_tickets",
                "InwardInventory",
                stockTicket.StockTicketId,
                $"Hủy phiếu nhập kho {stockTicket.TicketCode} - Lý do: {dto?.CancelReason ?? "—"}");

            return new ApiResponse<object>
            {
                Success = true,
                Message = wasCompleted
                    ? "Inward inventory cancelled and stock rolled back successfully"
                    : "Inward inventory cancelled successfully (pending ticket, no stock change)",
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

    public async Task<string> GetPrintHtmlAsync(ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        if (branchId == null)
            throw new UnauthorizedAccessException("JWT token is missing or invalid");

        var ticket = await _context.StockTickets
            .Include(t => t.Branch)
            .Include(t => t.StockTicketItems).ThenInclude(i => i.BranchProduct).ThenInclude(bp => bp.Product)
            .Include(t => t.Supplier)
            .FirstOrDefaultAsync(t => t.StockTicketId == id && t.BranchId == branchId.Value);

        if (ticket == null)
            throw new KeyNotFoundException($"No inward inventory with ID: {id}");

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

        return html;
    }

    public async Task<ApiResponse<List<InwardTicketForReturnDto>>> GetBySupplierAsync(ClaimsPrincipal user, Guid supplierId, Guid? branchId)
    {
        if (!IsOwner(user) && !HasAnyPermission(user,
                PermissionConstants.STOCK_INWARD_CREATE, PermissionConstants.STOCK_INWARD_UPDATE, PermissionConstants.STOCK_OUTWARD_CREATE))
            throw new BusinessException("Bạn không có quyền lấy danh sách phiếu nhập.");

        var jwtBranchId = _currentBranch.GetBranchId();
        var targetBranchId = (IsOwner(user) ? branchId : null) ?? jwtBranchId;
        if (targetBranchId == null || targetBranchId == Guid.Empty)
            throw new BusinessException("Không xác định được chi nhánh.");

        var tickets = await _context.StockTickets
            .Where(st => st.SupplierId == supplierId && st.Status == "COMPLETED" && st.TicketType == "PURCHASE" && st.BranchId == targetBranchId.Value)
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

        return new ApiResponse<List<InwardTicketForReturnDto>> { Success = true, Message = "Lấy danh sách phiếu nhập thành công.", Data = tickets };
    }

    public async Task<ApiResponse<List<ReturnableItemDto>>> GetReturnableItemsAsync(ClaimsPrincipal user, Guid id)
    {
        if (!IsOwner(user) && !HasAnyPermission(user,
                PermissionConstants.STOCK_INWARD_CREATE, PermissionConstants.STOCK_INWARD_UPDATE, PermissionConstants.STOCK_OUTWARD_CREATE))
            throw new BusinessException("Bạn không có quyền lấy danh sách sản phẩm.");

        var inwardTicket = await _context.StockTickets
            .Include(st => st.StockTicketItems).ThenInclude(si => si.BranchProduct).ThenInclude(bp => bp.Product)
            .FirstOrDefaultAsync(st => st.StockTicketId == id && st.Status == "COMPLETED" && st.TicketType == "PURCHASE");

        if (inwardTicket == null)
            throw new KeyNotFoundException("Không tìm thấy phiếu nhập hoặc phiếu chưa hoàn thành.");

        var inwardItemIds = inwardTicket.StockTicketItems.Select(si => si.TicketItemId).ToList();

        var returnedQuantities = await _context.StockTicketItems
            .Where(si => si.InwardTicketItemId != null && inwardItemIds.Contains(si.InwardTicketItemId.Value) && si.StockTicket!.Status != "CANCELLED")
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
                UnitName = item.UnitName ?? item.BranchProduct?.Product?.BaseUnit,
                OriginalQuantity = item.Discrepancy,
                UnitPrice = item.UnitPrice ?? 0,
                MaxReturnableQuantity = maxReturnable > 0 ? maxReturnable : 0,
                ActualStock = item.BranchProduct?.ActualStock ?? 0
            };
        })
        .Where(i => i.MaxReturnableQuantity > 0)
        .ToList();

        return new ApiResponse<List<ReturnableItemDto>> { Success = true, Message = "Lấy danh sách sản phẩm có thể trả thành công.", Data = items };
    }

    // ===== Private helpers =====

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
                : stockTicket.StockTicketItems?.Sum(i => ((i.UnitPrice ?? 0) > 0 ? (i.UnitPrice ?? 0) : (i.BranchProduct?.CostPrice ?? 0)) * i.Discrepancy) ?? 0,
            Items = stockTicket.StockTicketItems?.Select(item =>
            {
                var effectivePrice = (item.UnitPrice ?? 0) > 0 ? (item.UnitPrice ?? 0) : (item.BranchProduct?.CostPrice ?? 0);
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
                    UnitPrice = effectivePrice,
                    CostPrice = effectivePrice,
                    UnitName = item.UnitName ?? item.BranchProduct?.Product?.BaseUnit
                };
            }).ToList() ?? new()
        };
    }
}