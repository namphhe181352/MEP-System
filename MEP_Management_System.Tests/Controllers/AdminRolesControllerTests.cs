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
        public async Task CreateRole_ValidData_CreatesRoleSuccessfully()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            var request = new AdminRoleCreateDto
            {
                RoleName = "InventoryManager",
                Description = "Quản lý toàn bộ kho hàng và điều chuyển"
            };

            // Act
            var result = await controller.CreateRole(request);

            // Assert
            var okResult = Assert.IsType<CreatedAtActionResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<AdminRoleDto>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Equal("InventoryManager", apiResponse.Data.RoleName);
            
            var dbRole = await context.Roles.FirstOrDefaultAsync(r => r.RoleName == "InventoryManager");
            Assert.NotNull(dbRole);
            Assert.Equal("Quản lý toàn bộ kho hàng và điều chuyển", dbRole.Description);
        }
    }
}
