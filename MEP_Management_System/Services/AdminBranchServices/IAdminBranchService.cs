using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;

namespace MEP_Management_System.Services.AdminBranchServices;

/// <summary>
/// Service interface for Admin branch management operations.
/// </summary>
public interface IAdminBranchService
{
    Task<PageResultDto<AdminBranchDto>> GetBranchesAsync(string? search, short? isActive, int page, int pageSize);
    Task<AdminBranchDto> GetBranchByIdAsync(Guid id);
    Task<AdminBranchDto> CreateBranchAsync(CreateAdminBranchRequest dto, Guid currentAdminId);
    Task<AdminBranchDto> UpdateBranchAsync(Guid id, UpdateAdminBranchRequest dto, Guid currentAdminId);
    Task<(string message, short isActive)> DeleteBranchAsync(Guid id, Guid currentAdminId, bool force = false);
    Task<(string message, short isActive)> RestoreBranchAsync(Guid id, Guid currentAdminId);
    Task<string> HardDeleteBranchAsync(Guid id, Guid currentAdminId, bool force = false);
    Task<List<BranchUserDto>> GetBranchUsersAsync(Guid id);
    Task AssignUserToBranchAsync(Guid id, AssignUserToBranchRequest dto, Guid currentAdminId);
    Task RemoveUserFromBranchAsync(Guid id, Guid userId, Guid currentAdminId);
}
