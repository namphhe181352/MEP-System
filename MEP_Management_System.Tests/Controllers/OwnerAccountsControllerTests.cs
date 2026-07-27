using System;
using System.Collections.Generic;
using System.Linq;
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
    public class OwnerAccountsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public OwnerAccountsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private OwnerAccountsController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new OwnerAccountsController(context);

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
        public async Task GetOwners_ReturnsPaginatedOwners()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var role = new Role { RoleId = Guid.NewGuid(), RoleName = "Owner" };
            var user = new User { UserId = Guid.NewGuid(), FullName = "Owner User", Email = "owner@test.com", Username = "owner1", PasswordHash = "hash" };
            user.Roles.Add(role);
            
            context.Roles.Add(role);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetOwners(search: null, status: null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<PageResultDto<StaffAccountDto>>(okResult.Value);
            Assert.Single(dto.Items);
            Assert.Equal("Owner User", dto.Items.First().FullName);
        }

        [Fact]
        public async Task CreateOwner_ValidData_ReturnsCreated()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            context.Roles.Add(new Role { RoleId = Guid.NewGuid(), RoleName = "Owner" });
            await context.SaveChangesAsync();

            var request = new OwnerAccountsController.CreateStaffRequest
            {
                Username = "newowner",
                Email = "owner@system.local",
                FullName = "New Owner",
                Password = "Password123",
                Role = "Owner"
            };

            // Act
            var result = await controller.CreateOwner(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var dto = Assert.IsType<IdResponseDto>(createdResult.Value);
            Assert.NotEqual(Guid.Empty, dto.Id);

            var dbUser = await context.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.UserId == dto.Id);
            Assert.NotNull(dbUser);
            Assert.Contains(dbUser.Roles, r => r.RoleName == "Owner");
        }

        [Fact]
        public async Task UpdateStatus_ValidStatus_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);
            var userId = Guid.NewGuid();

            context.Users.Add(new User { UserId = userId, IsActive = 1, Status = "ACTIVE", FullName = "Test User", Email = "test@test.com", Username = "testuser", PasswordHash = "hash" });
            await context.SaveChangesAsync();

            var request = new OwnerAccountsController.UpdateStatusRequest { Status = "suspended" };

            // Act
            var result = await controller.UpdateStatus(userId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var dbUser = await context.Users.FindAsync(userId);
            Assert.Equal((short)0, dbUser.IsActive);
            Assert.Equal("SUSPENDED", dbUser.Status);
        }

        [Fact]
        public async Task BanStaff_ValidId_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var adminId = Guid.NewGuid();
            var controller = CreateController(context, adminId);
            var userId = Guid.NewGuid();

            context.Users.Add(new User { UserId = userId, FullName = "Bad User", Email = "bad@test.com", Username = "baduser", PasswordHash = "hash", IsActive = 1 });
            await context.SaveChangesAsync();

            var request = new OwnerAccountsController.BanRequest { Reason = "Violation" };

            // Act
            var result = await controller.BanStaff(userId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);

            var dbUser = await context.Users.FindAsync(userId);
            Assert.Equal((short)0, dbUser.IsActive);
            Assert.Equal("BANNED", dbUser.Status);
        }
    }
}
