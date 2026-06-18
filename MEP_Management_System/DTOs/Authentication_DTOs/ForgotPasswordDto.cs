using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs
{
    public class ForgotPasswordDto
    {
        [Required]
        public string Username { get; set; } = null!;
        [Required]
        public string NewPassword { get; set; } = null!;
    }
}
