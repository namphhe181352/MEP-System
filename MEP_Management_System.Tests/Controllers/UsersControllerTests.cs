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
    public class UsersControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public UsersControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private UsersController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new UsersController(context);

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
        public async Task GetProfile_ValidUser_ReturnsUserProfile()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId);

            context.Users.Add(new User 
            { 
                UserId = userId, 
                FullName = "Nhân viên Bán hàng A", 
                Email = "nva@mep.vn", 
                Username = "nva", 
                PasswordHash = "hash" 
            });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetProfile();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<UserDto>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Equal("Nhân viên Bán hàng A", apiResponse.Data.FullName);
            Assert.Equal("nva@mep.vn", apiResponse.Data.Email);
        }
    }
}
