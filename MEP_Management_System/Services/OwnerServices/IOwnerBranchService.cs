using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;

namespace MEP_Management_System.Services.OwnerServices;

public interface IOwnerBranchService
{
    Task<List<OwnerBranchDto>> GetBranchesAsync(Guid ownerUserId);
    Task<OwnerBranchDto> GetBranchByIdAsync(Guid ownerUserId, Guid branchId);
    Task<OwnerBranchDto> CreateBranchAsync(Guid ownerUserId, CreateOwnerBranchRequest request);
    Task<OwnerBranchDto> UpdateBranchAsync(Guid ownerUserId, Guid branchId, UpdateOwnerBranchRequest request);
    Task<PaginatedResponse<OwnerBranchHistoryDto>> GetBranchHistoryAsync(Guid ownerUserId, Guid branchId, int page, int pageSize, string? type, string? status);
    Task<BranchSettingsDto> GetBranchSettingsAsync(Guid ownerUserId, Guid branchId);
    Task<BranchSettingsDto> UpdateBranchSettingsAsync(Guid ownerUserId, Guid branchId, UpdateBranchSettingsRequest request);
    Task<List<CategoryPolicyDto>> GetCategoryPoliciesAsync(Guid ownerUserId, Guid branchId);
    Task UpdateCategoryPoliciesAsync(Guid ownerUserId, Guid branchId, UpdateCategoryPoliciesRequest request);
    Task<object> GetReturnDiscountAsync(Guid ownerUserId, Guid branchId);
    Task<object> UpdateReturnDiscountAsync(Guid ownerUserId, Guid branchId, UpdateReturnDiscountRequest request);
}
