using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.OwnerController;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;
using MEP_Management_System.DTOs.InventoryDTOs;

namespace MEP_Management_System.UnitTests.Controllers.OwnerController
{
    public class OwnerBranchControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public OwnerBranchControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private OwnerBranchController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new OwnerBranchController(context);

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
        public async Task GetBranch_OwnerWithAssignedBranch_ReturnsBranches()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            var controller = CreateController(context, ownerId);

            var branch = new Branch { BranchId = Guid.NewGuid(), BranchName = "Test Branch", IsActive = 1 };
            context.Branches.Add(branch);
            
            context.StaffAssignments.Add(new StaffAssignment { AssignmentId = Guid.NewGuid(), UserId = ownerId, BranchId = branch.BranchId });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetBranch();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<List<OwnerBranchDto>>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.Single(response.Data);
            Assert.Equal("Test Branch", response.Data.First().BranchName);
        }

        [Fact]
        public async Task CreateBranch_ValidData_CreatesBranchAndAssignsOwner()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            var controller = CreateController(context, ownerId);

            var request = new CreateOwnerBranchRequest
            {
                BranchName = "New Store 1",
                BranchCode = "NS01",
                City = "Hanoi"
            };

            // Act
            var result = await controller.CreateBranch(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var response = Assert.IsType<ApiResponse<OwnerBranchDto>>(createdResult.Value);
            
            Assert.True(response.Success);
            Assert.Equal("New Store 1", response.Data.BranchName);

            // Verify assignment in DB
            var branchId = response.Data.BranchId;
            var assignment = await context.StaffAssignments.FirstOrDefaultAsync(sa => sa.BranchId == branchId && sa.UserId == ownerId);
            Assert.NotNull(assignment);
        }

        [Fact]
        public async Task UpdateBranchSettings_NotOwner_ReturnsForbid()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ownerId = Guid.NewGuid();
            var controller = CreateController(context, ownerId);

            var branch = new Branch { BranchId = Guid.NewGuid(), BranchName = "Test Branch", IsActive = 1 };
            context.Branches.Add(branch);
            // Notice: We don't add staff assignment here, so Owner does NOT own this branch
            await context.SaveChangesAsync();

            var request = new UpdateBranchSettingsRequest { ReturnDaysAllowed = 7 };

            // Act
            var result = await controller.UpdateBranchSettings(branch.BranchId, request);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }
    }
}
