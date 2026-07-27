using System;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.OwnerController;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class OwnerOrderDiscountTiersControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public OwnerOrderDiscountTiersControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private OwnerOrderDiscountTiersController CreateController(MEPDatabaseContext context)
        {
            return new OwnerOrderDiscountTiersController(context);
        }

        [Fact]
        public async Task Create_ValidData_ReturnsCreated()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);
            var branchId = Guid.NewGuid();

            var request = new CreateOrderDiscountTierDto
            {
                BranchId = branchId,
                MinOrderValue = 1000000,
                DiscountPercent = 10
            };

            // Act
            var result = await controller.Create(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<OrderDiscountTierDto>(createdResult.Value);
            Assert.Equal(1000000, dto.MinOrderValue);
            Assert.Equal(10, dto.DiscountPercent);
            Assert.Equal(branchId, dto.BranchId);
        }

        [Fact]
        public async Task Create_InvalidDiscountPercent_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var request = new CreateOrderDiscountTierDto
            {
                MinOrderValue = 1000000,
                DiscountPercent = 150 // Invalid > 100
            };

            // Act
            var result = await controller.Create(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(badRequestResult.Value);
            Assert.Equal("MSG-02", errorResponse.Error.Code);
        }

        [Fact]
        public async Task Update_ValidData_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);
            var branchId = Guid.NewGuid();
            var tierId = Guid.NewGuid();

            context.OrderDiscountTiers.Add(new OrderDiscountTier
            {
                OrderDiscountTierId = tierId,
                BranchId = branchId,
                MinOrderValue = 1000000,
                DiscountPercent = 10,
                IsActive = true
            });
            await context.SaveChangesAsync();

            var request = new UpdateOrderDiscountTierDto
            {
                BranchId = branchId,
                MinOrderValue = 2000000,
                DiscountPercent = 15,
                IsActive = true
            };

            // Act
            var result = await controller.Update(tierId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<OrderDiscountTierDto>(okResult.Value);
            Assert.Equal(2000000, dto.MinOrderValue);
            Assert.Equal(15, dto.DiscountPercent);
        }

        [Fact]
        public async Task Delete_ValidId_ReturnsNoContent()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);
            var tierId = Guid.NewGuid();

            context.OrderDiscountTiers.Add(new OrderDiscountTier
            {
                OrderDiscountTierId = tierId,
                MinOrderValue = 1000000,
                DiscountPercent = 10
            });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.Delete(tierId);

            // Assert
            Assert.IsType<NoContentResult>(result);
            var deletedTier = await context.OrderDiscountTiers.FindAsync(tierId);
            Assert.Null(deletedTier);
        }
    }
}
