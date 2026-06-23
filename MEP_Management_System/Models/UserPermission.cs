using System;

namespace MEP_Management_System.Models;

public partial class UserPermission
{
    public Guid UserPermissionId { get; set; }

    public Guid UserId { get; set; }

    public Guid PermissionId { get; set; }

    public virtual Permission Permission { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
