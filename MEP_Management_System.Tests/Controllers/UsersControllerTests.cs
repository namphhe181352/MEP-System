using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers;
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
        public async Task ChangeDefaultBranch_UserNotFound_ReturnsNotFound()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId);

            // Act
            var result = await controller.ChangeDefaultBranch(Guid.NewGuid());

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Không tìm thấy người dùng", notFoundResult.Value!.ToString());
        }

        [Fact]
        public async Task ChangeDefaultBranch_NoAccess_ReturnsForbidden()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, userId);

            context.Users.Add(new User { UserId = userId, FullName = "Test User", Email = "test@test.com", Username = "test", PasswordHash = "hash" });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.ChangeDefaultBranch(branchId);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, statusCodeResult.StatusCode);
            Assert.Contains("You don't have access to this branch", statusCodeResult.Value!.ToString());
        }

        [Fact]
        public async Task ChangeDefaultBranch_Valid_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, userId);

            context.Users.Add(new User { UserId = userId, FullName = "Test User", Email = "test@test.com", Username = "test", PasswordHash = "hash" });
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Test Branch" });
            context.StaffAssignments.Add(new StaffAssignment { AssignmentId = Guid.NewGuid(), UserId = userId, BranchId = branchId });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.ChangeDefaultBranch(branchId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Default branch changed", okResult.Value!.ToString());

            var updatedUser = await context.Users.FindAsync(userId);
            Assert.Equal(branchId, updatedUser!.DefaultBranchId);
        }
    }
}
