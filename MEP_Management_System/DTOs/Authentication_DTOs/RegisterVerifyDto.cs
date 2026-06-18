using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs
{
    public class RegisterVerifyDto
    {
        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Mã OTP không được để trống")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải gồm 6 chữ số")]
        public string OtpCode { get; set; } = null!;

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [StringLength(255, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Họ và tên chủ cửa hàng không được để trống")]
        public string FullName { get; set; } = null!;

        [Required(ErrorMessage = "Tên doanh nghiệp/Cửa hàng không được để trống")]
        public string BranchName { get; set; } = null!;
    }
}