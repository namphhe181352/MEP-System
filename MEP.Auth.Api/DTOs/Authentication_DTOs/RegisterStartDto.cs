using System.ComponentModel.DataAnnotations;

namespace MEP.Auth.Api.DTOs.Authentication_DTOs
{
    public class RegisterStartDto
    {
        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; } = null!;
    }
}