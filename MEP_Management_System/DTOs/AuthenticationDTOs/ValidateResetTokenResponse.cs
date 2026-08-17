namespace MEP_Management_System.DTOs.AuthenticationDTOs;

public class ValidateResetTokenResponse
{
    public bool IsValid { get; set; }
    public string? Email { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
