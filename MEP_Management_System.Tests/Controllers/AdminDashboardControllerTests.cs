using System;
using System.Collections.Generic;
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
    public class AdminDashboardControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public AdminDashboardControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private AdminDashboardController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new AdminDashboardController(context);

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

        // ==========================================
        // TEST GET /api/admin/dashboard
        // ==========================================
        [Fact]
        public async Task GetOverview_CalculatesGlobalMetricsAccurately()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            // Add 2 active branches, 1 inactive branch
            context.Branches.Add(new Branch { BranchId = Guid.NewGuid(), BranchName = "Chi nhánh Quận 1", IsActive = 1 });
            context.Branches.Add(new Branch { BranchId = Guid.NewGuid(), BranchName = "Chi nhánh Quận 3", IsActive = 1 });
            context.Branches.Add(new Branch { BranchId = Guid.NewGuid(), BranchName = "Chi nhánh Cũ", IsActive = 0 }); // Inactive

            // Add 2 active users, 1 inactive user
            context.Users.Add(new User { UserId = Guid.NewGuid(), FullName = "Nguyễn Văn A", IsActive = 1, Username = "A", Email = "A@mep.vn", PasswordHash = "hash" });
            context.Users.Add(new User { UserId = Guid.NewGuid(), FullName = "Trần Thị B", IsActive = 1, Username = "B", Email = "B@mep.vn", PasswordHash = "hash" });
            context.Users.Add(new User { UserId = Guid.NewGuid(), FullName = "Lê Văn C", IsActive = 0, Username = "C", Email = "C@mep.vn", PasswordHash = "hash" }); // Inactive

            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetOverview();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            // Assuming it returns an AdminDashboardOverviewDto
            // Wait, looking at current structure, the response type might be object or specific DTO. 
            // We use dynamic or specific class if defined. Usually it's an anonymous object or a DTO.
            // Let's assert based on expected fields if it returns Ok(new { ... })
            var overview = okResult.Value;
            Assert.NotNull(overview);
            
            var type = overview.GetType();
            var totalBranches = (int)type.GetProperty("TotalActiveBranches")?.GetValue(overview, null);
            var totalUsers = (int)type.GetProperty("TotalActiveUsers")?.GetValue(overview, null);

            Assert.Equal(2, totalBranches); // Only 2 active
            Assert.Equal(2, totalUsers); // Only 2 active
        }
    }
}
