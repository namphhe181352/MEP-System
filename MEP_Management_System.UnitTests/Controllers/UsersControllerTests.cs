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

namespace MEP_Management_System.UnitTests.Controllers
{
    public class UsersControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public UsersControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private UsersController CreateController(MEPDatabaseContext context, Guid? userId = null, string[]? roles = null)
        {
            var controller = new UsersController(context);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            }
            if (roles != null)
            {
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
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
        public async Task ChangeDefaultBranch_WithAccess_ChangesSuccessfully()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId });
            context.StaffAssignments.Add(new StaffAssignment { UserId = userId, BranchId = branchId });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);
            var result = await controller.ChangeDefaultBranch(branchId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var user = await context.Users.FindAsync(userId);
            Assert.Equal(branchId, user!.DefaultBranchId);
        }

        [Fact]
        public async Task ChangeDefaultBranch_WithoutAccess_ReturnsForbidden()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId });
            // No StaffAssignment
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);
            var result = await controller.ChangeDefaultBranch(branchId);

            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, objectResult.StatusCode);
        }

        [Fact]
        public async Task GetSystemNotifications_ReturnsRelevantNotifications()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId });
            
            // Add some notifications
            context.SystemNotifications.Add(new SystemNotification 
            { 
                NotificationId = Guid.NewGuid(), 
                Status = "SENT", 
                Target = "ALL", 
                SentAt = DateTime.UtcNow,
                Title = "Test 1",
                Content = "Content 1"
            });
            context.SystemNotifications.Add(new SystemNotification 
            { 
                NotificationId = Guid.NewGuid(), 
                Status = "SENT", 
                Target = "OWNER", 
                SentAt = DateTime.UtcNow,
                Title = "Test 2",
                Content = "Content 2"
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, new[] { "STAFF" }); // user is staff
            
            var result = await controller.GetSystemNotifications(7);

            var okResult = Assert.IsType<OkObjectResult>(result);
            // Staff should only see "ALL" because they are not "OWNER"
            var value = okResult.Value;
            var dataProperty = value?.GetType().GetProperty("data")?.GetValue(value, null) as IEnumerable<object>;
            
            Assert.NotNull(dataProperty);
            Assert.Single(dataProperty); // Only the "ALL" notification
        }
    }
}
