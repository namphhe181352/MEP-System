using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.SaleController;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.SaleController
{
    public class ProductsControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<ICurrentBranchAccessor> _mockBranchAccessor;

        public ProductsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
        }

        private ProductsController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var controller = new ProductsController(context, _mockBranchAccessor.Object);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            }
            if (branchId.HasValue)
            {
                claims.Add(new Claim("branchId", branchId.Value.ToString()));
                _mockBranchAccessor.Setup(x => x.GetBranchId()).Returns(branchId.Value);
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
        public async Task GetProducts_WithKeyword_ReturnsMatchingProducts()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var productId1 = Guid.NewGuid();
            var productId2 = Guid.NewGuid();
            
            context.Products.Add(new Product { ProductId = productId1, ProductName = "Ống nước nhựa Tiền Phong", ProductCode = "ONG-01", Status = 1, BaseUnit = "Cái" });
            context.Products.Add(new Product { ProductId = productId2, ProductName = "Cáp điện trần phú", ProductCode = "CAP-01", Status = 1, BaseUnit = "Cuộn" });
            
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId1, IsActive = 1, SellPrice = 15000, AvailableStock = 100 });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId2, IsActive = 1, SellPrice = 250000, AvailableStock = 20 });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.GetProducts(search: "ống");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var products = Assert.IsType<List<ProductPriceDto>>(okResult.Value);
            
            Assert.Single(products);
            Assert.Equal("Ống nước nhựa Tiền Phong", products[0].ProductName);
            Assert.Equal(15000, products[0].RetailPrice);
            Assert.Equal(100, products[0].AvailableStock);
        }

        [Fact]
        public async Task GetPrice_ValidProduct_ReturnsPriceInfo()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            
            context.Products.Add(new Product { ProductId = productId, ProductName = "Dây điện", Status = 1 });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, IsActive = 1, SellPrice = 5000 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.GetPrice(productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<ProductPriceDto>(okResult.Value);
            
            Assert.Equal(productId, dto.ProductId);
            Assert.Equal(5000, dto.RetailPrice);
        }

        [Fact]
        public async Task GetStock_ValidProduct_ReturnsStockInfo()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "CN Hà Nội" });
            context.Products.Add(new Product { ProductId = productId, ProductName = "Bóng đèn", Status = 1 });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, IsActive = 1, AvailableStock = 150 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.GetStock(productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<ProductStockDto>(okResult.Value);
            
            Assert.Equal(productId, dto.ProductId);
            Assert.Equal(150, dto.InStockQty);
            Assert.Single(dto.ByBranch);
            Assert.Equal(150, dto.ByBranch[0].AvailableStock);
        }
    }
}
