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

namespace MEP_Management_System.Tests.Controllers
{
    public class SaleProductsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public SaleProductsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private ProductsController CreateController(MEPDatabaseContext context, Guid branchId, Guid? userId = null, string role = "SalesStaff")
        {
            var mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
            mockBranchAccessor.Setup(m => m.GetBranchId()).Returns(branchId);

            var controller = new ProductsController(context, mockBranchAccessor.Object);

            var claims = new List<Claim>
            {
                new Claim("branchId", branchId.ToString())
            };
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
        public async Task GetProducts_ReturnsProducts()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, branchId, userId);

            var productId = Guid.NewGuid();
            var product = new Product { ProductId = productId, ProductName = "Test Product", ProductCode = "TP01", BaseUnit = "Cái", Status = 1 };
            context.Products.Add(product);
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, Product = product, IsActive = 1, SellPrice = 1000 });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetProducts(null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var items = Assert.IsType<List<ProductPriceDto>>(okResult.Value);
            Assert.Single(items);
            Assert.Equal("Test Product", items.First().ProductName);
        }

        [Fact]
        public async Task GetPrice_ValidId_ReturnsPrice()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, branchId, userId);

            var productId = Guid.NewGuid();
            var product = new Product { ProductId = productId, ProductName = "Test Product", ProductCode = "TP01", BaseUnit = "Cái", Status = 1 };
            context.Products.Add(product);
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, Product = product, IsActive = 1, SellPrice = 1000 });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetPrice(productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<ProductPriceDto>(okResult.Value);
            Assert.Equal(1000, dto.RetailPrice);
        }

        [Fact]
        public async Task GetStock_ValidId_ReturnsStock()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, branchId, userId);

            var productId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = productId, ProductName = "Test Product", ProductCode = "TP01", BaseUnit = "Cái", Status = 1 });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, IsActive = 1, AvailableStock = 50 });
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Test Branch" });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetStock(productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<ProductStockDto>(okResult.Value);
            Assert.Single(dto.ByBranch);
            Assert.Equal(50, dto.ByBranch.First().AvailableStock);
        }
    }
}
