using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AuthenticationDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using MEP_Management_System.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.AuthServices;

public class AuthService : IAuthService
{
    private readonly MEPDatabaseContext _context;
    private readonly IEmailService _emailService;
    private readonly JwtHelper _jwtHelper;
    private readonly IMapper _mapper;

    public AuthService(
        MEPDatabaseContext context,
        IEmailService emailService,
        JwtHelper jwtHelper,
        IMapper mapper)
    {
        _context = context;
        _emailService = emailService;
        _jwtHelper = jwtHelper;
        _mapper = mapper;
    }

    public async Task SendRegistrationOtpAsync(RegisterStartDto dto)
    {
        var emailExists = await _context.Users.AnyAsync(x => x.Email == dto.Email);
        if (emailExists)
        {
            throw new BusinessException("Email này đã được đăng ký tài khoản.");
        }

        var otpCode = GenerateOtpCode();

        var otpVerification = new OtpVerification
        {
            OtpId = Guid.NewGuid(),
            Email = dto.Email,
            OtpCode = otpCode,
            Purpose = "REGISTER",
            ExpiredAt = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(10), DateTimeKind.Unspecified),
            IsUsed = 0
        };

        _context.OtpVerifications.Add(otpVerification);
        await _context.SaveChangesAsync();

        await _emailService.SendOtpEmailAsync(dto.Email, otpCode);
    }

    public async Task<bool> VerifyRegistrationOtpAsync(RegisterVerifyDto dto)
    {
        var otp = await _context.OtpVerifications
            .FirstOrDefaultAsync(x => x.Email == dto.Email
                                   && x.OtpCode == dto.OtpCode
                                   && x.IsUsed == 0
                                   && x.ExpiredAt > DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified));

        if (otp != null)
        {
            otp.IsUsed = 1;
            await _context.SaveChangesAsync();
            return true;
        }

        return false;
    }

    public async Task SendForgotPasswordOtpAsync(ForgotPasswordStartDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == dto.Email);
        if (user == null)
        {
            throw new BusinessException("Email chưa được đăng ký.");
        }

        if (user.IsActive != 1 || !user.IsVerified)
        {
            throw new BusinessException("Tài khoản đang bị khóa hoặc chưa xác minh.");
        }

        var oldOtps = await _context.OtpVerifications
            .Where(x => x.Email == dto.Email && x.Purpose == "RESET_PASSWORD" && x.IsUsed == 0)
            .ToListAsync();
        foreach (var old in oldOtps) old.IsUsed = 1;

        var otpCode = GenerateOtpCode();
        var otpVerification = new OtpVerification
        {
            OtpId = Guid.NewGuid(),
            Email = dto.Email,
            OtpCode = otpCode,
            Purpose = "RESET_PASSWORD",
            ExpiredAt = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(10), DateTimeKind.Unspecified),
            IsUsed = 0
        };
        _context.OtpVerifications.Add(otpVerification);
        await _context.SaveChangesAsync();

        await _emailService.SendOtpEmailAsync(dto.Email, otpCode);
    }

    public async Task ResetPasswordWithOtpAsync(ForgotPasswordVerifyDto dto)
    {
        var otp = await _context.OtpVerifications
            .FirstOrDefaultAsync(x => x.Email == dto.Email
                                   && x.OtpCode == dto.OtpCode
                                   && x.Purpose == "RESET_PASSWORD"
                                   && x.IsUsed == 0
                                   && x.ExpiredAt > DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified));

        if (otp == null)
        {
            throw new BusinessException("Mã xác nhận không hợp lệ hoặc đã hết hạn.");
        }

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == dto.Email);
        if (user == null)
        {
            throw new BusinessException("Tài khoản không tồn tại.");
        }

        otp.IsUsed = 1;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword.Trim());
        await _context.SaveChangesAsync();
    }

    public async Task SendPasswordResetLinkAsync(ForgotPasswordRequest dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == dto.Email);
        if (user == null)
        {
            return;
        }

        var oldTokens = await _context.PasswordResetTokens
            .Where(t => t.UserId == user.UserId && t.UsedAt == null)
            .ToListAsync();
        foreach (var t in oldTokens) t.UsedAt = DateTime.UtcNow;

        var plainToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = ComputeSha256Hash(plainToken);
        var expiresAt = DateTime.UtcNow.AddMinutes(30);

        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };
        _context.PasswordResetTokens.Add(resetToken);
        await _context.SaveChangesAsync();

        var baseUrl = "https://mep.vn";
        var resetLink = $"{baseUrl}/reset-password?token={plainToken}";

        try
        {
            await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, resetLink);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EMAIL ERROR] Failed to send reset email: {ex.Message}");
        }
    }

    public async Task<ValidateResetTokenResponse> ValidateResetTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return new ValidateResetTokenResponse { IsValid = false };
        }

        var tokenHash = ComputeSha256Hash(token);
        var resetToken = await _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (resetToken == null || resetToken.UsedAt != null || resetToken.ExpiresAt < DateTime.UtcNow)
        {
            return new ValidateResetTokenResponse { IsValid = false };
        }

        return new ValidateResetTokenResponse
        {
            IsValid = true,
            Email = resetToken.User?.Email
        };
    }

    public async Task ResetPasswordWithTokenAsync(ResetPasswordRequest dto)
    {
        if (string.IsNullOrEmpty(dto.Token))
        {
            throw new BusinessException("Token là bắt buộc.");
        }

        if (string.IsNullOrEmpty(dto.NewPassword))
        {
            throw new BusinessException("Mật khẩu xác nhận không khớp.");
        }

        if (dto.NewPassword.Length < 6)
        {
            throw new BusinessException("Mật khẩu phải từ 6 ký tự trở lên.");
        }

        var tokenHash = ComputeSha256Hash(dto.Token);
        var resetToken = await _context.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (resetToken == null || resetToken.UsedAt != null || resetToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new BusinessException("Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");
        }

        var user = await _context.Users.FindAsync(resetToken.UserId);
        if (user == null)
        {
            throw new BusinessException("Tài khoản không tồn tại.");
        }

        resetToken.UsedAt = DateTime.UtcNow;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword.Trim());
        await _context.SaveChangesAsync();
    }

    public async Task<object> LoginAsync(LoginDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Email == dto.Email);
        if (user == null)
        {
            throw new BusinessException("Email này chưa được đăng ký.");
        }

        var isCorrectPassword = BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash);
        if (!isCorrectPassword)
        {
            throw new BusinessException("Mật khẩu không chính xác.");
        }

        if (user.IsActive != 1)
        {
            throw new BusinessException("Tài khoản của bạn hiện đang bị khóa.");
        }

        if (string.Equals(user.Status, "DELETED", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("Tài khoản đã bị xóa. Vui lòng liên hệ Admin.");
        }

        if (!user.IsVerified)
        {
            throw new BusinessException("Tài khoản của bạn chưa được xác thực.");
        }

        var userRoles = await _context.Roles
            .Where(r => r.Users.Any(u => u.UserId == user.UserId))
            .Select(r => r.RoleName)
            .ToListAsync();

        var isOwner = userRoles.Contains("Owner");
        var isAdmin = userRoles.Contains("Admin");

        string? branchIdForToken = null;

        if (isAdmin)
        {
            branchIdForToken = null;
        }
        else if (isOwner)
        {
            var branches = await _context.StaffAssignments
                .Where(sa => sa.UserId == user.UserId)
                .Include(sa => sa.Branch)
                .Select(sa => new
                {
                    sa.Branch.BranchId,
                    sa.Branch.BranchName,
                    sa.Branch.Address,
                    sa.Branch.City,
                    sa.Branch.Phone,
                    sa.Branch.IsActive
                })
                .ToListAsync();

            if (user.DefaultBranchId != null)
            {
                branchIdForToken = user.DefaultBranchId.ToString();
            }
            else if (branches.Any())
            {
                branchIdForToken = branches.First().BranchId.ToString();
            }
            else
            {
                var firstActiveBranch = await _context.Branches.FirstOrDefaultAsync(b => b.IsActive == 1);
                if (firstActiveBranch != null)
                {
                    branchIdForToken = firstActiveBranch.BranchId.ToString();
                }
            }
        }
        else
        {
            if (user.DefaultBranchId.HasValue)
            {
                branchIdForToken = user.DefaultBranchId.Value.ToString();
            }
            else
            {
                var assignment = await _context.StaffAssignments
                    .Where(sa => sa.UserId == user.UserId)
                    .OrderByDescending(sa => sa.AssignedAt)
                    .FirstOrDefaultAsync();

                if (assignment == null)
                {
                    var firstBranch = await _context.Branches.FirstOrDefaultAsync(b => b.IsActive == 1);
                    if (firstBranch != null)
                    {
                        user.DefaultBranchId = firstBranch.BranchId;
                        _context.StaffAssignments.Add(new StaffAssignment
                        {
                            AssignmentId = Guid.NewGuid(),
                            UserId = user.UserId,
                            BranchId = firstBranch.BranchId,
                            AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
                        });
                        await _context.SaveChangesAsync();
                        branchIdForToken = firstBranch.BranchId.ToString();
                    }
                    else
                    {
                        throw new BusinessException("Tài khoản chưa được phân công chi nhánh.");
                    }
                }
                else
                {
                    user.DefaultBranchId = assignment.BranchId;
                    await _context.SaveChangesAsync();
                    branchIdForToken = assignment.BranchId.ToString();
                }
            }
        }

        var rolePermissions = await _context.Roles
            .Where(r => r.Users.Any(u => u.UserId == user.UserId))
            .SelectMany(r => r.Permissions)
            .Select(p => p.PermissionCode)
            .ToListAsync();

        var userPermissions = await _context.UserPermissions
            .Where(up => up.UserId == user.UserId)
            .Select(up => new { up.Permission.PermissionCode, up.IsGranted })
            .ToListAsync();

        var deniedPermissions = userPermissions
            .Where(up => up.IsGranted == false)
            .Select(up => up.PermissionCode)
            .ToHashSet();

        var allowedUserPermissions = userPermissions
            .Where(up => up.IsGranted != false)
            .Select(up => up.PermissionCode);

        var allPermissions = rolePermissions
            .Union(allowedUserPermissions)
            .Except(deniedPermissions)
            .Distinct()
            .ToList();

        var jwtToken = _jwtHelper.GenerateToken(
            user.UserId,
            user.Username,
            user.FullName,
            userRoles,
            allPermissions,
            branchId: branchIdForToken
        );

        var responseDto = _mapper.Map<UserResponseDto>(user);
        responseDto.Token = jwtToken;
        responseDto.Roles = userRoles;
        responseDto.Permissions = allPermissions;

        return new
        {
            message = "Đăng nhập thành công!",
            roles = userRoles,
            data = responseDto
        };
    }

    private static string GenerateOtpCode()
    {
        return new Random().Next(100000, 999999).ToString();
    }

    private static string ComputeSha256Hash(string rawData)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
