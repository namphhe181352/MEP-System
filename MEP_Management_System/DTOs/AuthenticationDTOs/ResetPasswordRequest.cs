using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.AuthenticationDTOs;

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "Token là bắt buộc.")]
    public string Token { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
    [StringLength(255, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên.")]
    public string NewPassword { get; set; } = null!;

    [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc.")]
    public string ConfirmPassword { get; set; } = null!;
}
