using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class AccountSettingsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

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
        public async Task ChangePassword_CorrectCurrentPassword_UpdatesPassword()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId);

            // Use real BCrypt for the test to ensure matching logic works
            var currentPasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass123!");
            
            context.Users.Add(new User 
            { 
                UserId = userId, 
                Username = "test_user", 
                PasswordHash = currentPasswordHash 
            });
            await context.SaveChangesAsync();

            var request = new ChangePasswordDto
            {
                CurrentPassword = "OldPass123!",
                NewPassword = "NewStrongPass456!",
                ConfirmNewPassword = "NewStrongPass456!"
            };

            // Act
            var result = await controller.ChangePassword(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Equal("Đổi mật khẩu thành công.", apiResponse.Message);
            
            // Verify DB Hash is changed
            var dbUser = await context.Users.FindAsync(userId);
            Assert.True(BCrypt.Net.BCrypt.Verify("NewStrongPass456!", dbUser.PasswordHash));
            Assert.False(BCrypt.Net.BCrypt.Verify("OldPass123!", dbUser.PasswordHash));
        }
    }
}
