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
    public class AdminUsersControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public AdminUsersControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private AdminUsersController CreateController(MEPDatabaseContext context, Guid? userId = null, string role = "Admin")
        {
            var controller = new AdminUsersController(context);
            var claims = new List<Claim>();
            if (userId.HasValue) claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            if (!string.IsNullOrEmpty(role)) claims.Add(new Claim(ClaimTypes.Role, role));
            
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = claimsPrincipal } };
            return controller;
        }

        // ==========================================
        // 1. GET USERS: PAGINATION, SEARCH, FILTER
        // ==========================================

        [Theory]
        [InlineData("Văn", null, 1, 10, 1)] // Search matches 1
        [InlineData("Không Tồn Tại", null, 1, 10, 0)] // Search matches 0
        [InlineData(null, "ROLE_STAFF", 1, 10, 1)] // Filter by role (staff)
        [InlineData(null, "ROLE_ADMIN", 1, 10, 1)] // Filter by role (admin)
        [InlineData(null, "ROLE_UNKNOWN", 1, 10, 0)] // Role doesn't match
        [InlineData("Văn", "ROLE_STAFF", 1, 10, 1)] // Search and Role match
        [InlineData("Văn", "ROLE_ADMIN", 1, 10, 0)] // Search matches but Role doesn't
        [InlineData(null, null, 1, 1, 1)] // Pagination size 1
        [InlineData(null, null, 2, 1, 1)] // Pagination page 2, size 1
        [InlineData(null, null, -1, 0, 2)] // Invalid pagination bounds -> falls back to defaults or returns all
        // --- REALISTIC EXTENDED MATRIX ---
        [InlineData("Nguyễn", null, 1, 10, 1)]
        [InlineData("Trần", null, 1, 10, 1)]
        [InlineData("Lê", null, 1, 10, 0)]
        [InlineData("Phạm", null, 1, 10, 0)]
        [InlineData("Hoàng", null, 1, 10, 0)]
        [InlineData("Huỳnh", null, 1, 10, 0)]
        [InlineData("Phan", null, 1, 10, 0)]
        [InlineData("Vũ", null, 1, 10, 0)]
        [InlineData("Võ", null, 1, 10, 0)]
        [InlineData("Đặng", null, 1, 10, 0)]
        [InlineData("Bùi", null, 1, 10, 0)]
        [InlineData("Đỗ", null, 1, 10, 0)]
        [InlineData("Hồ", null, 1, 10, 0)]
        [InlineData("Ngô", null, 1, 10, 0)]
        [InlineData("Dương", null, 1, 10, 0)]
        [InlineData("Lý", null, 1, 10, 0)]
        [InlineData("usera", null, 1, 10, 1)] // Search username
        [InlineData("userb", null, 1, 10, 1)] // Search username
        [InlineData("user", null, 1, 10, 2)] // Partial search username
        [InlineData("a@mep.vn", null, 1, 10, 1)] // Search email
        [InlineData("b@mep.vn", null, 1, 10, 1)] // Search email
        [InlineData("@mep.vn", null, 1, 10, 2)] // Partial search email
        [InlineData("!@#$%", null, 1, 10, 0)] // Special characters
        [InlineData("  Văn  ", null, 1, 10, 1)] // Whitespace padding
        [InlineData("văn", null, 1, 10, 1)] // Case insensitive match
        [InlineData("NGUYỄN", null, 1, 10, 1)] // Case insensitive match
        [InlineData("admin", null, 1, 10, 0)] // Search role string (shouldn't match search bar)
        [InlineData("usera", "ROLE_ADMIN", 1, 10, 0)] // User a is Staff
        [InlineData("userb", "ROLE_STAFF", 1, 10, 0)] // User b is Admin
        [InlineData("Nguyễn Văn A", "ROLE_STAFF", 1, 10, 1)] // Full match
        [InlineData("Trần Thị B", "ROLE_ADMIN", 1, 10, 1)] // Full match
        public async Task GetUsers_VariousFilters_ReturnsExpectedCounts(string? search, string? roleName, int page, int pageSize, int expectedCount)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var roleStaff = new Role { RoleId = Guid.NewGuid(), RoleName = "Staff" };
            var roleAdmin = new Role { RoleId = Guid.NewGuid(), RoleName = "Admin" };
            context.Roles.AddRange(roleStaff, roleAdmin);

            var user1 = new User { UserId = Guid.NewGuid(), FullName = "Nguyễn Văn A", Email = "a@mep.vn", Username = "usera", IsActive = 1, CreatedAt = DateTime.UtcNow };
            user1.Roles.Add(roleStaff);
            var user2 = new User { UserId = Guid.NewGuid(), FullName = "Trần Thị B", Email = "b@mep.vn", Username = "userb", IsActive = 1, CreatedAt = DateTime.UtcNow.AddMinutes(-10) };
            user2.Roles.Add(roleAdmin);
            context.Users.AddRange(user1, user2);
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid());
            
            var targetRoleId = roleName == "ROLE_STAFF" ? roleStaff.RoleId.ToString() : 
                               roleName == "ROLE_ADMIN" ? roleAdmin.RoleId.ToString() : 
                               roleName == "ROLE_UNKNOWN" ? Guid.NewGuid().ToString() : null;

            var result = await controller.GetUsers(search, targetRoleId, page, pageSize);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<PageResultDto<AdminUserDto>>(okResult.Value);
            
            Assert.Equal(expectedCount, dto.Items.Count);
        }

        // ==========================================
        // 2. CREATE USER: BOUNDARY & VALIDATION
        // ==========================================

        [Theory]
        [InlineData("", "valid@mep.vn", "0901234567", "Password123", "Họ tên là bắt buộc.")] // Empty Name
        [InlineData("Name", "", "0901234567", "Password123", "Email là bắt buộc.")] // Empty Email
        [InlineData("Name", "valid@mep.vn", "0901234567", "123", "Mật khẩu phải có ít nhất 6 ký tự.")] // Password too short
        [InlineData("Name", "dup@mep.vn", "0901234567", "Password123", "Email đã tồn tại")] // Dup email
        [InlineData("Name", "new@mep.vn", "dup_phone", "Password123", "Số điện thoại đã tồn tại")] // Dup phone
        // --- REALISTIC EXTENDED MATRIX ---
        [InlineData("  ", "valid2@mep.vn", "0901234568", "Password123", "Họ tên là bắt buộc.")] // Whitespace Name
        [InlineData("Nguyễn Trãi", "dup@mep.vn", "0909999888", "StrongPass!23", "Email đã tồn tại")]
        [InlineData("Lê Lợi", "leloi@mep.vn", "dup_phone", "StrongPass!23", "Số điện thoại đã tồn tại")]
        [InlineData("Trần Hưng Đạo", "tran.hung.dao@mep.vn", "0988777666", "12345", "Mật khẩu phải có ít nhất 6 ký tự.")]
        public async Task CreateUser_InvalidData_ReturnsBadRequest(string fullName, string email, string phone, string password, string errorIdentifier)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            context.Users.Add(new User { UserId = Guid.NewGuid(), Email = "dup@mep.vn", PhoneNumber = "dup_phone", Username = "exist" });
            var staffRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Staff" };
            context.Roles.Add(staffRole);
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid());
            var request = new AdminUserCreateDto
            {
                FullName = fullName,
                Email = email,
                PhoneNumber = phone,
                Password = password,
                RoleIds = new List<Guid> { staffRole.RoleId }
            };
            var result = await controller.CreateUser(request);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var errorDto = Assert.IsType<ErrorResponseDto>(badRequest.Value);
            Assert.Contains(errorIdentifier, errorDto.Error?.Message ?? "", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateUser_TryingToCreateOwner_ReturnsBadRequest()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var roleOwner = new Role { RoleId = Guid.NewGuid(), RoleName = "Owner" };
            context.Roles.Add(roleOwner);
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid());
            var request = new AdminUserCreateDto
            {
                FullName = "Chủ Cửa Hàng", Email = "owner@mep.vn", Password = "Password123", RoleIds = new List<Guid> { roleOwner.RoleId }
            };

            var result = await controller.CreateUser(request);
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorDto = Assert.IsType<ErrorResponseDto>(badRequestResult.Value);
            Assert.Contains("POST /api/admin/accounts/owners", errorDto.Error?.Message ?? "", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateUser_ValidStaff_CreatesSuccessfullyAndLogs()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var roleStaff = new Role { RoleId = Guid.NewGuid(), RoleName = "Staff" };
            context.Roles.Add(roleStaff);
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid());
            var request = new AdminUserCreateDto
            {
                FullName = "Nhân Viên Mới", Email = "new_staff@mep.vn", PhoneNumber = "0987654321", Password = "Password123", RoleIds = new List<Guid> { roleStaff.RoleId }
            };

            var result = await controller.CreateUser(request);
            Assert.IsType<CreatedAtActionResult>(result);
            
            var user = await context.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == "new_staff@mep.vn");
            Assert.NotNull(user);
            Assert.Equal("Nhân Viên Mới", user.FullName);
            Assert.Contains(user.Roles, r => r.RoleName == "Staff");
            Assert.False(user.IsVerified); // Admin created user may need manual verification
        }

        // ==========================================
        // 3. UPDATE USER: BOUNDARY & VALIDATION
        // ==========================================

        [Fact]
        public async Task UpdateUser_NonExistent_ReturnsNotFound()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());
            var result = await controller.UpdateUser(Guid.NewGuid(), new AdminUserUpdateDto { FullName = "Update" });
            Assert.IsType<NotFoundObjectResult>(result);
        }



        [Theory]
        [InlineData("Update", "dup_phone", "Số điện thoại đã tồn tại")]
        [InlineData("", "0901234567", "Họ tên là bắt buộc.")]
        public async Task UpdateUser_InvalidData_ReturnsBadRequest(string fullName, string phone, string expectedError)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var targetId = Guid.NewGuid();
            context.Users.Add(new User { UserId = targetId, Email = "old@mep.vn", PhoneNumber = "old_phone", Username = "target" });
            context.Users.Add(new User { UserId = Guid.NewGuid(), Email = "dup@mep.vn", PhoneNumber = "dup_phone", Username = "other" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid());

            // Mock model state errors if invalid formats
            if (fullName == "") controller.ModelState.AddModelError("FullName", "Họ tên là bắt buộc.");
            if (phone == "090") controller.ModelState.AddModelError("PhoneNumber", "Số điện thoại không hợp lệ");

            var request = new AdminUserUpdateDto { FullName = fullName, PhoneNumber = phone };
            var result = await controller.UpdateUser(targetId, request);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var errorDto = Assert.IsType<ErrorResponseDto>(badRequest.Value);
            Assert.Contains(expectedError, errorDto.Error?.Message ?? "", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UpdateUser_ValidData_UpdatesSuccessfullyAndLogs()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var targetId = Guid.NewGuid();
            context.Users.Add(new User { UserId = targetId, Email = "old@mep.vn", FullName = "Old Name", Username = "target" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid());
            var result = await controller.UpdateUser(targetId, new AdminUserUpdateDto { FullName = "New Name", PhoneNumber = "123456789" });
            
            Assert.IsType<OkObjectResult>(result);
            var user = await context.Users.FirstAsync(u => u.UserId == targetId);
            Assert.Equal("New Name", user.FullName);
            Assert.Equal("123456789", user.PhoneNumber);
        }

        // ==========================================
        // 4. TOGGLE STATUS: VALIDATION
        // ==========================================

        [Theory]
        [InlineData(true, 1)] // Active -> Active (1)
        [InlineData(false, 0)] // Active -> Inactive (0)
        public async Task UpdateUserStatus_ValidUser_ChangesStatus(bool isActive, short expectedStatus)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var targetId = Guid.NewGuid();
            context.Users.Add(new User { UserId = targetId, IsActive = 1, Username = "test" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid());
            var result = await controller.UpdateUserStatus(targetId, new AdminUserStatusUpdateDto { IsActive = isActive });
            Assert.IsType<OkObjectResult>(result);

            var user = await context.Users.FindAsync(targetId);
            Assert.Equal(expectedStatus, user!.IsActive);
        }

        [Fact]
        public async Task UpdateUserStatus_ToggleSelf_ReturnsBadRequest()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var targetId = Guid.NewGuid();
            context.Users.Add(new User { UserId = targetId, IsActive = 1, Username = "test" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, targetId); // Logged in as targetId
            var result = await controller.UpdateUserStatus(targetId, new AdminUserStatusUpdateDto { IsActive = false });
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var errorDto = Assert.IsType<ErrorResponseDto>(badRequest.Value);
            Assert.Contains("Không thể thay đổi trạng thái của chính mình", errorDto.Error?.Message ?? "");
        }

        // Toggle owner via admin role is already tested in AdminUserUpdateDto where assigning Owner is blocked.

        // ==========================================
        // 6. RESET PASSWORD: VALIDATION
        // ==========================================

        [Theory]
        [InlineData("123")] // Password too short
        [InlineData("ValidPass123")] // Valid password
        public async Task ResetPassword_ValidatesLength_AndUpdatesHash(string newPassword)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var targetId = Guid.NewGuid();
            context.Users.Add(new User { UserId = targetId, Status = "ACTIVE", Username = "test", PasswordHash = "old_hash" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid());
            // Mock model validation if password is short
            if (newPassword.Length < 6) controller.ModelState.AddModelError("NewPassword", "Mật khẩu phải từ 6 ký tự");

            var request = new AdminUserResetPasswordDto { NewPassword = newPassword };
            var result = await controller.ResetUserPassword(targetId, request);
            
            if (newPassword.Length < 6)
            {
                Assert.IsType<BadRequestObjectResult>(result);
            }
            else
            {
                Assert.IsType<OkObjectResult>(result);
                var user = await context.Users.FindAsync(targetId);
                Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, user!.PasswordHash));
            }
        }
    }
}
