using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class SystemConfig
{
    public Guid ConfigId { get; set; }

    public Guid? BranchId { get; set; }

    public string ConfigKey { get; set; } = null!;

    public string ConfigValue { get; set; } = null!;

    public virtual Branch? Branch { get; set; }
}
