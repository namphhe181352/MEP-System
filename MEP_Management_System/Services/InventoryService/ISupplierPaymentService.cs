using MEP_Management_System.DTOs.InventoryDTOs;

namespace MEP_Management_System.Services.InventoryService;

public interface ISupplierPaymentService
{
    Task<(List<SupplierPaymentResponseDto> Items, int TotalCount)> GetPaymentsAsync(
        Guid? branchId, Guid? supplierId, int pageNumber, int pageSize);

    Task<Guid> CreatePaymentAsync(CreateSupplierPaymentDto dto, Guid currentUserId);

    Task<bool> UpdatePaymentAsync(Guid id, UpdateSupplierPaymentDto dto);

    Task<bool> DeletePaymentAsync(Guid id);
}
