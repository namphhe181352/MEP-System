using System;
using System.Collections.Generic;
using System.Linq;
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
using BCrypt.Net;

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
        public async Task GetProfile_ReturnsProfile()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId);

            context.Users.Add(new User 
            { 
                UserId = userId, 
                FullName = "Test User", 
                Email = "test@test.com", 
                Username = "test", 
                PasswordHash = "hash",
                Status = "ACTIVE"
            });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetProfile();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<AccountProfileDto>>(okResult.Value);
            Assert.True(response.Success);
            Assert.Equal("Test User", response.Data!.FullName);
        }

        [Fact]
        public async Task UpdateProfile_ValidData_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId);

            context.Users.Add(new User 
            { 
                UserId = userId, 
                FullName = "Old Name", 
                Email = "test@test.com", 
                Username = "test", 
                PasswordHash = "hash",
                Status = "ACTIVE"
            });
            await context.SaveChangesAsync();

            var request = new UpdateProfileRequest { FullName = "New Name", PhoneNumber = "1234567890" };

            // Act
            var result = await controller.UpdateProfile(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<AccountProfileDto>>(okResult.Value);
            Assert.True(response.Success);
            Assert.Equal("New Name", response.Data!.FullName);
            Assert.Equal("1234567890", response.Data.PhoneNumber);
        }

        [Fact]
        public async Task ChangePassword_ValidData_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId);

            var oldPasswordHash = BCrypt.Net.BCrypt.HashPassword("oldpassword");

            context.Users.Add(new User 
            { 
                UserId = userId, 
                FullName = "Test User", 
                Email = "test@test.com", 
                Username = "test", 
                PasswordHash = oldPasswordHash,
                Status = "ACTIVE"
            });
            await context.SaveChangesAsync();

            var request = new ChangePasswordRequest { CurrentPassword = "oldpassword", NewPassword = "newpassword" };

            // Act
            var result = await controller.ChangePassword(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<ApiResponse<bool>>(okResult.Value);
            Assert.True(response.Success);

            var dbUser = await context.Users.FindAsync(userId);
            Assert.True(BCrypt.Net.BCrypt.Verify("newpassword", dbUser!.PasswordHash));
        }
    }
}
