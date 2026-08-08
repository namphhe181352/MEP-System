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

namespace MEP_Management_System.Tests.Controllers
{
    public class InventoryProductsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<ICurrentBranchAccessor> _mockBranchAccessor;

        public InventoryProductsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
        }

        private InventoryProductsController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null, string role = "Owner")
        {
            var controller = new InventoryProductsController(context, _mockBranchAccessor.Object);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
                claims.Add(new Claim("userId", userId.Value.ToString()));
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
                _mockBranchAccessor.Setup(m => m.GetBranchId()).Returns(branchId.Value);
            else
                _mockBranchAccessor.Setup(m => m.GetBranchId()).Returns((Guid?)null);

            return controller;
        }

        // ==========================================
        // TEST GET /api/inventory/products
        // ==========================================
        [Fact]
        public async Task GetProducts_ReturnsProductsFromSpecificBranch_Only()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branch1Id = Guid.NewGuid();
            var branch2Id = Guid.NewGuid(); // Another branch
            var userId = Guid.NewGuid();
            
            // Using controller assigned to branch 1
            var controller = CreateController(context, userId, branch1Id);

            var productA = new Product { ProductId = Guid.NewGuid(), ProductName = "Bàn phím cơ Akko", SellingPrice = 1200000, Status = "ACTIVE" };
            var productB = new Product { ProductId = Guid.NewGuid(), ProductName = "Chuột Logitech G102", SellingPrice = 450000, Status = "ACTIVE" };
            context.Products.AddRange(productA, productB);
            
            // Product A is in Branch 1 (Stock = 5)
            context.BranchProducts.Add(new BranchProduct { BranchId = branch1Id, ProductId = productA.ProductId, StockQuantity = 5 });
            // Product B is in Branch 2 (Stock = 20), should NOT be visible to Branch 1 query
            context.BranchProducts.Add(new BranchProduct { BranchId = branch2Id, ProductId = productB.ProductId, StockQuantity = 20 });
            
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetProducts(search: null, categoryId: null, status: null, pageNumber: 1, pageSize: 10, branchId: branch1Id);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<PaginatedResponse<InventoryProductDto>>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Single(apiResponse.Data.Items); // Should only return product A
            
            var returnedItem = apiResponse.Data.Items.First();
            Assert.Equal("Bàn phím cơ Akko", returnedItem.ProductName);
            Assert.Equal(5, returnedItem.StockQuantity);
        }

        [Fact]
        public async Task GetLowStockProducts_ReturnsOnlyProductsUnderThreshold()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, Guid.NewGuid(), branchId);

            var p1 = new Product { ProductId = Guid.NewGuid(), ProductName = "Màn hình Dell 24 inch", Status = "ACTIVE" };
            var p2 = new Product { ProductId = Guid.NewGuid(), ProductName = "Cáp HDMI 2m", Status = "ACTIVE" };
            context.Products.AddRange(p1, p2);
            
            // p1 has 2 items left, p2 has 50 items left
            // Default threshold is usually around 10
            context.BranchProducts.Add(new BranchProduct { BranchId = branchId, ProductId = p1.ProductId, StockQuantity = 2 });
            context.BranchProducts.Add(new BranchProduct { BranchId = branchId, ProductId = p2.ProductId, StockQuantity = 50 });
            await context.SaveChangesAsync();

            // Act
            // GetLowStock endpoint usually exists, or using standard GetProducts with a lowStock filter
            // Depending on controller implementation:
            var result = await controller.GetProducts(search: null, categoryId: null, status: "LOW_STOCK", pageNumber: 1, pageSize: 10, branchId: branchId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<PaginatedResponse<InventoryProductDto>>>(okResult.Value);
            
            // It should only return p1
            Assert.True(apiResponse.Success);
            Assert.Single(apiResponse.Data.Items);
            Assert.Equal("Màn hình Dell 24 inch", apiResponse.Data.Items.First().ProductName);
        }
    }
}
