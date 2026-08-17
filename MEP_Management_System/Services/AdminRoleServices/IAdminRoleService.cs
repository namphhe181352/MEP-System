using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;

namespace MEP_Management_System.Services.AdminRoleServices;

/// <summary>
/// Service interface for Admin role management operations.
/// </summary>
public interface IAdminRoleService
{
    Task<List<AdminRoleDto>> GetRolesAsync();
    Task<List<AdminPermissionDto>> GetPermissionsAsync();
    Task<List<object>> GetRolePermissionMatrixAsync();
    Task<string> ConfigureRolePermissionsAsync(Guid id, AdminRolePermissionUpdateDto dto, Guid currentAdminId);
}
