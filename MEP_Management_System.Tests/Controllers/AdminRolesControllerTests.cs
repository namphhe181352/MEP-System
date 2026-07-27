using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.AdminController;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class AdminRolesControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public AdminRolesControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private AdminRolesController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new AdminRolesController(context);

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
        public async Task GetRoles_ReturnsRoles()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            context.Roles.Add(new Role { RoleId = Guid.NewGuid(), RoleName = "Test Role", Description = "Test" });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetRoles();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var roles = Assert.IsAssignableFrom<IEnumerable<AdminRoleDto>>(okResult.Value);
            Assert.Single(roles);
            Assert.Equal("Test Role", roles.First().RoleName);
        }

        [Fact]
        public async Task GetPermissions_ReturnsPermissions()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            context.Permissions.Add(new Permission { PermissionId = Guid.NewGuid(), PermissionCode = "TEST_CODE", PermissionName = "Test" });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetPermissions();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var permissions = Assert.IsAssignableFrom<IEnumerable<AdminPermissionDto>>(okResult.Value);
            Assert.Single(permissions);
            Assert.Equal("TEST_CODE", permissions.First().PermissionCode);
        }

        [Fact]
        public async Task GetRolePermissionMatrix_ReturnsMatrix()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var role = new Role { RoleId = Guid.NewGuid(), RoleName = "Test Role" };
            var permission = new Permission { PermissionId = Guid.NewGuid(), PermissionCode = "TEST", PermissionName = "Test Perm" };
            role.Permissions.Add(permission);

            context.Roles.Add(role);
            context.Permissions.Add(permission);
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetRolePermissionMatrix();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task ConfigureRolePermissions_Valid_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var adminId = Guid.NewGuid();
            var controller = CreateController(context, adminId);
            
            context.Users.Add(new User { UserId = adminId, FullName = "Admin", Email = "admin@system.local", Username = "admin", PasswordHash = "hash" });

            var roleId = Guid.NewGuid();
            var role = new Role { RoleId = roleId, RoleName = "Staff" };
            var permissionId = Guid.NewGuid();
            var permission = new Permission { PermissionId = permissionId, PermissionCode = "TEST", PermissionName = "Test Perm" };
            
            context.Roles.Add(role);
            context.Permissions.Add(permission);
            await context.SaveChangesAsync();

            var request = new AdminRolePermissionUpdateDto
            {
                PermissionIds = new List<Guid> { permissionId }
            };

            // Act
            var result = await controller.ConfigureRolePermissions(roleId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);

            var dbRole = await context.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.RoleId == roleId);
            Assert.Single(dbRole!.Permissions);
            Assert.Equal(permissionId, dbRole.Permissions.First().PermissionId);
        }

        [Fact]
        public async Task ConfigureRolePermissions_Admin_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var roleId = Guid.NewGuid();
            var role = new Role { RoleId = roleId, RoleName = "Admin" }; // Editing Admin role
            var permissionId = Guid.NewGuid();
            var permission = new Permission { PermissionId = permissionId, PermissionCode = "TEST", PermissionName = "Test Perm" };
            
            context.Roles.Add(role);
            context.Permissions.Add(permission);
            await context.SaveChangesAsync();

            var request = new AdminRolePermissionUpdateDto
            {
                PermissionIds = new List<Guid> { permissionId }
            };

            // Act
            var result = await controller.ConfigureRolePermissions(roleId, request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
