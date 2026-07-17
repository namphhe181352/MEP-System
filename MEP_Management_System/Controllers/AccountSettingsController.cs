using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AccountSettings_DTOs;
using MEP_Management_System.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MEP_Management_System.Controllers
{
    /// <summary>
    /// API Cài đặt tài khoản - cho phép người dùng xem/sửa profile và đổi mật khẩu của chính mình.
    /// Accessible by all authenticated roles.
    /// Route: /api/account-settings
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/account-settings")]
    public class AccountSettingsController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;

        public AccountSettingsController(MEPDatabaseContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy UserId của user hiện tại từ JWT token.
        /// </summary>
        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : null;
        }

        #region GET /api/account-settings/me

        /// <summary>
        /// Lấy thông tin profile của user hiện tại.
        /// GET /api/account-settings/me
        /// </summary>
        [HttpGet("me")]
        public async Task<ActionResult<ApiResponse<AccountProfileDto>>> GetProfile()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new ApiResponse<AccountProfileDto>
                {
                    Success = false,
                    Message = "Không thể xác định người dùng từ token."
                });
            }

            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.UserId == userId.Value);

            if (user == null)
            {
                return NotFound(new ApiResponse<AccountProfileDto>
                {
                    Success = false,
                    Message = "Không tìm thấy người dùng."
                });
            }

            var dto = new AccountProfileDto
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

            return Ok(new ApiResponse<AccountProfileDto>
            {
                Success = true,
                Message = "Lấy thông tin thành công.",
                Data = dto
            });
        }

        #endregion

        #region PUT /api/account-settings/me

        /// <summary>
        /// Cập nhật thông tin cá nhân của user hiện tại (chỉ FullName và PhoneNumber).
        /// PUT /api/account-settings/me
        /// </summary>
        [HttpPut("me")]
        public async Task<ActionResult<ApiResponse<AccountProfileDto>>> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new ApiResponse<AccountProfileDto>
                {
                    Success = false,
                    Message = "Không thể xác định người dùng từ token."
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(new ApiResponse<AccountProfileDto>
                {
                    Success = false,
                    Message = "Dữ liệu không hợp lệ.",
                    Errors = errors
                });
            }

            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.UserId == userId.Value);

            if (user == null)
            {
                return NotFound(new ApiResponse<AccountProfileDto>
                {
                    Success = false,
                    Message = "Không tìm thấy người dùng."
                });
            }

            // Kiểm tra SĐT đã tồn tại chưa (nếu có thay đổi)
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var phoneTrimmed = request.PhoneNumber.Trim();
                var existingUser = await _context.Users
                    .AnyAsync(u => u.PhoneNumber == phoneTrimmed && u.UserId != userId.Value);

                if (existingUser)
                {
                    return BadRequest(new ApiResponse<AccountProfileDto>
                    {
                        Success = false,
                        Message = "Số điện thoại đã được sử dụng bởi tài khoản khác."
                    });
                }
            }

            // Cập nhật chỉ FullName và PhoneNumber
            user.FullName = request.FullName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

            await _context.SaveChangesAsync();

            var dto = new AccountProfileDto
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

            return Ok(new ApiResponse<AccountProfileDto>
            {
                Success = true,
                Message = "Cập nhật thông tin thành công.",
                Data = dto
            });
        }

        #endregion

        #region POST /api/account-settings/me/change-password

        /// <summary>
        /// Đổi mật khẩu của user hiện tại.
        /// POST /api/account-settings/me/change-password
        /// </summary>
        [HttpPost("me/change-password")]
        public async Task<ActionResult<ApiResponse<bool>>> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Không thể xác định người dùng từ token."
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Dữ liệu không hợp lệ.",
                    Errors = errors
                });
            }

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
            {
                return NotFound(new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Không tìm thấy người dùng."
                });
            }

            // Xác minh mật khẩu hiện tại
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                return BadRequest(new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Mật khẩu hiện tại không đúng."
                });
            }

            // Hash và lưu mật khẩu mới
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Message = "Đổi mật khẩu thành công.",
                Data = true
            });
        }

        #endregion
    }
}
