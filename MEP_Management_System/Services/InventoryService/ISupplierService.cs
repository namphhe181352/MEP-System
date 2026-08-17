using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;

namespace MEP_Management_System.Services.InventoryService;

public interface ISupplierService
{
    Task<(List<SupplierListItemDto> Items, int TotalCount, List<string> DistinctGroups)> GetSuppliersAsync(
        Guid branchId, SupplierQueryParameters p);

    Task<SupplierDetailDto?> GetSupplierAsync(Guid id, Guid branchId);

    Task<SupplierDetailDto> CreateSupplierAsync(SupplierUpsertRequest req, Guid branchId, Guid? userId);

    Task<SupplierDetailDto?> UpdateSupplierAsync(Guid id, SupplierUpsertRequest req, Guid branchId, Guid? userId);

    Task<(bool Success, string Message)> DeleteSupplierAsync(Guid id, Guid branchId);

    Task<(bool Success, string Message, short? NewStatus)> ToggleStatusAsync(Guid id, string status, Guid branchId, Guid? userId);
}
