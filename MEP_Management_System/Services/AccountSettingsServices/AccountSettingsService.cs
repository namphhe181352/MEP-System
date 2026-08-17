using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MEP_Management_System.Models;
using MEP_Management_System.DTOs.AccountSettings_DTOs;
using MEP_Management_System.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.AccountSettingsServices;

/// <summary>
/// Service xử lý logic nghiệp vụ cho Account Settings.
/// </summary>
public class AccountSettingsService : IAccountSettingsService
{
    private readonly MEPDatabaseContext _context;

    public AccountSettingsService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<AccountProfileDto?> GetProfileAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
            return null;

        return new AccountProfileDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Status = user.Status ?? "ACTIVE",
            SuspendedUntil = user.SuspendedUntil,
            CreatedAt = user.CreatedAt,
            Roles = user.Roles.Select(r => r.RoleName).ToList()
        };
    }

    public async Task<AccountProfileDto> UpdateProfileAsync(Guid userId, string fullName, string? phoneNumber)
    {
        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
            throw new KeyNotFoundException("Không tìm thấy người dùng.");

        // Kiểm tra SĐT đã tồn tại chưa (nếu có thay đổi)
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            var phoneTrimmed = phoneNumber.Trim();

            // Validate định dạng số điện thoại Việt Nam (10 số, bắt đầu 0[35789])
            if (!Regex.IsMatch(phoneTrimmed, @"^0[35789]\d{8}$"))
            {
                throw new BusinessException("Số điện thoại không hợp lệ. Vui lòng nhập số điện thoại Việt Nam 10 chữ số.");
            }

            var existingUser = await _context.Users
                .AnyAsync(u => u.PhoneNumber == phoneTrimmed && u.UserId != userId);

            if (existingUser)
            {
                throw new BusinessException("Số điện thoại đã được sử dụng bởi tài khoản khác.");
            }
        }

        // Cập nhật chỉ FullName và PhoneNumber
        user.FullName = fullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();

        await _context.SaveChangesAsync();

        return new AccountProfileDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Status = user.Status ?? "ACTIVE",
            SuspendedUntil = user.SuspendedUntil,
            CreatedAt = user.CreatedAt,
            Roles = user.Roles.Select(r => r.RoleName).ToList()
        };
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new KeyNotFoundException("Không tìm thấy người dùng.");

        // Xác minh mật khẩu hiện tại
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            throw new BusinessException("Mật khẩu hiện tại không đúng.");
        }

        // Kiểm tra mật khẩu mới không được trùng với mật khẩu cũ
        if (BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash))
        {
            throw new BusinessException("Mật khẩu mới không được trùng với mật khẩu cũ.");
        }

        // Hash và lưu mật khẩu mới
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _context.SaveChangesAsync();

        return true;
    }
}
