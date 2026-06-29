using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class Customer
{
    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = null!;

    /// <summary>Số điện thoại KH (UNIQUE theo SRS BR-82).</summary>
    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public int? LoyaltyPoints { get; set; }

    /// <summary>Hạn mức nợ tối đa (theo UC Configure Customer credit limit).</summary>
    public decimal? DebtLimit { get; set; }

    /// <summary>Dư nợ hiện tại (AP/AR tracking).</summary>
    public decimal? CurrentDebt { get; set; }

    /// <summary>Trạng thái: 1=Active, 0=Blocked. Mặc định 1.</summary>
    public short? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    /// <summary>Individual | Enterprise | Agent | Contractor</summary>
    public string? Group { get; set; }

    public string? Notes { get; set; }

    /// <summary>Derived but cached for performance</summary>
    public decimal? TotalSpent { get; set; }

    /// <summary>Derived but cached for performance</summary>
    public int? OrderCount { get; set; }

    /// <summary>Derived but cached for performance</summary>
    public DateTime? LastVisit { get; set; }

    /// <summary>Derived from TotalSpent: Bronze/Silver/Gold/Platinum</summary>
    public string? MembershipTier { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Người tạo khách hàng</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Cửa hàng mà khách hàng thuộc về</summary>
    public Guid? BranchId { get; set; }

    public virtual User? CreatedByUser { get; set; }
    public virtual Branch? Branch { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    public virtual ICollection<LoyaltyTransaction> LoyaltyTransactions { get; set; } = new List<LoyaltyTransaction>();
}
