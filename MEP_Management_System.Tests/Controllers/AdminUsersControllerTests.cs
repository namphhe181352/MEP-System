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
                claims.Add(new Claim(ClaimTypes.Role, "Admin"));
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
        // TEST GET /api/admin/users
        // ==========================================
        [Fact]
        public async Task GetUsers_WithPaginationAndSearch_ReturnsFilteredRealData()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var adminId = Guid.NewGuid();
            var controller = CreateController(context, adminId);
            
            var staffRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Staff" };
            context.Roles.Add(staffRole);

            var user1 = new User { UserId = Guid.NewGuid(), FullName = "Nguyễn Văn An", Email = "an.nguyen@mep.vn", Username = "an.nguyen", PasswordHash = "hash1", IsActive = 1, CreatedAt = DateTime.UtcNow.AddDays(-5) };
            user1.Roles.Add(staffRole);
            var user2 = new User { UserId = Guid.NewGuid(), FullName = "Trần Thị Bích", Email = "bich.tran@mep.vn", Username = "bich.tran", PasswordHash = "hash2", IsActive = 1, CreatedAt = DateTime.UtcNow.AddDays(-2) };
            user2.Roles.Add(staffRole);
            var user3 = new User { UserId = Guid.NewGuid(), FullName = "Lê Hoàng Phúc", Email = "phuc.le@system.local", Username = "phuc.le", PasswordHash = "hash3", IsActive = 0, CreatedAt = DateTime.UtcNow.AddDays(-1) };
            
            context.Users.AddRange(user1, user2, user3);
            await context.SaveChangesAsync();

            // Act - Search for "Bích"
            var result = await controller.GetUsers(search: "Bích", roleId: null, page: 1, pageSize: 10);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<PageResultDto<AdminUserDto>>(okResult.Value);
            
            Assert.Single(dto.Items);
            Assert.Equal("Trần Thị Bích", dto.Items.First().FullName);
            Assert.Equal("bich.tran@mep.vn", dto.Items.First().Email);
            Assert.Equal(1, dto.TotalCount);
        }

        // ==========================================
        // TEST POST /api/admin/users
        // ==========================================
        [Fact]
        public async Task CreateUser_ValidStaffData_CreatesSuccessfullyAndAssignsBranch()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var adminId = Guid.NewGuid();
            var controller = CreateController(context, adminId);
            
            var staffRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Staff", Description = "Nhân viên bán hàng" };
            context.Roles.Add(staffRole);
            
            var branch = new Branch { BranchId = Guid.NewGuid(), BranchName = "MEP Store Q1 - TP.HCM", IsActive = 1 };
            context.Branches.Add(branch);
            await context.SaveChangesAsync();

            var request = new AdminUserCreateDto
            {
                FullName = "Phạm Lê Gia Bảo",
                Email = "bao.pham@mep.vn",
                PhoneNumber = "0987654321",
                Password = "SecurePassword123!",
                RoleIds = new List<Guid> { staffRole.RoleId },
                BranchId = branch.BranchId
            };

            // Act
            var result = await controller.CreateUser(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var dbUser = await context.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == "bao.pham@mep.vn");
            
            Assert.NotNull(dbUser);
            Assert.Equal("Phạm Lê Gia Bảo", dbUser.FullName);
            Assert.Equal("bao.pham@mep.vn", dbUser.Username); // Username from Email
            Assert.Contains(dbUser.Roles, r => r.RoleName == "Staff");
            Assert.Equal(branch.BranchId, dbUser.DefaultBranchId);
            Assert.Equal(1, dbUser.IsActive);
            
            // Check assignment
            var assignment = await context.StaffAssignments.FirstOrDefaultAsync(sa => sa.UserId == dbUser.UserId);
            Assert.NotNull(assignment);
            Assert.Equal(branch.BranchId, assignment.BranchId);
        }

        [Fact]
        public async Task CreateUser_FailsWhenTryingToCreateOwner_ThroughAdminUsersEndpoint()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());
            
            var ownerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Owner" };
            context.Roles.Add(ownerRole);
            await context.SaveChangesAsync();

            var request = new AdminUserCreateDto
            {
                FullName = "Chủ Cửa Hàng",
                Email = "owner.new@mep.vn",
                Password = "Password123!",
                RoleIds = new List<Guid> { ownerRole.RoleId }
            };

            // Act
            var result = await controller.CreateUser(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorDetail = GetErrorDetailMessage(badRequestResult.Value);
            Assert.Contains("dùng endpoint POST /api/admin/accounts/owners", errorDetail);
        }

        [Fact]
        public async Task CreateUser_MissingRequiredFields_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());
            
            var request = new AdminUserCreateDto
            {
                FullName = "", // Missing Name
                Email = "invalid",
                Password = "123" // Too short
            };

            // Act
            var result = await controller.CreateUser(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorDetail = GetErrorDetailMessage(badRequestResult.Value);
            // It will fail at the first validation (Email or FullName)
            Assert.NotNull(errorDetail); 
        }

        // ==========================================
        // TEST PUT /api/admin/users/{id}/roles
        // ==========================================
        [Fact]
        public async Task AssignUserRole_CannotAssignAdminRole()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());
            
            var user = new User { UserId = Guid.NewGuid(), FullName = "Nhân viên thường", Email = "staff@mep.vn", Username = "staff", PasswordHash = "hash" };
            var adminRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Admin" };
            
            context.Users.Add(user);
            context.Roles.Add(adminRole);
            await context.SaveChangesAsync();

            var request = new AdminUserRoleUpdateDto { RoleIds = new List<Guid> { adminRole.RoleId } };

            // Act
            var result = await controller.AssignUserRole(user.UserId, request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorDetail = GetErrorDetailMessage(badRequestResult.Value);
            Assert.Equal("Không thể gán vai trò Admin.", errorDetail);
        }

        // ==========================================
        // TEST PATCH /api/admin/users/{id}/status
        // ==========================================
        [Fact]
        public async Task UpdateUserStatus_DeactivateUser_Success()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var adminId = Guid.NewGuid();
            var controller = CreateController(context, adminId);
            
            var staffId = Guid.NewGuid();
            var user = new User { UserId = staffId, FullName = "Nhân viên vi phạm", Email = "badstaff@mep.vn", Username = "badstaff", PasswordHash = "hash", IsActive = 1, Status = "ACTIVE" };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new AdminUserStatusUpdateDto { IsActive = false };

            // Act
            var result = await controller.UpdateUserStatus(staffId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dbUser = await context.Users.FindAsync(staffId);
            Assert.Equal((short)0, dbUser.IsActive);
            Assert.Equal("SUSPENDED", dbUser.Status);
        }

        [Fact]
        public async Task UpdateUserStatus_AdminCannotDeactivateThemselves()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var adminId = Guid.NewGuid();
            var controller = CreateController(context, adminId);
            
            var adminUser = new User { UserId = adminId, FullName = "Admin User", Email = "admin@mep.vn", Username = "admin", PasswordHash = "hash", IsActive = 1, Status = "ACTIVE" };
            context.Users.Add(adminUser);
            await context.SaveChangesAsync();

            var request = new AdminUserStatusUpdateDto { IsActive = false };

            // Act
            var result = await controller.UpdateUserStatus(adminId, request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorDetail = GetErrorDetailMessage(badRequestResult.Value);
            Assert.Equal("Không thể thay đổi trạng thái của chính mình.", errorDetail);
        }

        // ==========================================
        // Helper Method
        // ==========================================
        private string GetErrorDetailMessage(object value)
        {
            var errorProp = value.GetType().GetProperty("Error")?.GetValue(value, null);
            if (errorProp == null) return null;
            return errorProp.GetType().GetProperty("Message")?.GetValue(errorProp, null) as string;
        }
    }
}
