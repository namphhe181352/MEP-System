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

namespace MEP_Management_System.UnitTests.Controllers.AdminController
{
    public class AdminRolesControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public AdminRolesControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private AdminRolesController CreateController(MEPDatabaseContext context, Guid? userId = null, string role = "Admin")
        {
            var controller = new AdminRolesController(context);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            }
            if (!string.IsNullOrEmpty(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
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
        public async Task GetRoles_FiltersOutSpecificStaffRoles_ReturnsFilteredRoles()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            context.Roles.AddRange(
                new Role { RoleId = Guid.NewGuid(), RoleName = "Admin" },
                new Role { RoleId = Guid.NewGuid(), RoleName = "Owner" },
                new Role { RoleId = Guid.NewGuid(), RoleName = "Staff" }, // Should be filtered out
                new Role { RoleId = Guid.NewGuid(), RoleName = "SalesStaff" }, // Should be filtered out
                new Role { RoleId = Guid.NewGuid(), RoleName = "InventoryStaff" } // Should be filtered out
            );
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.GetRoles();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var roles = Assert.IsAssignableFrom<IEnumerable<AdminRoleDto>>(okResult.Value);
            
            Assert.Equal(2, roles.Count()); // Only Admin and Owner
            Assert.Contains(roles, r => r.RoleName == "Admin");
            Assert.Contains(roles, r => r.RoleName == "Owner");
        }

        [Fact]
        public async Task GetPermissions_ReturnsListOfPermissions()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            context.Permissions.AddRange(
                new Permission { PermissionId = Guid.NewGuid(), PermissionCode = "CREATE_USER", PermissionName = "Tạo user" },
                new Permission { PermissionId = Guid.NewGuid(), PermissionCode = "DELETE_USER", PermissionName = "Xóa user" }
            );
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.GetPermissions();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var permissions = Assert.IsAssignableFrom<IEnumerable<AdminPermissionDto>>(okResult.Value);
            
            Assert.Equal(2, permissions.Count());
        }

        [Fact]
        public async Task ConfigureRolePermissions_ValidData_UpdatesPermissionsAndLogsAction()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var roleId = Guid.NewGuid();
            var role = new Role { RoleId = roleId, RoleName = "Manager" };
            context.Roles.Add(role);

            var perm1 = new Permission { PermissionId = Guid.NewGuid(), PermissionCode = "PERM1", PermissionName = "Perm 1" };
            var perm2 = new Permission { PermissionId = Guid.NewGuid(), PermissionCode = "PERM2", PermissionName = "Perm 2" };
            context.Permissions.AddRange(perm1, perm2);
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid());
            var dto = new AdminRolePermissionUpdateDto { PermissionIds = new List<Guid> { perm1.PermissionId, perm2.PermissionId } };

            // Act
            var result = await controller.ConfigureRolePermissions(roleId, dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var updatedRole = await context.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.RoleId == roleId);
            Assert.Equal(2, updatedRole!.Permissions.Count);
            
            var log = await context.SystemLogs.FirstOrDefaultAsync(l => l.Action == "UPDATE_ROLE_PERMISSIONS");
            Assert.NotNull(log); // Ensure it logged the action
        }

        [Fact]
        public async Task ConfigureRolePermissions_TryEditAdminRole_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var roleId = Guid.NewGuid();
            context.Roles.Add(new Role { RoleId = roleId, RoleName = "Admin" });
            
            var perm1 = new Permission { PermissionId = Guid.NewGuid(), PermissionCode = "PERM1", PermissionName = "Perm 1" };
            context.Permissions.Add(perm1);
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var dto = new AdminRolePermissionUpdateDto { PermissionIds = new List<Guid> { perm1.PermissionId } };

            // Act
            var result = await controller.ConfigureRolePermissions(roleId, dto);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = Assert.IsType<ErrorResponseDto>(badRequest.Value);
            Assert.Equal("Không thể chỉnh sửa quyền của Admin.", errorResponse.Error.Message);
        }
    }
}
