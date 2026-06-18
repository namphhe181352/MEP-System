using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs;

public class CreateStaffDto
{
    [Required(ErrorMessage = "Họ và tên nhân viên không được để trống.")]
    [StringLength(100, ErrorMessage = "Họ và tên không được vượt quá 100 ký tự.")]
    public string FullName { get; set; } = null!;

    [Required(ErrorMessage = "Email không được để trống.")]
    [EmailAddress(ErrorMessage = "Định dạng Email không hợp lệ.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Mật khẩu không được để trống.")]
    [StringLength(50, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 đến 50 ký tự.")]
    public string Password { get; set; } = null!;
}