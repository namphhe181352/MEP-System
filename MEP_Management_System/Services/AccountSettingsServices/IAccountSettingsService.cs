using System;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.AccountSettings_DTOs;

namespace MEP_Management_System.Services.AccountSettingsServices;

/// <summary>
/// Interface cho AccountSettingsService.
/// </summary>
public interface IAccountSettingsService
{
    /// <summary>
    /// Lấy profile của user.
    /// </summary>
    Task<AccountProfileDto?> GetProfileAsync(Guid userId);

    /// <summary>
    /// Cập nhật profile user (FullName, PhoneNumber).
    /// </summary>
    Task<AccountProfileDto> UpdateProfileAsync(Guid userId, string fullName, string? phoneNumber);

    /// <summary>
    /// Đổi mật khẩu.
    /// </summary>
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
}
