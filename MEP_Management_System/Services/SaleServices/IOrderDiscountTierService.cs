using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;

namespace MEP_Management_System.Services.SaleServices;

public interface IOrderDiscountTierService
{
    Task<List<OrderDiscountTierDto>> GetAllAsync(Guid? branchId);
    Task<OrderDiscountTierDto?> GetByIdAsync(Guid id);
    Task<OrderDiscountTierDto> CreateAsync(CreateOrderDiscountTierRequest req, Guid? branchId);
    Task<OrderDiscountTierDto?> UpdateAsync(Guid id, CreateOrderDiscountTierRequest req, Guid? branchId);
    Task<bool> DeleteAsync(Guid id);
}

public class CreateOrderDiscountTierRequest
{
    public Guid? BranchId { get; set; }
    public decimal MinOrderValue { get; set; }
    public decimal DiscountPercent { get; set; }
    public bool IsActive { get; set; } = true;
}
