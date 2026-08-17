using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using OutwardInventoryType = MEP_Management_System.DTOs.InventoryDTOs.OutwardInventoryType;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.InventoryServices;

public class OutwardInventoryServiceImpl : IOutwardInventoryService
{
    private readonly MEPDatabaseContext _context;
    private readonly ICurrentBranchAccessor _currentBranch;

    public OutwardInventoryServiceImpl(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch)
    {
        _context = context;
        _currentBranch = currentBranch;
    }

    private Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst("userId")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
    }

    private bool IsOwner(ClaimsPrincipal user) => user.IsInRole("Owner");

    private static DateTime UtcNow() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    public async Task<ApiResponse<PaginatedResponse<OutwardInventoryResponseDto>>> GetOutwardInventoriesAsync(
        ClaimsPrincipal user, Guid? branchId, string? status, string? ticketType,
        DateTime? fromDate, DateTime? toDate, int pageNumber, int pageSize)
    {
        var list = _context.StockTickets
            .AsNoTracking()
            .Include(st => st.User)
            .Include(st => st.Supplier)
            .Where(st => st.TicketType == "SALE" || st.TicketType == "SUPPLIER_RETURN");

        if (!IsOwner(user))
        {
            if (!branchId.HasValue)
                throw new UnauthorizedAccessException("JWT token is missing or invalid");
            list = list.Where(st => st.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
            list = list.Where(st => st.Status == status);

        if (!string.IsNullOrWhiteSpace(ticketType))
            list = list.Where(st => st.TicketType == ticketType);

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

        return new ApiResponse<PaginatedResponse<OutwardInventoryResponseDto>>
        {
            Success = true,
            Message = "Outward inventory history retrieved successfully",
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
        };
    }

    public async Task<ApiResponse<OutwardInventoryResponseDto>> GetOutwardInventoryByIdAsync(ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        var stockTicketQuery = _context.StockTickets
            .AsNoTracking()
            .Include(st => st.User)
            .Include(st => st.Supplier)
            .Include(st => st.StockTicketItems).ThenInclude(item => item.BranchProduct).ThenInclude(bp => bp.Product)
            .Where(st => st.StockTicketId == id && (st.TicketType == "SALE" || st.TicketType == "SUPPLIER_RETURN"));

        if (!IsOwner(user))
        {
            if (branchId == null)
                throw new UnauthorizedAccessException("JWT token is missing or invalid");
            stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
        }

        var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

        if (stockTicket == null)
            throw new KeyNotFoundException($"Khong tim thay phieu xuat kho voi ID: {id}");

        return new ApiResponse<OutwardInventoryResponseDto>
        {
            Success = true,
            Message = "Outward inventory retrieved successfully",
            Data = MapToOutwardInventoryResponse(stockTicket)
        };
    }

    public async Task<ApiResponse<object>> CreateOutwardInventoryAsync(ClaimsPrincipal user, OutwardInventoryCreateDto dto, Guid? branchIdOverride)
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

        if (dto.OutwardType == OutwardInventoryType.SupplierReturn && (!dto.SupplierId.HasValue || dto.SupplierId == Guid.Empty))
            throw new BusinessException("Validation failed", "Nha cung cap la bat buoc doi voi phieu tra hang.");

        var itemValidationErrors = new List<string>();
        for (int i = 0; i < dto.Items.Count; i++)
        {
            var item = dto.Items[i];
            if (item.Quantity <= 0)
                itemValidationErrors.Add($"Dong {i + 1}: So luong phai lon hon 0.");
        }

        if (itemValidationErrors.Any())
            throw new BusinessException("Validation failed", itemValidationErrors);

        var currentTime = UtcNow();
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
                SupplierId = dto.OutwardType == OutwardInventoryType.SupplierReturn ? dto.SupplierId : null,
                TicketType = GetTicketTypeName(dto.OutwardType),
                Status = "PENDING",
                Notes = dto.Note ?? dto.Reason,
                CreatedAt = currentTime
            };

            _context.StockTickets.Add(stockTicket);

            foreach (var item in dto.Items)
            {
                Guid parsedId = item.BranchProductId;
                bool isGuid = parsedId != Guid.Empty;

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
                    ActualQuantity = branchProduct.ActualStock,
                    Discrepancy = -item.Quantity,
                    UnitPrice = item.UnitPrice ?? branchProduct.CostPrice,
                    UnitName = branchProduct.Product?.BaseUnit ?? "",
                    WarrantyPeriod = branchProduct.Product?.WarrantyPeriod ?? 0,
                    WarrantyUnit = branchProduct.Product?.WarrantyUnit ?? "MONTH"
                };

                _context.StockTicketItems.Add(ticketItem);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ApiResponse<object>
            {
                Success = true,
                Message = "Outward inventory created successfully. Please confirm to update stock.",
                Data = new { TicketId = stockTicket.StockTicketId, TicketCode = stockTicket.TicketCode, TicketType = stockTicket.TicketType, BranchId = stockTicket.BranchId, Status = stockTicket.Status }
            };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse<OutwardInventoryResponseDto>> UpdateOutwardInventoryAsync(ClaimsPrincipal user, Guid id, OutwardInventoryUpdateDto dto, Guid? branchId)
    {
        var currentUserId = GetCurrentUserId(user);
        var stockTicketQuery = _context.StockTickets
            .Include(st => st.StockTicketItems)
            .Where(st => st.StockTicketId == id && (st.TicketType == "SALE" || st.TicketType == "SUPPLIER_RETURN"));

        if (!IsOwner(user))
        {
            if (branchId == null)
                throw new UnauthorizedAccessException("JWT token is missing or invalid");
            stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
        }

        var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

        if (stockTicket == null)
            throw new KeyNotFoundException($"Khong tim thay phieu xuat kho voi ID: {id}");

        if (stockTicket.Status == "CANCELLED")
            throw new BusinessException("Cannot update cancelled ticket", "Phieu da bi huy. Khong the chinh sua.");

        if (stockTicket.Status == "COMPLETED")
        {
            if (dto.Note != null) stockTicket.Notes = dto.Note;
            if (dto.Reason != null) stockTicket.Reason = dto.Reason;
            await _context.SaveChangesAsync();
            return new ApiResponse<OutwardInventoryResponseDto> { Success = true, Message = "Outward inventory updated (notes only for completed tickets)", Data = MapToOutwardInventoryResponse(stockTicket) };
        }

        if (dto.Note != null) stockTicket.Notes = dto.Note;
        if (dto.Reason != null) stockTicket.Reason = dto.Reason;

        if (!string.IsNullOrWhiteSpace(dto.Status))
        {
            var normalized = dto.Status.Trim().ToUpperInvariant();
            if (normalized is "PENDING" or "CANCELLED")
                stockTicket.Status = normalized;
        }

        await _context.SaveChangesAsync();
        return new ApiResponse<OutwardInventoryResponseDto> { Success = true, Message = "Outward inventory updated successfully", Data = MapToOutwardInventoryResponse(stockTicket) };
    }

    public async Task<ApiResponse<object>> ConfirmOutwardInventoryAsync(ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        var currentUserId = GetCurrentUserId(user);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var stockTicketQuery = _context.StockTickets
                .Include(st => st.StockTicketItems).ThenInclude(item => item.BranchProduct)
                .Where(st => st.StockTicketId == id && (st.TicketType == "SALE" || st.TicketType == "SUPPLIER_RETURN"));

            if (!IsOwner(user))
            {
                if (branchId == null)
                    throw new UnauthorizedAccessException("JWT token is missing or invalid");
                stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
            }

            var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

            if (stockTicket == null)
                throw new KeyNotFoundException($"Khong tim thay phieu xuat kho voi ID: {id}");

            if (stockTicket.Status == "COMPLETED")
                throw new BusinessException("Ticket already confirmed", "Phieu da duoc xac nhan truoc do.");

            if (stockTicket.Status == "CANCELLED")
                throw new BusinessException("Cannot confirm cancelled ticket", "Phieu da bi huy. Khong the xac nhan.");

            if (stockTicket.StockTicketItems == null || !stockTicket.StockTicketItems.Any())
                throw new BusinessException("Cannot confirm ticket without items", "Phieu khong co san pham nao.");

            foreach (var item in stockTicket.StockTicketItems)
            {
                var qty = item.Discrepancy < 0 ? Math.Abs(item.Discrepancy) : 0;

                try
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync($@"
                        UPDATE branch_products
                        SET actual_stock = actual_stock - {qty},
                            available_stock = available_stock - {qty}
                        WHERE branch_product_id = {item.BranchProductId}
                          AND actual_stock >= {qty}");
                }
                catch (Exception ex) when (ex is DbUpdateConcurrencyException || ex is DbUpdateException)
                {
                    await transaction.RollbackAsync();
                    throw new BusinessException("He thong dang xu ly mot giao dich kho khac cho san pham nay.", "Vui long thu lai sau giay lat.");
                }

                var updatedBp = await _context.BranchProducts.FirstOrDefaultAsync(bp => bp.BranchProductId == item.BranchProductId);
                if (updatedBp != null)
                {
                    item.ActualQuantity = updatedBp.ActualStock;
                    item.SystemQuantity = updatedBp.ActualStock + qty;
                }

                var ledger = new InventoryLedger
                {
                    LedgerId = Guid.NewGuid(),
                    BranchId = stockTicket.BranchId,
                    BranchProductId = item.BranchProductId,
                    DocType = stockTicket.TicketType ?? "SALE",
                    DocId = stockTicket.StockTicketId,
                    DocCode = stockTicket.TicketCode,
                    TransactionTime = DateTime.UtcNow,
                    QtyChange = -qty,
                    UnitCostPrice = Math.Round(item.UnitPrice ?? 0, 4),
                    TotalCostValue = Math.Round(qty * (item.UnitPrice ?? 0), 4),
                    StockAfter = updatedBp?.ActualStock ?? 0,
                    AvgCostAfter = Math.Round(updatedBp?.CostPrice ?? (item.UnitPrice ?? 0), 4),
                    CreatedAt = DateTime.UtcNow
                };
                _context.InventoryLedgers.Add(ledger);
            }

            stockTicket.Status = "COMPLETED";
            stockTicket.CompletedAt = DateTime.UtcNow;
            stockTicket.ConfirmedBy = currentUserId;
            if (stockTicket.TotalAmount == 0)
                stockTicket.TotalAmount = stockTicket.StockTicketItems.Sum(item => item.Discrepancy * (item.UnitPrice ?? 0));

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ApiResponse<object>
            {
                Success = true,
                Message = "Xac nhan phieu xuat kho thanh cong. Ton kho da duoc cap nhat.",
                Data = new { TicketId = stockTicket.StockTicketId, TicketCode = stockTicket.TicketCode, Status = stockTicket.Status }
            };
        }
        catch (BusinessException) { throw; }
        catch (UnauthorizedAccessException) { throw; }
        catch (KeyNotFoundException) { throw; }
        catch (Exception ex) when (ex is not DbUpdateConcurrencyException && ex is not DbUpdateException)
        {
            await transaction.RollbackAsync();
            throw new BusinessException("Loi he thong khi xac nhan phieu xuat kho. Vui long thu lai sau.", ex.Message);
        }
    }

    public async Task<ApiResponse<object>> CancelOutwardInventoryAsync(ClaimsPrincipal user, Guid id, CancelTicketDto dto, Guid? branchId)
    {
        var currentUserId = GetCurrentUserId(user);
        if (!currentUserId.HasValue)
            throw new UnauthorizedAccessException("Khong the xac dinh nguoi dung tu token.");

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var stockTicketQuery = _context.StockTickets
                .Include(st => st.StockTicketItems).ThenInclude(item => item.BranchProduct)
                .Where(st => st.StockTicketId == id && (st.TicketType == "SALE" || st.TicketType == "SUPPLIER_RETURN"));

            if (!IsOwner(user))
            {
                if (branchId == null)
                    throw new UnauthorizedAccessException("JWT token is missing or invalid");
                stockTicketQuery = stockTicketQuery.Where(st => st.BranchId == branchId.Value);
            }

            var stockTicket = await stockTicketQuery.FirstOrDefaultAsync();

            if (stockTicket == null)
                throw new KeyNotFoundException($"Khong tim thay phieu xuat kho voi ID: {id}");

            if (stockTicket.Status?.ToUpperInvariant() == "CANCELLED")
                throw new BusinessException("Ticket already cancelled", "Phieu da duoc huy truoc do.");

            var wasCompleted = stockTicket.Status == "COMPLETED";
            stockTicket.Status = "CANCELLED";
            stockTicket.CancelledAt = DateTime.UtcNow;
            stockTicket.CancelledBy = currentUserId;
            stockTicket.CancelReason = dto?.CancelReason;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ApiResponse<object>
            {
                Success = true,
                Message = wasCompleted
                    ? "Outward inventory cancelled and stock rolled back successfully"
                    : "Outward inventory cancelled successfully (pending ticket, no stock change)",
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
        var ticket = await _context.StockTickets
            .Include(t => t.Branch)
            .Include(t => t.StockTicketItems).ThenInclude(i => i.BranchProduct).ThenInclude(bp => bp.Product)
            .Include(t => t.Supplier)
            .FirstOrDefaultAsync(t => t.StockTicketId == id && t.BranchId == branchId.Value);

        if (ticket == null)
            throw new KeyNotFoundException($"No outward inventory with ID: {id}");

        var items = ticket.StockTicketItems?.Select(i => new
        {
            i.BranchProduct?.Product?.ProductCode,
            i.BranchProduct?.Product?.ProductName,
            Unit = i.BranchProduct?.Product?.BaseUnit,
            Quantity = Math.Abs(i.Discrepancy),
            i.BranchProduct?.SellPrice
        }).ToList() ?? new();

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
<td>{item.SellPrice:N0}</td>
</tr>";
        }

        html += $@"</table>
<p><strong>Ghi chu:</strong> {ticket.Notes ?? ""}</p>
<p style=""margin-top:40px"">Ky ten: __________________</p>
</body>
</html>";

        return html;
    }

    // ===== Private helpers =====

    private static string GenerateTicketCode(OutwardInventoryType type, DateTime currentTime)
    {
        var typePrefix = type switch
        {
            OutwardInventoryType.Sale => "SAL-",
            OutwardInventoryType.SupplierReturn => "SRT-",
            _ => "SAL-"
        };
        return $"{typePrefix}{currentTime:yyyyMMddHHmmss}";
    }

    private static string GetTicketTypeName(OutwardInventoryType type)
    {
        return type switch
        {
            OutwardInventoryType.Sale => "SALE",
            OutwardInventoryType.SupplierReturn => "SUPPLIER_RETURN",
            _ => "SALE"
        };
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
            SupplierId = stockTicket.SupplierId,
            UserName = stockTicket.User?.FullName ?? stockTicket.User?.Username,
            SupplierName = stockTicket.Supplier?.SupplierName,
            Reason = stockTicket.Reason,
            Note = stockTicket.Notes,
            CreatedAt = stockTicket.CreatedAt,
            CompletedAt = stockTicket.CompletedAt,
            CancelledAt = stockTicket.CancelledAt,
            ConfirmedBy = stockTicket.ConfirmedBy,
            CancelledBy = stockTicket.CancelledBy,
            CancelReason = stockTicket.CancelReason,
            TotalAmount = stockTicket.TotalAmount,
            Items = stockTicket.StockTicketItems?.Select(item =>
            {
                var effectivePrice = (item.UnitPrice ?? 0) > 0 ? (item.UnitPrice ?? 0) : (item.BranchProduct?.CostPrice ?? 0);
                return new OutwardInventoryItemResponseDto
                {
                    TicketItemId = item.TicketItemId,
                    BranchProductId = item.BranchProductId,
                    ProductId = item.BranchProduct?.ProductId ?? Guid.Empty,
                    ProductCode = item.BranchProduct?.Product?.ProductCode ?? string.Empty,
                    ProductName = item.BranchProduct?.Product?.ProductName ?? string.Empty,
                    SystemQuantity = item.SystemQuantity,
                    Quantity = Math.Abs(item.Discrepancy),
                    ActualQuantity = item.ActualQuantity,
                    CostPrice = effectivePrice,
                    UnitPrice = effectivePrice,
                    UnitName = item.UnitName ?? item.BranchProduct?.Product?.BaseUnit
                };
            }).ToList() ?? new()
        };
    }
}
