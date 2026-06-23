using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class Branch
{
    public Guid BranchId { get; set; }

    public string BranchName { get; set; } = null!;

    public string? BranchCode { get; set; }

    public string? Phone { get; set; }

    public string? City { get; set; }

    public string? Address { get; set; }

    public string? Type { get; set; }

    public Guid? ManagerUserId { get; set; }

    public short IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User? ManagerUser { get; set; }

    public virtual ICollection<BranchProduct> BranchProducts { get; set; } = new List<BranchProduct>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<StaffAssignment> StaffAssignments { get; set; } = new List<StaffAssignment>();

    public virtual ICollection<StockTicket> StockTickets { get; set; } = new List<StockTicket>();

    public virtual ICollection<SystemConfig> SystemConfigs { get; set; } = new List<SystemConfig>();
    public virtual ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public virtual ICollection<DailySummary> DailySummaries { get; set; } = new List<DailySummary>();
}
