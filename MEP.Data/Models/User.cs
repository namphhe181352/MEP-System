using System;
using System.Collections.Generic;

namespace MEP.Data.Models;

public partial class User
{
    public Guid UserId { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public short? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string Email { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public bool IsVerified { get; set; } // NEW: for Owner store approval
    public string? Status { get; set; } // ACTIVE / SUSPENDED / BANNED
    public DateTime? SuspendedUntil { get; set; } // suspension end date
    public string? SubscriptionPlan { get; set; } // BASIC / PREMIUM / ENTERPRISE

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual ICollection<CommunityPost> CommunityPosts { get; set; } = new List<CommunityPost>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<ReturnOrder> ReturnOrders { get; set; } = new List<ReturnOrder>();

    public virtual ICollection<StaffAssignment> StaffAssignments { get; set; } = new List<StaffAssignment>();

    public virtual ICollection<StockTicket> StockTickets { get; set; } = new List<StockTicket>();

    public virtual ICollection<SystemLog> SystemLogs { get; set; } = new List<SystemLog>();

    public virtual ICollection<ViolationReport> ViolationReports { get; set; } = new List<ViolationReport>();

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

    public virtual ICollection<SystemNotification> SystemNotifications { get; set; } = new List<SystemNotification>();

    public virtual ICollection<StoreApproval> StoreApprovals { get; set; } = new List<StoreApproval>();
}
