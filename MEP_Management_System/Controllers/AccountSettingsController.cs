using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AccountSettings_DTOs;
using MEP_Management_System.Services.AccountSettingsServices;
using System;
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
        private readonly IAccountSettingsService _accountSettingsService;

        public AccountSettingsController(IAccountSettingsService accountSettingsService)
        {
            _accountSettingsService = accountSettingsService;
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

            var dto = await _accountSettingsService.GetProfileAsync(userId.Value);
            if (dto == null)
            {
                return NotFound(new ApiResponse<AccountProfileDto>
                {
                    Success = false,
                    Message = "Không tìm thấy người dùng."
                });
            }

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

            var dto = await _accountSettingsService.UpdateProfileAsync(userId.Value, request.FullName, request.PhoneNumber);

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

            await _accountSettingsService.ChangePasswordAsync(userId.Value, request.CurrentPassword, request.NewPassword);

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
