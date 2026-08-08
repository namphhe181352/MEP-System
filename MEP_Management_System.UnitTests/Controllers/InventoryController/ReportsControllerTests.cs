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
        }

        [Fact]
        public async Task GetDailyEndReport_ValidRequest_ReturnsReport()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Cửa hàng Kim khí Điện nước 1" });
            
            // Add some completed orders for today
            context.Orders.Add(new Order
            {
                OrderId = Guid.NewGuid(),
                BranchId = branchId,
                Status = "COMPLETED",
                TotalAmount = 2500000,
                CreatedAt = DateTime.UtcNow
            });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var request = new DailyEndReportRequestDto { Date = DateTime.UtcNow.Date };

            // Act
            var result = await controller.GetDailyEndReport(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<DailyEndReportDto>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            Assert.Equal("Cửa hàng Kim khí Điện nước 1", response.Data.BranchName);
            Assert.Equal(2500000, response.Data.TotalRevenue);
            Assert.Equal(1, response.Data.TotalOrders);
        }

        [Fact]
        public async Task GetStockMovementReport_ValidRequest_ReturnsReport()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Cửa hàng Kim khí Điện nước 1" });
            
            var productId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = productId, ProductCode = "ONG-21", ProductName = "Ống nhựa Bình Minh phi 21", Status = 1 });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, IsActive = 1, CostPrice = 15000 });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var request = new StockMovementReportRequestDto 
            { 
                FromDate = DateTime.UtcNow.AddDays(-7),
                ToDate = DateTime.UtcNow
            };

            // Act
            var result = await controller.GetStockMovementReport(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<StockMovementReportDto>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            Assert.Single(response.Data.Items);
            Assert.Equal("ONG-21", response.Data.Items[0].ProductCode);
        }

        [Fact]
        public async Task GetLowStockReport_ReturnsLowStockItems()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            var productId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = productId, ProductCode = "BONG-9W", ProductName = "Bóng đèn Led Rạng Đông 9W" });
            context.BranchProducts.Add(new BranchProduct 
            { 
                BranchProductId = Guid.NewGuid(), 
                BranchId = branchId, 
                ProductId = productId,
                ActualStock = 5,
                MinimumStock = 20 
            });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var request = new LowStockReportRequestDto();

            // Act
            var result = await controller.GetLowStockReport(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<LowStockReportDto>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
            Assert.Single(response.Data.Items);
            
            var item = response.Data.Items[0];
            Assert.Equal("BONG-9W", item.ProductCode);
            Assert.Equal(5, item.CurrentStock);
        }
    }
}
