using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs;

public class ForgotPasswordStartDto
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = null!;
}

public class ForgotPasswordVerifyDto
{
    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Mã xác nhận là bắt buộc.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã xác nhận phải gồm 6 chữ số.")]
    public string OtpCode { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
    [StringLength(255, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên.")]
    public string NewPassword { get; set; } = null!;
}
