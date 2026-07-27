using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.AdminController;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class AdminUsersControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public AdminUsersControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private AdminUsersController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new AdminUsersController(context);

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
        public async Task GetUsers_ReturnsPaginatedUsers()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var user = new User { UserId = Guid.NewGuid(), FullName = "Test User", Email = "test@test.com", Username = "test", PasswordHash = "hash" };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetUsers(null, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<PageResultDto<AdminUserDto>>(okResult.Value);
            Assert.Single(dto.Items);
            Assert.Equal("Test User", dto.Items.First().FullName);
        }

        [Fact]
        public async Task CreateUser_ValidData_ReturnsCreated()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var adminId = Guid.NewGuid();
            var controller = CreateController(context, adminId);
            
            context.Users.Add(new User { UserId = adminId, FullName = "Admin", Email = "admin@system.local", Username = "admin", PasswordHash = "hash" });
            var role = new Role { RoleId = Guid.NewGuid(), RoleName = "Staff" };
            context.Roles.Add(role);
            
            var branch = new Branch { BranchId = Guid.NewGuid(), BranchName = "Main Branch", IsActive = 1 };
            context.Branches.Add(branch);
            
            await context.SaveChangesAsync();

            var request = new AdminUserCreateDto
            {
                FullName = "New Staff",
                Email = "staff@test.com",
                Password = "Password123!",
                RoleIds = new List<Guid> { role.RoleId },
                BranchId = branch.BranchId
            };

            // Act
            var result = await controller.CreateUser(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var dbUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "staff@test.com");
            Assert.NotNull(dbUser);
            Assert.Equal("New Staff", dbUser.FullName);
        }

        [Fact]
        public async Task AssignUserRole_ValidData_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var adminId = Guid.NewGuid();
            var controller = CreateController(context, adminId);

            context.Users.Add(new User { UserId = adminId, FullName = "Admin", Email = "admin@system.local", Username = "admin", PasswordHash = "hash" });

            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, FullName = "Staff", Email = "staff@test.com", Username = "staff", PasswordHash = "hash" });
            
            var roleId = Guid.NewGuid();
            context.Roles.Add(new Role { RoleId = roleId, RoleName = "StaffRole" });
            await context.SaveChangesAsync();

            var request = new AdminUserRoleUpdateDto { RoleIds = new List<Guid> { roleId } };

            // Act
            var result = await controller.AssignUserRole(userId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dbUser = await context.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.UserId == userId);
            Assert.Single(dbUser!.Roles);
            Assert.Equal(roleId, dbUser.Roles.First().RoleId);
        }

        [Fact]
        public async Task UpdateUserStatus_ValidData_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var adminId = Guid.NewGuid();
            var controller = CreateController(context, adminId);

            context.Users.Add(new User { UserId = adminId, FullName = "Admin", Email = "admin@system.local", Username = "admin", PasswordHash = "hash" });

            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, FullName = "Staff", Email = "staff@test.com", Username = "staff", PasswordHash = "hash", IsActive = 1 });
            await context.SaveChangesAsync();

            var request = new AdminUserStatusUpdateDto { IsActive = false };

            // Act
            var result = await controller.UpdateUserStatus(userId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dbUser = await context.Users.FindAsync(userId);
            Assert.Equal((short)0, dbUser!.IsActive);
            Assert.Equal("SUSPENDED", dbUser.Status);
        }
    }
}
