using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;

namespace MEP_Management_System.Services;

public interface IStockService
{
    /// <summary>GET /api/Stock — danh sách tồn kho phân trang, hỗ trợ lọc.</summary>
    Task<ApiResponse<PaginatedResponse<StockResponseDto>>> GetStocksAsync(
        ClaimsPrincipal user,
        Guid? branchId,
        string? search,
        bool? lowStockOnly,
        int pageNumber,
        int pageSize);

    /// <summary>GET /api/Stock/product/{productId} — tồn kho 1 sản phẩm.</summary>
    Task<ApiResponse<StockResponseDto>> GetStockByProductAsync(ClaimsPrincipal user, Guid productId, Guid? branchId);

    /// <summary>PUT /api/Stock/{productId} — điều chỉnh tồn kho + ghi inventory_ledger.</summary>
    Task<ApiResponse<StockResponseDto>> UpdateStockAsync(ClaimsPrincipal user, Guid productId, UpdateStockDto dto, Guid? branchId);
}