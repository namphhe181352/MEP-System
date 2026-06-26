using Microsoft.AspNetCore.Authorization;

namespace MEP_Management_System.Authorization;

/// <summary>
/// Attribute dùng để gắn lên Controller / Action.
/// Tự sinh Policy tương ứng với PermissionCode, áp dụng Bearer scheme.
/// Cú pháp: [HasPermission(PermissionConstants.SUPPLIER_VIEW)]
/// </summary>
public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permissionCode)
        : base(policy: PermissionPolicyFactory.Create(permissionCode))
    {
    }
}