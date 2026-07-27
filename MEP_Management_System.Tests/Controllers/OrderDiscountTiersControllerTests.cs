using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.SaleController;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class OrderDiscountTiersControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

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
        public async Task GetAll_ReturnsSortedDiscountTiers()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);
            
            var branchId = Guid.NewGuid();
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Main Branch" });

            context.OrderDiscountTiers.Add(new OrderDiscountTier { OrderDiscountTierId = Guid.NewGuid(), BranchId = branchId, MinOrderValue = 100000, DiscountPercent = 10 });
            context.OrderDiscountTiers.Add(new OrderDiscountTier { OrderDiscountTierId = Guid.NewGuid(), BranchId = branchId, MinOrderValue = 50000, DiscountPercent = 5 });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetAll(branchId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var tiers = Assert.IsAssignableFrom<IEnumerable<OrderDiscountTierDto>>(okResult.Value);
            Assert.Equal(2, tiers.Count());
            
            // Should be sorted by MinOrderValue ascending
            Assert.Equal(50000, tiers.First().MinOrderValue);
            Assert.Equal(5, tiers.First().DiscountPercent);
            Assert.Equal("Main Branch", tiers.First().BranchName);
        }

        [Fact]
        public async Task GetById_InvalidId_ReturnsNotFound()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            // Act
            var result = await controller.GetById(Guid.NewGuid());

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(notFoundResult.Value);
            Assert.Equal("MSG-01", errorResponse.Error.Code);
        }

        [Fact]
        public async Task GetById_ValidId_ReturnsTier()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var tierId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Main Branch" });
            context.OrderDiscountTiers.Add(new OrderDiscountTier { OrderDiscountTierId = tierId, BranchId = branchId, MinOrderValue = 50000, DiscountPercent = 5 });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetById(tierId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<OrderDiscountTierDto>(okResult.Value);
            Assert.Equal(tierId, dto.OrderDiscountTierId);
            Assert.Equal(50000, dto.MinOrderValue);
            Assert.Equal("Main Branch", dto.BranchName);
        }
    }
}
