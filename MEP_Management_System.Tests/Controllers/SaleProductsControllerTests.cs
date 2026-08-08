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

        // ==========================================
        // TEST GET /api/sale/products
        // ==========================================
        [Fact]
        public async Task GetProducts_FiltersByActiveBranchOnly_ReturnsCorrectPrices()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var activeBranchId = Guid.NewGuid();
            var anotherBranchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, activeBranchId, userId);

            // Create product
            var product = new Product { ProductId = Guid.NewGuid(), ProductName = "Bia Heineken Thùng 24", ProductCode = "B-HEI-24", BaseUnit = "Thùng", Status = 1 };
            context.Products.Add(product);
            
            // Branch 1 price
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = activeBranchId, ProductId = product.ProductId, Product = product, IsActive = 1, SellPrice = 450000 });
            // Branch 2 price
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = anotherBranchId, ProductId = product.ProductId, Product = product, IsActive = 1, SellPrice = 460000 });
            
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetProducts(null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var items = Assert.IsType<List<ProductPriceDto>>(okResult.Value);
            
            Assert.Single(items);
            Assert.Equal("Bia Heineken Thùng 24", items.First().ProductName);
            Assert.Equal(450000, items.First().RetailPrice); // Must return the price specific to activeBranchId
        }

        // ==========================================
        // TEST GET /api/sale/products/{id}/price
        // ==========================================
        [Fact]
        public async Task GetPrice_ValidProductId_ReturnsBranchSpecificPrice()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId, Guid.NewGuid());

            var productId = Guid.NewGuid();
            var product = new Product { ProductId = productId, ProductName = "Sữa Tươi Vinamilk 1L", ProductCode = "S-VNM-1L", BaseUnit = "Hộp", Status = 1 };
            context.Products.Add(product);
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, Product = product, IsActive = 1, SellPrice = 35000 });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetPrice(productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<ProductPriceDto>(okResult.Value);
            Assert.Equal(35000, dto.RetailPrice);
        }
    }
}
