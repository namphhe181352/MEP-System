using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AccountSettings_DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers
{
    public class AccountSettingsControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public AccountSettingsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private AccountSettingsController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new AccountSettingsController(context);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            }

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            return controller;
        }

        [Fact]
        public async Task GetProfile_WithValidToken_ReturnsProfile()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var user = new User { UserId = userId, Username = "testuser", FullName = "Test User" };
            user.Roles.Add(new Role { RoleId = Guid.NewGuid(), RoleName = "Staff" });
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);
            var result = await controller.GetProfile();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<AccountProfileDto>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.Equal(userId, response.Data!.UserId);
            Assert.Contains("Staff", response.Data.Roles);
        }

        [Fact]
        public async Task GetProfile_WithoutToken_ReturnsUnauthorized()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context); // No user ID

            var result = await controller.GetProfile();

            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<AccountProfileDto>>(unauthorizedResult.Value);
            Assert.False(response.Success);
        }

        [Theory]
        [InlineData("Valid Name", "0901234567", true)]
        [InlineData("Valid Name", "090123456", false)] // Invalid format
        [InlineData("Valid Name", "0901234567", false, true)] // Phone exists
        public async Task UpdateProfile_Validations(string name, string phone, bool isValid, bool phoneExists = false)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, FullName = "Old", PhoneNumber = "0333333333" });
            
            if (phoneExists)
            {
                context.Users.Add(new User { UserId = Guid.NewGuid(), PhoneNumber = phone });
            }
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);
            var request = new UpdateProfileRequest { FullName = name, PhoneNumber = phone };

            var result = await controller.UpdateProfile(request);

            if (isValid)
            {
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
                var response = Assert.IsType<ApiResponse<AccountProfileDto>>(okResult.Value);
                Assert.True(response.Success);
                
                var user = await context.Users.FindAsync(userId);
                Assert.Equal(name, user!.FullName);
                Assert.Equal(phone, user.PhoneNumber);
            }
            else
            {
                var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
                var response = Assert.IsType<ApiResponse<AccountProfileDto>>(badRequest.Value);
                Assert.False(response.Success);
            }
        }

        [Fact]
        public async Task ChangePassword_ValidPasswords_ChangesSuccessfully()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword("OldPassword123!");
            context.Users.Add(new User { UserId = userId, PasswordHash = hashedPassword });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);
            var request = new ChangePasswordRequest { CurrentPassword = "OldPassword123!", NewPassword = "NewPassword123!" };

            var result = await controller.ChangePassword(request);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<bool>>(okResult.Value);
            Assert.True(response.Success);

            var user = await context.Users.FindAsync(userId);
            Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword123!", user!.PasswordHash));
        }

        [Fact]
        public async Task ChangePassword_WrongCurrentPassword_ReturnsBadRequest()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword("OldPassword123!");
            context.Users.Add(new User { UserId = userId, PasswordHash = hashedPassword });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);
            var request = new ChangePasswordRequest { CurrentPassword = "WrongPassword", NewPassword = "NewPassword123!" };

            var result = await controller.ChangePassword(request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<bool>>(badRequest.Value);
            Assert.False(response.Success);
            Assert.Contains("Mật khẩu hiện tại không đúng", response.Message);
        }

        [Fact]
        public async Task ChangePassword_SamePassword_ReturnsBadRequest()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword("Password123!");
            context.Users.Add(new User { UserId = userId, PasswordHash = hashedPassword });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);
            var request = new ChangePasswordRequest { CurrentPassword = "Password123!", NewPassword = "Password123!" };

            var result = await controller.ChangePassword(request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<bool>>(badRequest.Value);
            Assert.False(response.Success);
            Assert.Contains("Mật khẩu mới không được trùng với mật khẩu cũ", response.Message);
        }
    }
}
