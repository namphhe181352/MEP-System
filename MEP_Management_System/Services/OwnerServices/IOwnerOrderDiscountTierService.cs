using MEP_Management_System.DTOs.SaleDTOs;

namespace MEP_Management_System.Services.OwnerServices;

public interface IOwnerOrderDiscountTierService
{
    Task<OrderDiscountTierDto> CreateAsync(Guid? branchId, CreateOrderDiscountTierDto request);
    Task<OrderDiscountTierDto> UpdateAsync(Guid id, Guid? branchId, UpdateOrderDiscountTierDto request);
    Task DeleteAsync(Guid id);
}
