using System;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;

namespace MEP_Management_System.Services.AdminUserServices;

/// <summary>
/// Service interface for Admin user management operations.
/// </summary>
public interface IAdminUserService
{
    Task<PageResultDto<AdminUserDto>> GetUsersAsync(string? search, string? roleId, int page, int pageSize);
    Task<AdminUserDetailDto> GetUserByIdAsync(Guid id);
    Task<IdResponseDto> CreateUserAsync(AdminUserCreateDto dto, Guid? currentAdminId);
    Task UpdateUserAsync(Guid id, AdminUserUpdateDto dto, Guid? currentAdminId);
    Task AssignUserRoleAsync(Guid id, AdminUserRoleUpdateDto dto, Guid? currentAdminId);
    Task UpdateUserStatusAsync(Guid id, AdminUserStatusUpdateDto dto, Guid? currentAdminId);
    Task ResetUserPasswordAsync(Guid id, AdminUserResetPasswordDto dto, Guid? currentAdminId);
    Task<PageResultDto<SystemLogDto>> GetUserActivitiesAsync(Guid id, int page, int pageSize);
}
