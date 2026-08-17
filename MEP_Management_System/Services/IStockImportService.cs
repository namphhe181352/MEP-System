using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;

namespace MEP_Management_System.Services
{
    /// <summary>
    /// Dịch vụ phiếu nhập kho legacy (StockImport). Delegate sang InwardInventory:
    /// tạo phiếu PENDING không ghi ledger; confirm mới ghi ledger + cộng tồn kho.
    /// </summary>
    public interface IStockImportService
    {
        Task<ApiResponse<PaginatedResponse<StockImportResponseDto>>> GetStockImportsAsync(
            ClaimsPrincipal user, Guid? branchId, string? status, string? ticketType,
            DateTime? fromDate, DateTime? toDate, int pageNumber, int pageSize);

        Task<ApiResponse<StockImportResponseDto>> GetStockImportByIdAsync(ClaimsPrincipal user, Guid id, Guid? branchId);

        Task<ApiResponse<object>> CreateStockImportAsync(ClaimsPrincipal user, StockImportCreateDto dto, Guid? branchId);
    }
}