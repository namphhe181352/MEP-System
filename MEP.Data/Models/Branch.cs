using System;
using System.Collections.Generic;

namespace MEP.Data.Models;

public partial class Branch
{
    public Guid BranchId { get; set; }

    public string BranchName { get; set; } = null!;

    public string? Address { get; set; }

    public virtual ICollection<BranchProduct> BranchProducts { get; set; } = new List<BranchProduct>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<StaffAssignment> StaffAssignments { get; set; } = new List<StaffAssignment>();

    public virtual ICollection<StockTicket> StockTickets { get; set; } = new List<StockTicket>();

    public virtual ICollection<SystemConfig> SystemConfigs { get; set; } = new List<SystemConfig>();
}
