using Microsoft.AspNetCore.Authorization;

namespace MEP_Management_System.Authorization;

/// <summary>
/// Requirement mô tả "cần PermissionCode = X mới được truy cập".
/// Được đọc bởi PermissionAuthorizationHandler.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionCode { get; }

    public PermissionRequirement(string permissionCode)
    {
        PermissionCode = permissionCode;
    }
}