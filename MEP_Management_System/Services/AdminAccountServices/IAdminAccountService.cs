using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;

namespace MEP_Management_System.Services.AdminAccountServices;

/// <summary>
/// Service interface for Admin account management operations.
/// </summary>
public interface IAdminAccountService
{
    Task<PageResultDto<AdminAccountDetailDto>> GetAccountsAsync(string? role, string? status, string? search, int page, int pageSize);
    Task<AdminAccountDetailDto> GetAccountByIdAsync(Guid id);
    Task<PageResultDto<SystemLogDto>> GetAccountActivitiesAsync(Guid id, int page, int pageSize);
    Task<Guid> CreateOwnerAsync(AdminCreateOwnerRequest dto, Guid currentAdminId);
    Task<Guid> CreateStaffAsync(AdminCreateStaffRequest dto, Guid currentAdminId);
    Task UpdateAccountAsync(Guid id, AdminUpdateAccountRequest dto, Guid currentAdminId);
    Task AssignRolesAsync(Guid id, AdminAssignRolesRequest dto, Guid currentAdminId);
    Task AssignPermissionsAsync(Guid id, AdminAssignPermissionsRequest dto, Guid currentAdminId);
    Task AssignBranchAsync(Guid id, AdminAssignBranchRequest dto, Guid currentAdminId);
    Task UpdateStatusAsync(Guid id, AdminToggleStatusRequest dto, Guid currentAdminId);
    Task ResetPasswordAsync(Guid id, AdminResetPasswordRequest dto, Guid currentAdminId);
    Task<AdminAccountRelationsDto> CheckRelationsAsync(Guid id);
    Task SoftDeleteAccountAsync(Guid id, Guid currentAdminId);
    Task RestoreAccountAsync(Guid id, Guid currentAdminId);
    Task PermanentDeleteAccountAsync(Guid id, Guid currentAdminId);
    Task<List<object>> GetDeletedAccountsAsync();
}
