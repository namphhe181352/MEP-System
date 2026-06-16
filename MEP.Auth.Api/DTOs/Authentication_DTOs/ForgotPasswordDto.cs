using System.ComponentModel.DataAnnotations;

namespace MEP.Auth.Api.DTOs.Authentication_DTOs
{
    public class ForgotPasswordDto
    {
        [Required]
        public string Username { get; set; } = null!;
        [Required]
        public string NewPassword { get; set; } = null!;
    }
}
