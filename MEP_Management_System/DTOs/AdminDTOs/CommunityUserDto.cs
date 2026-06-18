namespace MEP_Management_System.DTOs;

public class CommunityUserDto
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
    public DateTime? CreatedAt { get; set; }
}
