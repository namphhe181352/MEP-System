using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.InventoryController;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.InventoryController
{
    public class ReportsControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<ICurrentBranchAccessor> _mockBranchAccessor;

        public ReportsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
        }

        private ReportsController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var reportService = new ReportService(context, _mockBranchAccessor.Object);
            var controller = new ReportsController(context, _mockBranchAccessor.Object, reportService);

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

            if (branchId.HasValue)
            {
                _mockBranchAccessor.Setup(x => x.GetBranchId()).Returns(branchId.Value);
                // Also add user with default branch to ensure it resolves
                if (userId.HasValue)
                {
                    if (!context.Users.Any(u => u.UserId == userId.Value))
                    {
                        context.Users.Add(new User { UserId = userId.Value, DefaultBranchId = branchId.Value });
                        context.SaveChanges();
                    }
                }
            }
            else
            {
                _mockBranchAccessor.Setup(x => x.GetBranchId()).Returns((Guid?)null);
            }

            return controller;
        }        [Theory]
        [InlineData("COMPLETED", 1000000, 1000000, 1)] // Single completed order
        [InlineData("CANCELLED", 1000000, 0, 0)] // Cancelled order shouldn't count towards revenue
        [InlineData("DRAFT", 1000000, 0, 0)] // Draft shouldn't count
        [InlineData("COMPLETED", 0, 0, 1)] // Zero value order
        [InlineData("COMPLETED", 500000, 500000, 1)] // Different amount
        public async Task GetDailyEndReport_CalculatesRevenueCorrectly(string orderStatus, decimal orderAmount, decimal expectedRevenue, int expectedCount)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Test Branch" });
            
            context.Orders.Add(new Order
            {
                OrderId = Guid.NewGuid(),
                BranchId = branchId,
                Status = orderStatus,
                TotalAmount = orderAmount,
                CreatedAt = DateTime.UtcNow
            });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var request = new DailyEndReportRequestDto { Date = DateTime.UtcNow.Date };

            var result = await controller.GetDailyEndReport(request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<DailyEndReportDto>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.Equal(expectedRevenue, response.Data.TotalRevenue);
            Assert.Equal(expectedCount, response.Data.TotalOrders);
        }

        [Theory]
        [InlineData(10, 5, true)] // Below min stock -> appears in report
        [InlineData(10, 15, false)] // Above min stock -> doesn't appear
        [InlineData(10, 10, true)] // Equal to min stock -> DOES appear (<= logic)
        [InlineData(50, 0, true)] // Out of stock
        [InlineData(0, 0, true)] // Zero min stock, zero stock -> DOES appear (<= logic)
        public async Task GetLowStockReport_FiltersCorrectly(int minStock, int actualStock, bool expectedInReport)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            var productId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = productId, ProductCode = "TEST", ProductName = "Test" });
            context.BranchProducts.Add(new BranchProduct 
            { 
                BranchProductId = Guid.NewGuid(), 
                BranchId = branchId, 
                ProductId = productId,
                ActualStock = actualStock,
                MinimumStock = minStock 
            });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var request = new LowStockReportRequestDto();

            var result = await controller.GetLowStockReport(request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<LowStockReportDto>>(okResult.Value);
            
            Assert.True(response.Success);
            if (expectedInReport)
            {
                Assert.Single(response.Data.Items);
            }
            else
            {
                Assert.Empty(response.Data.Items);
            }
        }
    }
}

