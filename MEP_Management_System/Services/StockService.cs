using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

public class StockService : IStockService
{
    private readonly MEPDatabaseContext _context;
    private readonly InventoryLedgerService _ledgerService;

    public StockService(MEPDatabaseContext context, InventoryLedgerService ledgerService)
    {
        _context = context;
        _ledgerService = ledgerService;
    }

    private bool IsOwner(ClaimsPrincipal user) => user.IsInRole(PermissionConstants.OWNER_ROLE);

    /// <summary>
    /// Trả về branch cần dùng: Staff bắt buộc có branch; Owner có thể bỏ qua (null = tất cả).
    /// Nếu thiếu, ném UnauthorizedAccessException (khớp pattern InventoryCheck).
    /// </summary>
    private Guid? ResolveBranchOrThrow(ClaimsPrincipal user, Guid? branchId)
    {
        if (!IsOwner(user))
        {
            if (!branchId.HasValue)
                throw new UnauthorizedAccessException("Không xác định được chi nhánh hiện tại của người dùng.");
        }
        return branchId;
    }

    private static string ToStockStatus(decimal actualStock, decimal minStock)
    {
        if (actualStock <= 0) return "OUT_OF_STOCK";
        if (minStock > 0 && actualStock < minStock) return "LOW_STOCK";
        return "IN_STOCK";
    }

    private static StockResponseDto MapToResponse(BranchProduct bp)
    {
        var minStock = bp.MinimumStock ?? 0m;
        return new StockResponseDto
        {
            BranchProductId = bp.BranchProductId,
            BranchId = bp.BranchId,
            BranchName = bp.Branch?.BranchName,
            ProductId = bp.ProductId,
            ProductCode = bp.Product?.ProductCode ?? "",
            ProductName = bp.Product?.ProductName ?? "",
            Barcode = bp.Product?.Barcode,
            Unit = bp.Product?.BaseUnit,
            Stock = bp.ActualStock,
            ActualStock = bp.ActualStock,
            AvailableStock = bp.AvailableStock,
            ReservedStock = bp.ReservedStock,
            WarrantyQuantity = bp.WarrantyQuantity,
            MinimumStock = minStock,
            MaximumStock = bp.MaximumStock ?? 0m,
            CostPrice = bp.CostPrice,
            SellPrice = bp.SellPrice,
            StockStatus = ToStockStatus(bp.ActualStock, minStock),
            ShelfLocation = bp.ShelfLocation
        };
    }

    private static StockResponseDto MapAggregated(List<BranchProduct> bps)
    {
        var first = bps.First();
        var minStock = bps.Sum(b => b.MinimumStock ?? 0m);
        return new StockResponseDto
        {
            BranchProductId = null,
            BranchId = null,
            BranchName = null,
            ProductId = first.ProductId,
            ProductCode = first.Product?.ProductCode ?? "",
            ProductName = first.Product?.ProductName ?? "",
            Barcode = first.Product?.Barcode,
            Unit = first.Product?.BaseUnit,
            Stock = bps.Sum(b => b.ActualStock),
            ActualStock = bps.Sum(b => b.ActualStock),
            AvailableStock = bps.Sum(b => b.AvailableStock),
            ReservedStock = bps.Sum(b => b.ReservedStock),
            WarrantyQuantity = bps.Sum(b => b.WarrantyQuantity),
            MinimumStock = minStock,
            MaximumStock = bps.Sum(b => b.MaximumStock ?? 0m),
            CostPrice = first.CostPrice,
            SellPrice = first.SellPrice,
            StockStatus = ToStockStatus(bps.Sum(b => b.ActualStock), minStock),
            ShelfLocation = first.ShelfLocation
        };
    }

    public async Task<ApiResponse<PaginatedResponse<StockResponseDto>>> GetStocksAsync(
        ClaimsPrincipal user, Guid? branchId, string? search, bool? lowStockOnly, int pageNumber, int pageSize)
    {
        var effectiveBranchId = ResolveBranchOrThrow(user, branchId);
        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize < 1 ? 10 : pageSize;

        IQueryable<BranchProduct> query = _context.BranchProducts
            .AsNoTracking()
            .Include(bp => bp.Product)
            .Include(bp => bp.Branch);

        if (effectiveBranchId.HasValue)
            query = query.Where(bp => bp.BranchId == effectiveBranchId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(bp =>
                EF.Functions.ILike(bp.Product.ProductName, term)
                || EF.Functions.ILike(bp.Product.ProductCode, term)
                || (bp.Product.Barcode != null && EF.Functions.ILike(bp.Product.Barcode, term)));
        }

        if (lowStockOnly == true)
            query = query.Where(bp => bp.ActualStock <= (bp.MinimumStock ?? 0m));

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderBy(bp => bp.Product != null ? bp.Product.ProductName : "")
            .ThenBy(bp => bp.ProductId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ApiResponse<PaginatedResponse<StockResponseDto>>
        {
            Success = true,
            Message = "Lấy danh sách tồn kho thành công.",
            Data = new PaginatedResponse<StockResponseDto>
            {
                Items = items.Select(MapToResponse).ToList(),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages,
                HasNextPage = pageNumber < totalPages,
                HasPreviousPage = pageNumber > 1
            }
        };
    }

    public async Task<ApiResponse<StockResponseDto>> GetStockByProductAsync(ClaimsPrincipal user, Guid productId, Guid? branchId)
    {
        var effectiveBranchId = ResolveBranchOrThrow(user, branchId);

        IQueryable<BranchProduct> query = _context.BranchProducts
            .AsNoTracking()
            .Include(bp => bp.Product)
            .Include(bp => bp.Branch)
            .Where(bp => bp.ProductId == productId);

        if (effectiveBranchId.HasValue)
            query = query.Where(bp => bp.BranchId == effectiveBranchId.Value);

        var bps = await query.ToListAsync();
        if (bps.Count == 0)
            throw new BusinessException("Không tìm thấy thông tin tồn kho cho sản phẩm này.");

        var dto = bps.Count == 1 ? MapToResponse(bps[0]) : MapAggregated(bps);

        return new ApiResponse<StockResponseDto>
        {
            Success = true,
            Message = "Lấy thông tin tồn kho thành công.",
            Data = dto
        };
    }

    public async Task<ApiResponse<StockResponseDto>> UpdateStockAsync(ClaimsPrincipal user, Guid productId, UpdateStockDto dto, Guid? branchId)
    {
        var effectiveBranchId = ResolveBranchOrThrow(user, branchId);
        if (!effectiveBranchId.HasValue)
            throw new BusinessException("Cần xác định chi nhánh để điều chỉnh tồn kho. Vui lòng truyền branchId hoặc header X-Branch-Id.");

        var bp = await _context.BranchProducts
            .Include(x => x.Product)
            .Include(x => x.Branch)
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.BranchId == effectiveBranchId.Value);

        if (bp == null)
            throw new BusinessException("Không tìm thấy sản phẩm trong kho của chi nhánh hiện tại.");

        if (dto.NewStock < 0)
            throw new BusinessException("Tồn kho mới không được âm.");

        var oldActual = bp.ActualStock;
        var newActual = dto.NewStock;
        var delta = Math.Round(newActual - oldActual, 2);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            bp.ActualStock = newActual;
            // Cân chỉnh available theo cùng mức chênh lệch, không để âm.
            bp.AvailableStock = Math.Max(0, bp.AvailableStock + delta);

            // Ghi inventory_ledger cùng transaction (InventoryLedgerService tự SaveChanges).
            await _ledgerService.WriteLedgerEntry(
                branchId: bp.BranchId,
                branchProductId: bp.BranchProductId,
                docType: "BALANCE_ADJUST",
                docId: Guid.NewGuid(),
                docCode: dto.Reason,
                transactionTime: DateTime.UtcNow,
                qtyChange: delta,
                unitCostPrice: bp.CostPrice,
                stockAfter: newActual,
                avgCostAfter: bp.CostPrice);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        return new ApiResponse<StockResponseDto>
        {
            Success = true,
            Message = "Điều chỉnh tồn kho thành công.",
            Data = MapToResponse(bp)
        };
    }
}