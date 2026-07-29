using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.AuthenticationDTOs;

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = null!;
}
