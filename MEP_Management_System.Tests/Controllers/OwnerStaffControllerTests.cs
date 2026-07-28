using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.OwnerController;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
        public async Task CreateStaff_ValidData_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Main Branch", IsActive = 1 });
            context.StaffAssignments.Add(new StaffAssignment { UserId = ownerId, BranchId = branchId });
            await context.SaveChangesAsync();

            var controller = CreateController(context, ownerId, branchId);

            var request = new CreateStaffWithPermissionRequest
            {
                Username = "newstaff",
                Password = "Password123!",
                FullName = "New Staff",
                Email = "staff@test.com",
                PhoneNumber = "0901234567"
            };

            // Act
            var result = await controller.CreateStaff(request);

            // Assert
            var okResult = Assert.IsType<CreatedAtActionResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<OwnerStaffDto>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Equal("Tạo nhân viên thành công.", apiResponse.Message);
            
            var dbUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "newstaff");
            Assert.NotNull(dbUser);
        }

        [Fact]
        public async Task CreateStaff_MissingFields_ReturnsBadRequest()
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
                Username = "", // Missing
                Password = "Password123!",
                FullName = "New Staff",
                Email = "staff@test.com"
            };

            // Act
            var result = await controller.CreateStaff(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("Thông tin bắt buộc bị thiếu.", apiResponse.Message);
        }

        [Fact]
        public async Task CreateStaff_DuplicateUsernameInBranch_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var existingStaffId = Guid.NewGuid();
            
            context.Users.Add(new User { 
                UserId = existingStaffId, 
                Username = "existingstaff", 
                Email = "old@test.com",
                FullName = "Old Staff",
                PasswordHash = "hash"
            });
            context.StaffAssignments.Add(new StaffAssignment { UserId = existingStaffId, BranchId = branchId });
            context.StaffAssignments.Add(new StaffAssignment { UserId = ownerId, BranchId = branchId });
            await context.SaveChangesAsync();

            var controller = CreateController(context, ownerId, branchId);

            var request = new CreateStaffWithPermissionRequest
            {
                Username = "existingstaff", // Duplicate
                Password = "Password123!",
                FullName = "New Staff",
                Email = "staff@test.com"
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
