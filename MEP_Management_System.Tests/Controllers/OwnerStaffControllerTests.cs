using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.OwnerController;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class OwnerStaffControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<ICurrentBranchAccessor> _mockBranchAccessor;

        public OwnerStaffControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            
            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
        }

        private OwnerStaffController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null, string role = "Owner")
        {
            var controller = new OwnerStaffController(context, _mockBranchAccessor.Object, new Mock<IConfiguration>().Object);

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

            if (branchId.HasValue)
            {
                _mockBranchAccessor.Setup(m => m.GetBranchId()).Returns(branchId.Value);
            }

            return controller;
        }

        [Fact]
        public async Task CreateStaff_ValidData_CreatesStaffInOwnersBranch()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            var branch = new Branch { BranchId = branchId, BranchName = "Chi nhánh Quận 1 (Owner quản lý)", IsActive = 1, ManagerUserId = ownerId };
            context.Branches.Add(branch);
            
            context.StaffAssignments.Add(new StaffAssignment { UserId = ownerId, BranchId = branchId });
            await context.SaveChangesAsync();

            var controller = CreateController(context, ownerId, branchId);

            var request = new CreateStaffWithPermissionRequest
            {
                Username = "sale.quan1",
                Password = "Password123!",
                FullName = "Lê Thị Thu",
                Email = "thu.le@mep.vn",
                PhoneNumber = "0912345678"
            };

            // Act
            var result = await controller.CreateStaff(request);

            // Assert
            var okResult = Assert.IsType<CreatedAtActionResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<OwnerStaffDto>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Equal("Tạo nhân viên thành công.", apiResponse.Message);
            Assert.Equal("Lê Thị Thu", apiResponse.Data.FullName);
            Assert.Equal("thu.le@mep.vn", apiResponse.Data.Email);
            
            // Check Database for exact constraints
            var dbUser = await context.Users.Include(u => u.StaffAssignments).FirstOrDefaultAsync(u => u.Username == "sale.quan1");
            Assert.NotNull(dbUser);
            Assert.Equal(1, dbUser.IsActive); // Should be active by default
            
            // Check that the staff was explicitly assigned to the Owner's branch
            Assert.Single(dbUser.StaffAssignments);
            Assert.Equal(branchId, dbUser.StaffAssignments.First().BranchId);
        }

        [Fact]
        public async Task CreateStaff_MissingUsername_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            context.StaffAssignments.Add(new StaffAssignment { UserId = ownerId, BranchId = branchId });
            await context.SaveChangesAsync();

            var controller = CreateController(context, ownerId, branchId);

            var request = new CreateStaffWithPermissionRequest
            {
                Username = "", // Empty Username
                Password = "Password123!",
                FullName = "Nguyễn Văn Không Tên",
                Email = "khongten@mep.vn"
            };

            // Act
            var result = await controller.CreateStaff(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Contains("Thông tin bắt buộc bị thiếu", apiResponse.Message);
        }

        [Fact]
        public async Task CreateStaff_DuplicateUsername_ReturnsBadRequestError()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var existingStaffId = Guid.NewGuid();
            
            // Existing staff in the DB
            var existingStaff = new User 
            { 
                UserId = existingStaffId, 
                Username = "thu.le", 
                Email = "thule.old@mep.vn",
                FullName = "Lê Thị Thu Cũ",
                PasswordHash = "hash"
            };
            context.Users.Add(existingStaff);
            context.StaffAssignments.Add(new StaffAssignment { UserId = existingStaffId, BranchId = branchId });
            context.StaffAssignments.Add(new StaffAssignment { UserId = ownerId, BranchId = branchId });
            await context.SaveChangesAsync();

            var controller = CreateController(context, ownerId, branchId);

            var request = new CreateStaffWithPermissionRequest
            {
                Username = "thu.le", // Attempting to use existing username
                Password = "Password123!",
                FullName = "Lê Thị Thu Mới",
                Email = "thule.new@mep.vn"
            };

            // Act
            var result = await controller.CreateStaff(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Contains("đã tồn tại trong chi nhánh này", apiResponse.Message);
        }
    }
}
