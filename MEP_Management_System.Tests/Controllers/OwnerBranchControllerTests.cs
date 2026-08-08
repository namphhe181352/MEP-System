using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.OwnerController;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class OwnerBranchControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public OwnerBranchControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            
            using var context = new MEPDatabaseContext(_dbContextOptions);
            context.Database.EnsureCreated();
        }

        private OwnerBranchController CreateController(MEPDatabaseContext context, Guid? userId = null, string role = "Owner")
        {
            var controller = new OwnerBranchController(context);

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

        // ==========================================
        // TEST GET /api/owner/branch
        // ==========================================
        [Fact]
        public async Task GetBranch_OwnerWithActiveBranch_ReturnsBranchDetails()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            var branch = new Branch 
            { 
                BranchId = branchId, 
                BranchName = "Chi nhánh Quận 3", 
                BranchCode = "B-Q3", 
                Phone = "0901234567", 
                Address = "Pasteur, Q3",
                IsActive = 1,
                ManagerUserId = ownerId
            };
            context.Branches.Add(branch);
            context.StaffAssignments.Add(new StaffAssignment { UserId = ownerId, BranchId = branchId });
            await context.SaveChangesAsync();

            var controller = CreateController(context, ownerId);

            // Act
            var result = await controller.GetBranch();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<List<OwnerBranchDto>>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Single(apiResponse.Data);
            Assert.Equal("Chi nhánh Quận 3", apiResponse.Data.First().BranchName);
            Assert.Equal("B-Q3", apiResponse.Data.First().BranchCode);
        }

        [Fact]
        public async Task GetBranch_OwnerWithoutActiveBranches_ReturnsNotFound()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            
            // Branch exists but is not active (e.g. IsActive = 0)
            var branch = new Branch 
            { 
                BranchId = Guid.NewGuid(), 
                BranchName = "Chi nhánh Đã Đóng Cửa", 
                IsActive = 0, // Inactive
                ManagerUserId = ownerId
            };
            context.Branches.Add(branch);
            context.StaffAssignments.Add(new StaffAssignment { UserId = ownerId, BranchId = branch.BranchId });
            await context.SaveChangesAsync();

            var controller = CreateController(context, ownerId);

            // Act
            var result = await controller.GetBranch();

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("Không tìm thấy chi nhánh nào.", apiResponse.Message);
        }

        // ==========================================
        // TEST POST /api/owner/branch
        // ==========================================
        [Fact]
        public async Task CreateBranch_ValidData_CreatesBranchSuccessfully()
        {
            // Arrange 
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            var controller = CreateController(context, ownerId);

            var request = new CreateOwnerBranchRequest
            {
                BranchName = "Chi nhánh Quận 7 (Mới)",
                BranchCode = "B-Q7",
                Phone = "0987654321",
                Address = "Nguyễn Hữu Thọ, Q7"
            };

            // Act
            var result = await controller.CreateBranch(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<OwnerBranchDto>>(createdResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Equal("Chi nhánh Quận 7 (Mới)", apiResponse.Data.BranchName);
            
            // Check Database 
            var dbBranch = await context.Branches.FirstOrDefaultAsync(b => b.BranchCode == "B-Q7");
            Assert.NotNull(dbBranch);
            Assert.Equal(ownerId, dbBranch.ManagerUserId);
            
            // Ensure owner is automatically assigned to the new branch
            var assignment = await context.StaffAssignments.FirstOrDefaultAsync(a => a.UserId == ownerId && a.BranchId == dbBranch.BranchId);
            Assert.NotNull(assignment);
        }

        [Fact]
        public async Task CreateBranch_DuplicateBranchCode_ReturnsBadRequest()
        {
            // Arrange 
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            
            // Pre-insert a branch with code B-Q7
            context.Branches.Add(new Branch { BranchId = Guid.NewGuid(), BranchName = "Old Branch", BranchCode = "B-Q7", IsActive = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, ownerId);

            var request = new CreateOwnerBranchRequest
            {
                BranchName = "New Branch Attempt",
                BranchCode = "B-Q7" // Duplicate code
            };

            // Act
            var result = await controller.CreateBranch(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("Mã chi nhánh đã tồn tại.", apiResponse.Message);
        }
    }
}
