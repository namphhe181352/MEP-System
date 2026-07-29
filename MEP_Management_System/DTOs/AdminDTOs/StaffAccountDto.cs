namespace MEP_Management_System.DTOs;

public class StaffAccountDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public short IsActive { get; set; }
    public bool IsVerified { get; set; }
    public string? Status { get; set; }
    public DateTime? SuspendedUntil { get; set; }
    public string? SubscriptionPlan { get; set; }
    public List<string> Roles { get; set; } = new();
    public DateTime? CreatedAt { get; set; }
}
