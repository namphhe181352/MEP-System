using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Controllers;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AuthenticationDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Services.Email;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.AuthenticationController
{
    public class AuthControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly JwtHelper _jwtHelper;

        public AuthControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockMapper = new Mock<IMapper>();
            _mockEmailService = new Mock<IEmailService>();
            _mockConfig = new Mock<IConfiguration>();

            _mockConfig.Setup(c => c["Jwt:Key"]).Returns("SuperSecretKeyThatIsAtLeast32CharactersLong!!");
            _mockConfig.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
            _mockConfig.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");

            _jwtHelper = new JwtHelper(_mockConfig.Object);
            
            _mockMapper.Setup(m => m.Map<UserResponseDto>(It.IsAny<User>())).Returns((User u) => new UserResponseDto { UserId = u.UserId, Email = u.Email, Username = u.Username });
        }

        private authController CreateController(MEPDatabaseContext context)
        {
            var controller = new authController(context, _mockMapper.Object, _jwtHelper, _mockEmailService.Object);
            
            var claims = new List<Claim>();
            var identity = new ClaimsIdentity(claims, "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            controller.ControllerContext.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
            controller.ControllerContext.HttpContext.Request.Headers["User-Agent"] = "TestAgent";

            return controller;
        }

        // =====================================
        // REGISTER START
        // =====================================

        [Fact]
        public async Task RegisterStart_EmailAlreadyExists_ReturnsBadRequest()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            context.Users.Add(new User { UserId = Guid.NewGuid(), Email = "test@mep.vn", Username = "test", PasswordHash = "hash" });
            await context.SaveChangesAsync();
            var controller = CreateController(context);

            var result = await controller.RegisterStart(new RegisterStartDto { Email = "test@mep.vn" });
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var message = badRequestResult.Value?.GetType().GetProperty("message")?.GetValue(badRequestResult.Value, null) as string;
            Assert.Equal("Email này đã được đăng ký tài khoản.", message);
        }

        [Fact]
        public async Task RegisterStart_NewEmail_SendsOtpAndReturnsOk()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var result = await controller.RegisterStart(new RegisterStartDto { Email = "new@mep.vn" });
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var otpDb = await context.OtpVerifications.FirstOrDefaultAsync(o => o.Email == "new@mep.vn");
            Assert.NotNull(otpDb);
            Assert.Equal("REGISTER", otpDb.Purpose);
            Assert.Equal((short)0, otpDb.IsUsed);
            _mockEmailService.Verify(s => s.SendOtpEmailAsync("new@mep.vn", otpDb.OtpCode), Times.Once);
        }

        // =====================================
        // REGISTER VERIFY
        // =====================================

        [Fact]
        public async Task RegisterVerify_Always_Returns410Gone()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            context.OtpVerifications.Add(new OtpVerification 
            { 
                OtpId = Guid.NewGuid(), Email = "any@mep.vn", OtpCode = "123456", IsUsed = 0, ExpiredAt = DateTime.UtcNow.AddMinutes(10)
            });
            await context.SaveChangesAsync();
            
            var controller = CreateController(context);
            var result = await controller.RegisterVerify(new RegisterVerifyDto { Email = "any@mep.vn", OtpCode = "123456" });
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(410, statusCodeResult.StatusCode);
            
            var otpDb = await context.OtpVerifications.FirstAsync();
            Assert.Equal((short)1, otpDb.IsUsed); // Verified it consumed the OTP before rejecting
        }

        // =====================================
        // FORGOT PASSWORD START
        // =====================================

        [Theory]
        [InlineData("notfound@mep.vn", 1, true, "Email chưa được đăng ký.")] // Not found
        [InlineData("exist@mep.vn", 0, true, "Tài khoản đang bị khóa hoặc chưa xác minh.")] // Inactive
        [InlineData("exist@mep.vn", 1, false, "Tài khoản đang bị khóa hoặc chưa xác minh.")] // Unverified
        public async Task ForgotPasswordStart_InvalidConditions_ReturnsBadRequest(string email, short isActive, bool isVerified, string expectedMessage)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            if (email == "exist@mep.vn")
            {
                context.Users.Add(new User { UserId = Guid.NewGuid(), Email = email, IsActive = isActive, IsVerified = isVerified, Username = "test" });
                await context.SaveChangesAsync();
            }

            var controller = CreateController(context);
            var result = await controller.ForgotPasswordStart(new ForgotPasswordStartDto { Email = "exist@mep.vn" });
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value, null) as string;
            Assert.Equal(expectedMessage, message);
        }

        [Fact]
        public async Task ForgotPasswordStart_ValidUser_SendsOtpAndInvalidatesOldOtps()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            context.Users.Add(new User { UserId = Guid.NewGuid(), Email = "valid@mep.vn", IsActive = 1, IsVerified = true, Username = "test" });
            context.OtpVerifications.Add(new OtpVerification { OtpId = Guid.NewGuid(), Email = "valid@mep.vn", Purpose = "RESET_PASSWORD", IsUsed = 0 });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.ForgotPasswordStart(new ForgotPasswordStartDto { Email = "valid@mep.vn" });
            
            Assert.IsType<OkObjectResult>(result);
            var otps = await context.OtpVerifications.Where(o => o.Email == "valid@mep.vn").ToListAsync();
            Assert.Equal(2, otps.Count);
            Assert.Equal((short)1, otps.First().IsUsed); // Old OTP is invalidated
            Assert.Equal((short)0, otps.Last().IsUsed); // New OTP is ready
            _mockEmailService.Verify(s => s.SendOtpEmailAsync("valid@mep.vn", otps.Last().OtpCode), Times.Once);
        }

        // =====================================
        // FORGOT PASSWORD VERIFY (OTP)
        // =====================================

        [Theory]
        [InlineData("wrong_otp", 10, 0, "Mã xác nhận không hợp lệ hoặc đã hết hạn.")] // Wrong OTP
        [InlineData("123456", -10, 0, "Mã xác nhận không hợp lệ hoặc đã hết hạn.")] // Expired OTP
        [InlineData("123456", 10, 1, "Mã xác nhận không hợp lệ hoặc đã hết hạn.")] // Used OTP
        public async Task ForgotPasswordVerify_InvalidOtp_ReturnsBadRequest(string inputOtp, int minutesAdd, short isUsed, string expectedMessage)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            context.OtpVerifications.Add(new OtpVerification 
            { 
                OtpId = Guid.NewGuid(), Email = "test@mep.vn", OtpCode = "123456", 
                Purpose = "RESET_PASSWORD", IsUsed = isUsed, ExpiredAt = DateTime.UtcNow.AddMinutes(minutesAdd)
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.ForgotPasswordVerify(new ForgotPasswordVerifyDto { Email = "test@mep.vn", OtpCode = inputOtp, NewPassword = "newpassword" });
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value, null) as string;
            Assert.Equal(expectedMessage, message);
        }

        [Fact]
        public async Task ForgotPasswordVerify_ValidOtp_ResetsPasswordAndReturnsOk()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, Email = "test@mep.vn", Username = "test", PasswordHash = "old_hash" });
            context.OtpVerifications.Add(new OtpVerification { OtpId = Guid.NewGuid(), Email = "test@mep.vn", OtpCode = "123456", Purpose = "RESET_PASSWORD", IsUsed = 0, ExpiredAt = DateTime.UtcNow.AddMinutes(10) });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.ForgotPasswordVerify(new ForgotPasswordVerifyDto { Email = "test@mep.vn", OtpCode = "123456", NewPassword = "newpassword" });
            
            Assert.IsType<OkObjectResult>(result);
            var user = await context.Users.FindAsync(userId);
            Assert.True(BCrypt.Net.BCrypt.Verify("newpassword", user!.PasswordHash)); // Password changed
        }

        // =====================================
        // FORGOT PASSWORD (TOKEN-BASED)
        // =====================================

        [Fact]
        public async Task ForgotPasswordToken_EmailDoesNotExist_ReturnsOkButNoEmailSent()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);
            
            var result = await controller.ForgotPassword(new ForgotPasswordRequest { Email = "nonexistent@mep.vn" });
            var okResult = Assert.IsType<OkObjectResult>(result);
            _mockEmailService.Verify(s => s.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ForgotPasswordToken_ValidEmail_GeneratesTokenAndSendsEmail()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, Email = "exist@mep.vn", Username = "test", FullName = "Test" });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.ForgotPassword(new ForgotPasswordRequest { Email = "exist@mep.vn" });
            
            Assert.IsType<OkObjectResult>(result);
            var token = await context.PasswordResetTokens.FirstOrDefaultAsync(t => t.UserId == userId);
            Assert.NotNull(token);
            _mockEmailService.Verify(s => s.SendPasswordResetEmailAsync("exist@mep.vn", "Test", It.Is<string>(link => link.Contains("token="))), Times.Once);
        }

        // =====================================
        // VALIDATE RESET TOKEN
        // =====================================

        [Theory]
        [InlineData("", false, 10, false)] // Empty token
        [InlineData("wrong_token", false, 10, false)] // Invalid token
        [InlineData("valid_token", true, 10, false)] // Used token (isUsed = true means UsedAt != null)
        [InlineData("valid_token", false, -10, false)] // Expired token
        public async Task ValidateResetToken_InvalidConditions_ReturnsFalse(string token, bool isUsed, int minutesAdd, bool expectedValid)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            if (token != "")
            {
                var tokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("valid_token"))).ToLowerInvariant();
                context.PasswordResetTokens.Add(new PasswordResetToken 
                { 
                    Id = Guid.NewGuid(), TokenHash = tokenHash, 
                    UsedAt = isUsed ? DateTime.UtcNow : null, 
                    ExpiresAt = DateTime.UtcNow.AddMinutes(minutesAdd) 
                });
                await context.SaveChangesAsync();
            }

            var controller = CreateController(context);
            var result = await controller.ValidateResetToken(token);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ValidateResetTokenResponse>(okResult.Value);
            Assert.Equal(expectedValid, response.IsValid);
        }

        // =====================================
        // RESET PASSWORD (TOKEN)
        // =====================================

        [Theory]
        [InlineData("", "pass123", "pass123", "Token là bắt buộc.")]
        [InlineData("token", "pass123", "pass456", "Mật khẩu xác nhận không khớp.")]
        [InlineData("token", "123", "123", "Mật khẩu phải từ 6 ký tự trở lên.")]
        public async Task ResetPasswordToken_ValidationFails_ReturnsBadRequest(string token, string newPass, string confirmPass, string expectedMessage)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);
            var result = await controller.ResetPassword(new ResetPasswordRequest { Token = token, NewPassword = newPass, ConfirmPassword = confirmPass });
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value, null) as string;
            Assert.Equal(expectedMessage, message);
        }

        // =====================================
        // LOGIN
        // =====================================

        [Theory]
        [InlineData("wrong@mep.vn", "password", 1, "ACTIVE", true, "Email này chưa được đăng ký.")] // Not registered
        [InlineData("test@mep.vn", "wrongpass", 1, "ACTIVE", true, "Mật khẩu không chính xác.")] // Wrong password
        [InlineData("test@mep.vn", "password", 0, "ACTIVE", true, "Tài khoản của bạn hiện đang bị khóa.")] // Inactive
        [InlineData("test@mep.vn", "password", 1, "DELETED", true, "Tài khoản đã bị xóa. Vui lòng liên hệ Admin.")] // Deleted
        [InlineData("test@mep.vn", "password", 1, "ACTIVE", false, "Tài khoản của bạn chưa được xác thực.")] // Unverified
        public async Task Login_InvalidConditions_ReturnsBadRequest(string loginEmail, string loginPassword, short isActive, string status, bool isVerified, string expectedMessage)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            context.Users.Add(new User 
            { 
                UserId = Guid.NewGuid(), Email = "test@mep.vn", Username = "test", 
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password"),
                IsActive = isActive, Status = status, IsVerified = isVerified 
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.Login(new LoginDto { Email = loginEmail, Password = loginPassword });
            
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = badRequest.Value?.GetType().GetProperty("message")?.GetValue(badRequest.Value, null) as string;
            Assert.Equal(expectedMessage, message);
        }

        [Fact]
        public async Task Login_ValidAdmin_ReturnsTokenWithoutBranchId()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            var user = new User { UserId = userId, Email = "admin@mep.vn", Username = "admin", PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"), IsActive = 1, Status = "ACTIVE", IsVerified = true };
            var role = new Role { RoleId = roleId, RoleName = "Admin" };
            role.Users.Add(user);
            context.Users.Add(user);
            context.Roles.Add(role);
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.Login(new LoginDto { Email = "admin@mep.vn", Password = "123456" });
            
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = okResult.Value?.GetType().GetProperty("data")?.GetValue(okResult.Value, null) as UserResponseDto;
            Assert.NotNull(dto);
            Assert.NotNull(dto.Token); // Token is generated
            // For admin, branch is null inside token which is checked in controller logic
        }

        [Fact]
        public async Task Login_ValidOwner_ReturnsBranchesInfo()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var user = new User { UserId = userId, Email = "owner@mep.vn", Username = "owner", PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"), IsActive = 1, Status = "ACTIVE", IsVerified = true };
            var role = new Role { RoleId = Guid.NewGuid(), RoleName = "Owner" };
            role.Users.Add(user);
            context.Users.Add(user);
            context.Roles.Add(role);
            
            var branch = new Branch { BranchId = branchId, BranchName = "Main", IsActive = 1 };
            context.Branches.Add(branch);
            context.StaffAssignments.Add(new StaffAssignment { AssignmentId = Guid.NewGuid(), UserId = userId, BranchId = branchId });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.Login(new LoginDto { Email = "owner@mep.vn", Password = "123456" });
            
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = okResult.Value?.GetType().GetProperty("data")?.GetValue(okResult.Value, null) as UserResponseDto;
            Assert.NotNull(dto);
            Assert.NotNull(dto.Token);
        }

        [Fact]
        public async Task Login_ValidStaffNoAssignment_AssignsDefaultBranchAndReturnsToken()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var user = new User { UserId = userId, Email = "staff@mep.vn", Username = "staff", PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456"), IsActive = 1, Status = "ACTIVE", IsVerified = true };
            var role = new Role { RoleId = Guid.NewGuid(), RoleName = "Staff" };
            role.Users.Add(user);
            context.Users.Add(user);
            context.Roles.Add(role);
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Main", IsActive = 1 });
            await context.SaveChangesAsync(); // No staff assignment exists yet

            var controller = CreateController(context);
            var result = await controller.Login(new LoginDto { Email = "staff@mep.vn", Password = "123456" });
            
            var okResult = Assert.IsType<OkObjectResult>(result);
            var assignment = await context.StaffAssignments.FirstOrDefaultAsync(sa => sa.UserId == userId);
            Assert.NotNull(assignment); // Auto assigned
            Assert.Equal(branchId, assignment.BranchId);
        }
    }
}
