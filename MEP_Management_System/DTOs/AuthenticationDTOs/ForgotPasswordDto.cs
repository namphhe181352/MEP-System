namespace MEP_Management_System.DTOs.AuthenticationDTOs;

public class ForgotPasswordDto
{
    public string Email { get; set; } = null!;
}

public class ForgotPasswordStartDto
{
    public string Email { get; set; } = null!;
}

public class ForgotPasswordVerifyDto
{
    public string Email { get; set; } = null!;
    public string OtpCode { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}
