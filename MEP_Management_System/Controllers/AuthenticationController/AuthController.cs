using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AuthenticationDTOs;
using MEP_Management_System.Services.AuthServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.Tasks;

namespace MEP_Management_System.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Initiates registration by sending OTP to email.
        /// </summary>
        [EnableRateLimiting("StrictLimit")]
        [HttpPost("register/start")]
        public async Task<IActionResult> RegisterStart([FromBody] RegisterStartDto dto)
        {
            await _authService.SendRegistrationOtpAsync(dto);
            return Ok(new { message = "Mã xác thực OTP đã được gửi đến email của bạn thành công. Vui lòng kiểm tra hộp thư." });
        }

        /// <summary>
        /// Verifies OTP for registration. Note: Self-registration of Owner accounts is disabled.
        /// </summary>
        [HttpPost("register/verify")]
        public async Task<IActionResult> RegisterVerify([FromBody] RegisterVerifyDto dto)
        {
            var verified = await _authService.VerifyRegistrationOtpAsync(dto);
            return StatusCode(StatusCodes.Status410Gone, new
            {
                message = "Tính năng tự đăng ký tài khoản Owner đã ngừng hoạt động. Vui lòng liên hệ Admin để được tạo tài khoản."
            });
        }

        /// <summary>
        /// Initiates forgot password by sending OTP to email.
        /// </summary>
        [EnableRateLimiting("StrictLimit")]
        [HttpPost("forgot-password/start")]
        public async Task<IActionResult> ForgotPasswordStart([FromBody] ForgotPasswordStartDto dto)
        {
            await _authService.SendForgotPasswordOtpAsync(dto);
            return Ok(new { message = "Mã xác nhận đã được gửi đến email của bạn." });
        }

        /// <summary>
        /// Verifies OTP and resets password.
        /// </summary>
        [EnableRateLimiting("StrictLimit")]
        [HttpPost("forgot-password/verify")]
        public async Task<IActionResult> ForgotPasswordVerify([FromBody] ForgotPasswordVerifyDto dto)
        {
            await _authService.ResetPasswordWithOtpAsync(dto);
            return Ok(new { message = "Đặt lại mật khẩu thành công." });
        }

        /// <summary>
        /// Initiates forgot password with token-based reset link.
        /// </summary>
        [EnableRateLimiting("StrictLimit")]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest dto)
        {
            await _authService.SendPasswordResetLinkAsync(dto);
            return Ok(new { message = "Nếu email tồn tại trong hệ thống, một liên kết đặt lại mật khẩu đã được gửi." });
        }

        /// <summary>
        /// Validates a password reset token.
        /// </summary>
        [HttpGet("validate-reset-token")]
        public async Task<IActionResult> ValidateResetToken([FromQuery] string token)
        {
            var result = await _authService.ValidateResetTokenAsync(token);
            return Ok(result);
        }

        /// <summary>
        /// Resets password using a valid token.
        /// </summary>
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest dto)
        {
            await _authService.ResetPasswordWithTokenAsync(dto);
            return Ok(new { message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập với mật khẩu mới." });
        }

        /// <summary>
        /// Authenticates user and returns JWT token.
        /// </summary>
        [EnableRateLimiting("StrictLimit")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var result = await _authService.LoginAsync(dto);
            return Ok(result);
        }
    }
}
