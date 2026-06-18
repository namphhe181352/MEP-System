using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class StaffAssignment
{
    public Guid AssignmentId { get; set; }

    public Guid UserId { get; set; }

    public Guid BranchId { get; set; }

    public DateTime? AssignedAt { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
