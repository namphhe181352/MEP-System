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
using static MEP_Management_System.Controllers.OwnerAccountsController;

namespace MEP_Management_System.UnitTests.Controllers.AdminController
{
    public class OwnerAccountsControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public OwnerAccountsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private OwnerAccountsController CreateController(MEPDatabaseContext context, Guid? userId = null, string role = "Admin")
        {
            var controller = new OwnerAccountsController(context);
            var claims = new List<Claim>();
            if (userId.HasValue) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            if (!string.IsNullOrEmpty(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim("role", role));
            }
            var identity = new ClaimsIdentity(claims, "TestAuthType", ClaimTypes.Name, ClaimTypes.Role);
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
            return controller;
        }

        // ==========================================
        // 1. GET OWNERS: SEARCH & FILTER
        // ==========================================

        [Theory]
        [InlineData(null, null, 1, 10, 2)] // All
        [InlineData("Nguyen", null, 1, 10, 1)] // Search
        [InlineData(null, 1, 1, 10, 1)] // Status Active
        [InlineData(null, 0, 1, 10, 1)] // Status Inactive
        [InlineData("Nguyen", 1, 1, 10, 1)] // Search + Status
        [InlineData("Nguyen", 0, 1, 10, 0)] // Search + Status mismtach
        public async Task GetOwners_FiltersProperly_ReturnsExpectedCount(string? search, int? status, int page, int pageSize, int expectedCount)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var roleOwner = new Role { RoleId = Guid.NewGuid(), RoleName = "Owner" };
            var roleStaff = new Role { RoleId = Guid.NewGuid(), RoleName = "Staff" };
            context.Roles.AddRange(roleOwner, roleStaff);

            var owner1 = new User { UserId = Guid.NewGuid(), FullName = "Nguyen Van A", Username = "owner1", IsActive = 1, CreatedAt = DateTime.UtcNow };
            owner1.Roles.Add(roleOwner);
            var owner2 = new User { UserId = Guid.NewGuid(), FullName = "Tran Van B", Username = "owner2", IsActive = 0, CreatedAt = DateTime.UtcNow.AddMinutes(-10) };
            owner2.Roles.Add(roleOwner);
            var staff1 = new User { UserId = Guid.NewGuid(), FullName = "Nguyen Van C", Username = "staff1", IsActive = 1 };
            staff1.Roles.Add(roleStaff);
            
            context.Users.AddRange(owner1, owner2, staff1);
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.GetOwners(search, status, page, pageSize);
            
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<PageResultDto<StaffAccountDto>>(okResult.Value);
            Assert.Equal(expectedCount, dto.Items.Count);
        }

        // ==========================================
        // 2. CREATE OWNER: VALIDATION & BUSINESS LOGIC
        // ==========================================

        [Theory]
        [InlineData("valid_username", "valid@email.com", "", "Tên chi nhánh là bắt buộc")] // Missing Branch Name
        [InlineData("dup_user", "new@email.com", "Branch 1", "Tên đăng nhập hoặc Email đã tồn tại")] // Duplicate username
        [InlineData("new_user", "dup@email.com", "Branch 1", "Tên đăng nhập hoặc Email đã tồn tại")] // Duplicate email
        public async Task CreateOwner_InvalidData_ReturnsBadRequest(string username, string email, string branchName, string expectedError)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var roleOwner = new Role { RoleId = Guid.NewGuid(), RoleName = "Owner" };
            context.Roles.Add(roleOwner);
            context.Users.Add(new User { UserId = Guid.NewGuid(), Username = "dup_user", Email = "dup@email.com" });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var request = new CreateStaffRequest { Username = username, Email = email, BranchName = branchName, Password = "Pass123", FullName = "Test" };
            
            var result = await controller.CreateOwner(request);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains(expectedError, badRequest.Value?.ToString() ?? "");
        }

        [Fact]
        public async Task CreateOwner_ValidData_CreatesUserAndDefaultBranch()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var roleOwner = new Role { RoleId = Guid.NewGuid(), RoleName = "Owner" };
            context.Roles.Add(roleOwner);
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var request = new CreateStaffRequest { Username = "newowner", Email = "newowner@mep.vn", FullName = "New Owner", Password = "Password123!", BranchName = "Chi nhánh Quận 1" };
            
            var result = await controller.CreateOwner(request);
            Assert.IsType<CreatedAtActionResult>(result);
            
            var user = await context.Users.Include(u => u.StaffAssignments).FirstAsync(u => u.Username == "newowner");
            Assert.True(user.IsVerified);
            Assert.Single(user.StaffAssignments);
            
            var branch = await context.Branches.FindAsync(user.StaffAssignments.First().BranchId);
            Assert.Equal("Chi nhánh Quận 1", branch!.BranchName);
        }

        // ==========================================
        // 3. UPDATE OWNER: VALIDATION & DUPLICATES
        // ==========================================

        [Theory]
        [InlineData("dup@email.com", "Email đã tồn tại trong hệ thống")]
        [InlineData("valid@email.com", null)] // Expected Ok
        public async Task UpdateOwner_ValidationChecks_ReturnsAppropriateResult(string newEmail, string? expectedError)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var targetId = Guid.NewGuid();
            context.Users.Add(new User { UserId = targetId, Email = "old@email.com", Username = "target" });
            context.Users.Add(new User { UserId = Guid.NewGuid(), Email = "dup@email.com", Username = "other" });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.UpdateOwner(targetId, new UpdateStaffRequest { Email = newEmail, FullName = "Test" });
            
            if (expectedError != null)
            {
                var badRequest = Assert.IsType<BadRequestObjectResult>(result);
                Assert.Contains(expectedError, badRequest.Value?.ToString() ?? "");
            }
            else
            {
                Assert.IsType<OkObjectResult>(result);
            }
        }

        // ==========================================
        // 4. TOGGLE STATUS: SELF-MUTATION PREVENTION
        // ==========================================

        [Fact]
        public async Task ToggleStatus_OnSelf_ReturnsBadRequest()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, IsActive = 1, Username = "test" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);
            var result = await controller.ToggleOwnerStatus(userId);
            
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Không thể thay đổi trạng thái của chính mình", badRequest.Value?.ToString() ?? "");
        }

        // ==========================================
        // 5. DELETE OWNER: SELF-MUTATION PREVENTION
        // ==========================================

        [Fact]
        public async Task DeleteOwner_OnSelf_ReturnsBadRequest()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, IsActive = 1, Username = "test" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);
            var result = await controller.DeleteOwner(userId);
            
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Không thể xóa chính mình", badRequest.Value?.ToString() ?? "");
        }
    }
}
