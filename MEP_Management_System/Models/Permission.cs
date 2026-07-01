using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class Permission
{
    public Guid PermissionId { get; set; }

    public string PermissionCode { get; set; } = null!;

    // Alias for backward compatibility with controller code
    public string Code => PermissionCode;

    public string PermissionName { get; set; } = null!;

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

    public virtual ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
}
