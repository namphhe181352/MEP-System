using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.SaleController;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.SaleController
{
    public class OrderDiscountTiersControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public OrderDiscountTiersControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private OrderDiscountTiersController CreateController(MEPDatabaseContext context)
        {
            return new OrderDiscountTiersController(context);
        }

        [Fact]
        public async Task GetAll_ReturnsSortedTiers()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Chi nhánh 1" });
            context.OrderDiscountTiers.Add(new OrderDiscountTier { OrderDiscountTierId = Guid.NewGuid(), BranchId = branchId, MinOrderValue = 5000000, DiscountPercent = 10 });
            context.OrderDiscountTiers.Add(new OrderDiscountTier { OrderDiscountTierId = Guid.NewGuid(), BranchId = branchId, MinOrderValue = 1000000, DiscountPercent = 5 });
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.GetAll(branchId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var tiers = Assert.IsAssignableFrom<IEnumerable<OrderDiscountTierDto>>(okResult.Value);
            
            Assert.Equal(2, tiers.Count());
            Assert.Equal(1000000, tiers.First().MinOrderValue); // Sorted by MinOrderValue ASC
            Assert.Equal(5000000, tiers.Last().MinOrderValue);
        }

        [Fact]
        public async Task CreateTier_ValidData_CreatesTier()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Chi nhánh 1" });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var request = new CreateOrderDiscountTierRequest
            {
                BranchId = branchId,
                MinOrderValue = 10000000,
                DiscountPercent = 15,
                IsActive = true
            };

            // Act
            var result = await controller.CreateTier(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<OrderDiscountTierDto>(createdResult.Value);
            
            Assert.Equal(10000000, dto.MinOrderValue);
            Assert.Equal(15, dto.DiscountPercent);
            
            var dbTier = await context.OrderDiscountTiers.FindAsync(dto.OrderDiscountTierId);
            Assert.NotNull(dbTier);
            Assert.Equal(10000000, dbTier.MinOrderValue);
        }

        [Fact]
        public async Task CreateTier_DuplicateMinOrderValue_ReturnsConflict()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Chi nhánh 1" });
            context.OrderDiscountTiers.Add(new OrderDiscountTier { OrderDiscountTierId = Guid.NewGuid(), BranchId = branchId, MinOrderValue = 2000000, DiscountPercent = 5 });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var request = new CreateOrderDiscountTierRequest
            {
                BranchId = branchId,
                MinOrderValue = 2000000, // Duplicate
                DiscountPercent = 10
            };

            // Act
            var result = await controller.CreateTier(request);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
            var response = Assert.IsType<MEP_Management_System.DTOs.SaleDTOs.ApiErrorResponse>(conflictResult.Value);
            Assert.Contains("đã tồn tại", response.Error.Message);
        }

        [Fact]
        public async Task UpdateTier_ValidData_UpdatesTier()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var tierId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Chi nhánh 1" });
            context.OrderDiscountTiers.Add(new OrderDiscountTier { OrderDiscountTierId = tierId, BranchId = branchId, MinOrderValue = 1000000, DiscountPercent = 5 });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var request = new CreateOrderDiscountTierRequest
            {
                BranchId = branchId,
                MinOrderValue = 1500000, // Updated value
                DiscountPercent = 7
            };

            // Act
            var result = await controller.UpdateTier(tierId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<OrderDiscountTierDto>(okResult.Value);
            
            Assert.Equal(1500000, dto.MinOrderValue);
            Assert.Equal(7, dto.DiscountPercent);
            
            var dbTier = await context.OrderDiscountTiers.FindAsync(tierId);
            Assert.Equal(1500000, dbTier.MinOrderValue);
        }
        [Theory]
        [InlineData(2000000, 10, true)] // Valid update
        [InlineData(1000000, 15, true)] // Valid update same min value, diff percent
        [InlineData(5000000, 20, false)] // Conflict with another existing tier's min value
        public async Task UpdateTier_Validations_ReturnsAppropriateResult(decimal newMinValue, decimal newPercent, bool isSuccess)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var tierId = Guid.NewGuid();
            var otherTierId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Chi nhánh 1" });
            context.OrderDiscountTiers.Add(new OrderDiscountTier { OrderDiscountTierId = tierId, BranchId = branchId, MinOrderValue = 1000000, DiscountPercent = 5 });
            context.OrderDiscountTiers.Add(new OrderDiscountTier { OrderDiscountTierId = otherTierId, BranchId = branchId, MinOrderValue = 5000000, DiscountPercent = 10 });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var request = new CreateOrderDiscountTierRequest
            {
                BranchId = branchId,
                MinOrderValue = newMinValue,
                DiscountPercent = newPercent
            };

            var result = await controller.UpdateTier(tierId, request);

            if (isSuccess)
            {
                var okResult = Assert.IsType<OkObjectResult>(result.Result);
            }
            else
            {
                var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
            }
        }
    }
}
