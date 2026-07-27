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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

using Microsoft.EntityFrameworkCore.Diagnostics;

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

        [Fact]
        public async Task GetBranch_OwnerHasActiveBranches_ReturnsOkWithList()
        {
            // Arrange (UTCID001)
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "CN 1", IsActive = 1 });
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
            Assert.Equal("CN 1", apiResponse.Data.First().BranchName);
        }

        [Fact]
        public async Task GetBranch_OwnerHasNoActiveBranches_ReturnsNotFound()
        {
            // Arrange (UTCID002)
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            
            // No branches added for this owner
            var controller = CreateController(context, ownerId);

            // Act
            var result = await controller.GetBranch();

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("Không tìm thấy chi nhánh nào.", apiResponse.Message);
        }

        [Fact]
        public async Task GetBranch_MissingUserClaim_ReturnsUnauthorized()
        {
            // Arrange (UTCID003)
            using var context = new MEPDatabaseContext(_dbContextOptions);
            
            // Create controller without NameIdentifier claim
            var controller = CreateController(context, null);

            // Act
            var result = await controller.GetBranch();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(unauthorizedResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("Unauthorized.", apiResponse.Message);
        }

        [Fact]
        public async Task CreateBranch_ValidData_ReturnsOk()
        {
            // Arrange (UTCID001 for CreateBranch)
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            var controller = CreateController(context, ownerId);

            var request = new CreateOwnerBranchRequest
            {
                BranchName = "CN Quận 1",
                BranchCode = "B001",
                Phone = "0987654321",
                Address = "123 Le Loi"
            };

            // Act
            var result = await controller.CreateBranch(request);

            // Assert
            var okResult = Assert.IsType<CreatedAtActionResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<OwnerBranchDto>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Equal("CN Quận 1", apiResponse.Data.BranchName);
            
            // Verify DB state
            var dbBranch = await context.Branches.FirstOrDefaultAsync(b => b.BranchCode == "B001");
            Assert.NotNull(dbBranch);
            Assert.Equal(ownerId, dbBranch.ManagerUserId);
        }

        [Fact]
        public async Task CreateBranch_MissingBranchName_ReturnsBadRequest()
        {
            // Arrange (UTCID002 for CreateBranch)
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            var controller = CreateController(context, ownerId);

            var request = new CreateOwnerBranchRequest
            {
                BranchName = "", // Empty name
                BranchCode = "B002"
            };

            // Act
            var result = await controller.CreateBranch(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("Tên chi nhánh là bắt buộc.", apiResponse.Message);
        }

        [Fact]
        public async Task CreateBranch_DuplicateBranchCode_ReturnsBadRequest()
        {
            // Arrange (UTCID003 for CreateBranch)
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            
            // Seed a branch with code B001
            context.Branches.Add(new Branch { BranchId = Guid.NewGuid(), BranchName = "Old Branch", BranchCode = "B001", IsActive = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, ownerId);

            var request = new CreateOwnerBranchRequest
            {
                BranchName = "New Branch",
                BranchCode = "B001" // Duplicate
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
