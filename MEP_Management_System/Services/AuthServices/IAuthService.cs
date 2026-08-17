using System.Threading.Tasks;
using MEP_Management_System.DTOs.AuthenticationDTOs;

namespace MEP_Management_System.Services.AuthServices;

public interface IAuthService
{
    Task SendRegistrationOtpAsync(RegisterStartDto dto);
    Task<bool> VerifyRegistrationOtpAsync(RegisterVerifyDto dto);
    Task SendForgotPasswordOtpAsync(ForgotPasswordStartDto dto);
    Task ResetPasswordWithOtpAsync(ForgotPasswordVerifyDto dto);
    Task SendPasswordResetLinkAsync(ForgotPasswordRequest dto);
    Task<ValidateResetTokenResponse> ValidateResetTokenAsync(string token);
    Task ResetPasswordWithTokenAsync(ResetPasswordRequest dto);
    Task<object> LoginAsync(LoginDto dto);
}
