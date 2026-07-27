using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Controllers;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Services.Email;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class AuthControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<IMapper> _mockMapper;
        private Mock<IEmailService> _mockEmailService;
        private JwtHelper _jwtHelper;

        public AuthControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockMapper = new Mock<IMapper>();
            _mockEmailService = new Mock<IEmailService>();

            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["Jwt:Key"]).Returns("ThisIsAVerySecretKeyForJwtAuthentication123!");
            mockConfig.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
            mockConfig.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");
            _jwtHelper = new JwtHelper(mockConfig.Object);
        }

        private authController CreateController(MEPDatabaseContext context)
        {
            return new authController(context, _mockMapper.Object, _jwtHelper, _mockEmailService.Object);
        }

        [Fact]
        public async Task RegisterStart_EmailAlreadyExists_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            context.Users.Add(new User { UserId = Guid.NewGuid(), Email = "test@test.com", Username = "test", PasswordHash = "hash", FullName = "Test" });
            await context.SaveChangesAsync();

            var request = new RegisterStartDto { Email = "test@test.com" };

            // Act
            var result = await controller.RegisterStart(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequestResult.Value);
            var valueType = badRequestResult.Value.GetType();
            var message = valueType.GetProperty("message")?.GetValue(badRequestResult.Value, null) as string;
            Assert.Equal("Email này đã được đăng ký tài khoản.", message);
        }

        [Fact]
        public async Task RegisterStart_ValidEmail_ReturnsOkAndSendsEmail()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var request = new RegisterStartDto { Email = "newuser@test.com" };

            _mockEmailService.Setup(e => e.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                             .Returns(Task.CompletedTask);

            // Act
            var result = await controller.RegisterStart(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            _mockEmailService.Verify(e => e.SendOtpEmailAsync("newuser@test.com", It.IsAny<string>()), Times.Once);
            
            var otpInDb = await context.OtpVerifications.FirstOrDefaultAsync(o => o.Email == "newuser@test.com");
            Assert.NotNull(otpInDb);
            Assert.Equal("REGISTER", otpInDb.Purpose);
        }

        [Fact]
        public async Task RegisterVerify_InvalidOtp_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var request = new RegisterVerifyDto { Email = "newuser@test.com", OtpCode = "123456", Password = "Password123!", FullName = "New User", BranchName = "Branch 1" };

            // Act
            var result = await controller.RegisterVerify(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var valueType = badRequestResult.Value.GetType();
            var message = valueType.GetProperty("message")?.GetValue(badRequestResult.Value, null) as string;
            Assert.Equal("Mã OTP không chính xác hoặc đã hết hạn.", message);
        }

        [Fact]
        public async Task RegisterVerify_ValidOtp_ReturnsOkAndCreatesUserAndBranch()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            context.OtpVerifications.Add(new OtpVerification
            {
                OtpId = Guid.NewGuid(),
                Email = "newuser@test.com",
                OtpCode = "123456",
                Purpose = "REGISTER",
                IsUsed = 0,
                ExpiredAt = DateTime.UtcNow.AddMinutes(10)
            });
            await context.SaveChangesAsync();

            var request = new RegisterVerifyDto { Email = "newuser@test.com", OtpCode = "123456", Password = "Password123!", FullName = "New User", BranchName = "Branch 1" };

            // Act
            var result = await controller.RegisterVerify(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var userInDb = await context.Users.FirstOrDefaultAsync(u => u.Email == "newuser@test.com");
            Assert.NotNull(userInDb);
            Assert.Equal("New User", userInDb.FullName);
            Assert.True(userInDb.IsVerified);

            var branchInDb = await context.Branches.FirstOrDefaultAsync(b => b.BranchName == "Branch 1");
            Assert.NotNull(branchInDb);
            Assert.Equal(userInDb.UserId, branchInDb.ManagerUserId);
        }

        [Fact]
        public async Task Login_InvalidEmail_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var request = new LoginDto { Email = "notfound@test.com", Password = "Password123!" };

            // Act
            var result = await controller.Login(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var valueType = badRequestResult.Value.GetType();
            var message = valueType.GetProperty("message")?.GetValue(badRequestResult.Value, null) as string;
            Assert.Equal("Email này chưa được đăng ký.", message);
        }
    }
}
