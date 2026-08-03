using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class SystemNotification
{
    public Guid NotificationId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string Target { get; set; } = null!; // "ALL" / "OWNER" / "STAFF" / "COMMUNITY"
    public Guid? BranchId { get; set; }
    public bool IsUrgent { get; set; } // BR-48: Override Do-Not-Disturb
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string Status { get; set; } = "DRAFT"; // "DRAFT" / "SCHEDULED" / "SENT" / "CANCELLED"
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;
}
