using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.AccountSettings_DTOs
{
    /// <summary>
    /// Request DTO cho PUT /api/account-settings/me
    /// </summary>
    public class UpdateProfileRequest
    {
        [Required(ErrorMessage = "Họ tên là bắt buộc.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Họ tên phải từ 1 đến 100 ký tự.")]
        public string FullName { get; set; } = null!;

        [StringLength(20, ErrorMessage = "Số điện thoại không quá 20 ký tự.")]
        [RegularExpression(@"^(0[0-9]{9,10})$", ErrorMessage = "Số điện thoại không hợp lệ. Phải bắt đầu bằng 0 và có 10-11 chữ số.")]
        public string? PhoneNumber { get; set; }
    }
}
