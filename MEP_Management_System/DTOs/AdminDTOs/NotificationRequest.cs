namespace MEP_Management_System.DTOs.AdminDTOs;

public class CreateNotificationRequest
{
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string Target { get; set; } = "ALL"; // ALL / OWNER / STAFF / COMMUNITY
    public bool IsUrgent { get; set; } = false;
    public DateTime? ScheduledAt { get; set; }
    public Guid? BranchId { get; set; }
}

public class UpdateNotificationRequest
{
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? Target { get; set; }
    public bool? IsUrgent { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public Guid? BranchId { get; set; }
}
