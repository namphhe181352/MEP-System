using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.AuthenticationDTOs
{
    public class RegisterStartDto
    {
        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; } = null!;
    }
}
