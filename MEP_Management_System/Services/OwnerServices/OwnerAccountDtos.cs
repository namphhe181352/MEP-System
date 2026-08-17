namespace MEP_Management_System.Services.OwnerServices;

public class CreateOwnerRequest
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string Password { get; set; } = null!;
    public string Role { get; set; } = null!;
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? SubscriptionPlan { get; set; }
}

public class UpdateOwnerRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Role { get; set; }
    public string? SubscriptionPlan { get; set; }
}

public class UpdateOwnerStatusRequest
{
    public string Status { get; set; } = null!;
}

public class BanOwnerRequest
{
    public string Reason { get; set; } = null!;
}
