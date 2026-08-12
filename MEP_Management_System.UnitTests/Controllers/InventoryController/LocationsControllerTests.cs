    using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.InventoryController;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.InventoryController
{
    public class LocationsControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public LocationsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private LocationsController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var controller = new LocationsController(context);
            var claims = new List<Claim>();
            if (userId.HasValue) claims.Add(new Claim("userId", userId.Value.ToString()));
            if (branchId.HasValue) claims.Add(new Claim("branchId", branchId.Value.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, "Owner"));

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
            return controller;
        }

        // =====================================
        // 1. GET LOCATIONS
        // =====================================

        [Theory]
        [InlineData(true, 1)] // activeOnly = true -> returns 1 active
        [InlineData(false, 2)] // activeOnly = false -> returns 2 (active + inactive)
        public async Task GetLocations_FiltersByActiveStatus(bool activeOnly, int expectedCount)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            context.ProductLocations.Add(new ProductLocation { LocationId = Guid.NewGuid(), BranchId = branchId, LocationName = "Active 1", IsActive = 1 });
            context.ProductLocations.Add(new ProductLocation { LocationId = Guid.NewGuid(), BranchId = branchId, LocationName = "Inactive 1", IsActive = 0 });
            context.ProductLocations.Add(new ProductLocation { LocationId = Guid.NewGuid(), BranchId = Guid.NewGuid(), LocationName = "Other Branch", IsActive = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid(), branchId);
            var result = await controller.GetLocations(activeOnly);
            
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<List<ProductLocation>>>(okResult.Value);
            Assert.Equal(expectedCount, response.Data.Count);
        }

        // =====================================
        // 2. CREATE LOCATION
        // =====================================

        [Theory]
        [InlineData("Valid", "Code1", "Tên vị trí đã tồn tại trong chi nhánh này.")] // Duplicate Name
        public async Task CreateLocation_InvalidData_ReturnsBadRequest(string name, string code, string expectedError)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            context.ProductLocations.Add(new ProductLocation { LocationId = Guid.NewGuid(), BranchId = branchId, LocationName = "Valid", LocationCode = "DupCode" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid(), branchId);
            var request = new CreateLocationDto { LocationName = name ?? "Test", LocationCode = code };
            
            var result = await controller.CreateLocation(request);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(badRequest.Value);
            Assert.Contains(expectedError, response.Message ?? "");
        }

        [Fact]
        public async Task CreateLocation_ValidData_CreatesAndReturnsLocation()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, Guid.NewGuid(), branchId);
            var request = new CreateLocationDto { LocationName = "New Loc", LocationCode = "NEW_LOC" };

            var result = await controller.CreateLocation(request);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<ProductLocation>>(okResult.Value);
            
            Assert.True(response.Success);
            var dbLoc = await context.ProductLocations.FirstOrDefaultAsync(l => l.LocationCode == "NEW_LOC");
            Assert.NotNull(dbLoc);
            Assert.Equal(branchId, dbLoc.BranchId);
        }

        // =====================================
        // 3. UPDATE LOCATION
        // =====================================

        [Theory]
        [InlineData("valid", "Không tìm thấy vị trí")] // ID not found
        [InlineData("dup", "Tên vị trí đã tồn tại trong chi nhánh này.")] // Name exists for another
        public async Task UpdateLocation_InvalidConditions_ReturnsBadRequest(string scenario, string expectedError)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            
            if (scenario != "valid") 
            {
                context.ProductLocations.Add(new ProductLocation { LocationId = targetId, BranchId = branchId, LocationName = "Target", LocationCode = "TargetCode" });
                context.ProductLocations.Add(new ProductLocation { LocationId = Guid.NewGuid(), BranchId = branchId, LocationName = "dup", LocationCode = "dupCode" });
                await context.SaveChangesAsync();
            }

            var controller = CreateController(context, Guid.NewGuid(), branchId);
            var searchId = scenario == "valid" ? Guid.NewGuid() : targetId;
            var request = new UpdateLocationDto { LocationName = scenario == "dup" ? scenario : "NewName", LocationCode = "NewCode" };
            
            var result = await controller.UpdateLocation(searchId, request);
            if (scenario == "valid") {
                Assert.IsType<NotFoundObjectResult>(result);
            } else {
                var badRequest = Assert.IsType<BadRequestObjectResult>(result);
                var response = Assert.IsType<ApiResponse<object>>(badRequest.Value);
                Assert.Contains(expectedError, response.Message ?? "");
            }
        }

        [Fact]
        public async Task UpdateLocation_ValidData_UpdatesSuccessfully()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var targetId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            context.ProductLocations.Add(new ProductLocation { LocationId = targetId, BranchId = branchId, LocationName = "Old", LocationCode = "OldCode" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid(), branchId);
            var request = new UpdateLocationDto { LocationName = "New", LocationCode = "NewCode", IsActive = 0 };

            var result = await controller.UpdateLocation(targetId, request);
            Assert.IsType<OkObjectResult>(result);
            var dbLoc = await context.ProductLocations.FindAsync(targetId);
            Assert.Equal("New", dbLoc!.LocationName);
            Assert.Equal(0, dbLoc.IsActive);
        }

        // =====================================
        // 4. DELETE LOCATION
        // =====================================

        [Fact]
        public async Task DeleteLocation_NotFound_ReturnsNotFound()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid(), Guid.NewGuid());
            var result = await controller.DeleteLocation(Guid.NewGuid());
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task DeleteLocation_ValidId_PerformsSoftDelete()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var targetId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            context.ProductLocations.Add(new ProductLocation { LocationId = targetId, BranchId = branchId, IsActive = 1, LocationName = "Test" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid(), branchId);
            var result = await controller.DeleteLocation(targetId);
            Assert.IsType<OkObjectResult>(result);

            var dbLoc = await context.ProductLocations.FindAsync(targetId);
            Assert.Equal(0, dbLoc!.IsActive);
        }
    }
}
