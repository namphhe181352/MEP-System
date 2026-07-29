using MailKit.Net.Smtp;
using MailKit.Security;
using MEP_Management_System.Services.Email;
using Microsoft.Extensions.Options;
using MimeKit;
using System;
using System.Threading.Tasks;

namespace MEP_Management_System.Services.Email
{
    public interface IEmailService
    {
        Task SendOtpEmailAsync(string toEmail, string otpCode);
        Task SendPasswordResetEmailAsync(string toEmail, string? fullName, string resetLink);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendOtpEmailAsync(string toEmail, string otpCode)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = "Mã xác thực OTP Đăng ký tài khoản - FAS-02 SYSTEM";

            // Tạo giao diện Email HTML chuyên nghiệp bằng Template chuỗi
            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                    <h2 style='color: #ff4500; text-align: center;'>FAS-02 SYSTEM</h2>
                    <p>Xin chào,</p>
                    <p>Bạn đã yêu cầu đăng ký tài khoản mới trên hệ thống <strong>FAS-02 SYSTEM</strong>.</p>
                    <p>Dưới đây là mã xác thực OTP của bạn (mã có hiệu lực trong vòng 10 phút):</p>
                    <div style='background-color: #f7f7f7; padding: 15px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 5px; color: #333; border-radius: 4px; margin: 20px 0; border: 1px dashed #cccccc;'>
                        {otpCode}
                    </div>
                    <p style='color: #888888; font-size: 12px;'>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này hoặc liên hệ quản trị viên.</p>
                    <hr style='border: none; border-top: 1px solid #eeeeee;' />
                    <p style='text-align: center; color: #aaaaaa; font-size: 11px;'>Đây là email tự động, vui lòng không phản hồi.</p>
                </div>"
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                // Kết nối tới SMTP Server sử dụng TLS bảo mật
                await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.Port, SecureSocketOptions.StartTls);

                // Xác thực tài khoản gửi mail
                await client.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.Password);

                // Thực hiện gửi
                await client.SendAsync(message);
            }
            catch (Exception ex)
            {
                // Log lỗi ra console nếu cấu hình sai hoặc lỗi kết nối
                Console.WriteLine($"[EMAIL ERROR] Không thể gửi email: {ex.Message}");
                throw new Exception("Lỗi hệ thống khi gửi email OTP. Vui lòng liên hệ hỗ trợ.");
            }
            finally
            {
                await client.DisconnectAsync(true);
            }
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string? fullName, string resetLink)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = "Dat lai mat khau - M.E.P System";

            var greeting = string.IsNullOrEmpty(fullName) ? "Xin chào" : $"Xin chào {fullName}";
            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                    <h2 style='color: #ff4500; text-align: center;'>M.E.P System</h2>
                    <p>{greeting},</p>
                    <p>Ban yeu cau dat lai mat khau tren he thong <strong>M.E.P System</strong>.</p>
                    <p>Ban hoac nguoi khac da yeu cau dat lai mat khau cho tai khoan nay. Neu la ban, vui long bam vao nut ben duoi de dat lai mat khau:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{resetLink}' style='background-color: #ff4500; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; font-size: 16px; font-weight: bold;'>Dat lai mat khau</a>
                    </div>
                    <p>Lien ket nay se het han trong <strong>30 phut</strong>.</p>
                    <p>Neu ban khong yeu cau dat lai mat khau, vui long bo qua email nay. Tai khoan cua ban van an toan.</p>
                    <hr style='border: none; border-top: 1px solid #eeeeee;' />
                    <p style='text-align: center; color: #aaaaaa; font-size: 11px;'>Day la email tu dong, vui long khong phan hoi.</p>
                </div>"
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.Port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_emailSettings.SenderEmail, _emailSettings.Password);
                await client.SendAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL ERROR] Khong the gui email reset mat khau: {ex.Message}");
                throw new Exception("Loi he thong khi gui email. Vui long lien he ho tro.");
            }
            finally
            {
                await client.DisconnectAsync(true);
            }
        }
    }
}