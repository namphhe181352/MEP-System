namespace MEP.Admin.Api.DTOs;

public class SystemNotificationDto
{
    public Guid NotificationId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string Target { get; set; } = null!;
    public bool IsUrgent { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string Status { get; set; } = null!;
    public string CreatedByName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
