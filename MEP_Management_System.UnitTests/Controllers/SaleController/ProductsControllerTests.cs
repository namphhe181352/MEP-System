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

        [Theory]
        [InlineData("Ống nước nhựa Tiền Phong", "ONG-01", "ống", 1)]
        [InlineData("Ống nước nhựa Tiền Phong", "ONG-01", "ONG", 1)]
        [InlineData("Cáp điện trần phú", "CAP-01", "Cáp", 1)]
        [InlineData("Cáp điện trần phú", "CAP-01", "cap", 1)]
        [InlineData("Máy bơm nước Ebara", "MAY-01", "máy", 1)]
        [InlineData("Máy bơm nước Ebara", "MAY-01", "Ebara", 1)]
        [InlineData("Vòi sen Inax", "VOI-01", "vòi", 1)]
        [InlineData("Bóng đèn Rạng Đông", "DEN-01", "Bóng", 1)]
        [InlineData("Công tắc Sino", "CONG-01", "sino", 1)]
        [InlineData("Ổ cắm Panasonic", "OCAM-01", "ổ", 1)]
        [InlineData("Dây mạng AMP", "DAY-01", "AMP", 1)]
        [InlineData("Cầu dao tự động LS", "CAU-01", "LS", 1)]
        [InlineData("Keo dán ống Bình Minh", "KEO-01", "Bình", 1)]
        [InlineData("Sơn nước Nippon", "SON-01", "nước", 1)]
        [InlineData("Thước cuộn 5m", "THUOC-01", "5m", 1)]
        [InlineData("Máy khoan cầm tay Bosch", "KHOAN-01", "bosch", 1)]
        [InlineData("Ống nước nhựa Tiền Phong", "ONG-01", "Not match", 0)]
        [InlineData("Cáp điện trần phú", "CAP-01", "ABC", 0)]
        public async Task GetProducts_WithKeyword_ReturnsMatchingProducts(string productName, string productCode, string searchKeyword, int expectedCount)
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            
            context.Products.Add(new Product { ProductId = productId, ProductName = productName, ProductCode = productCode, Status = 1, BaseUnit = "Cái" });
            
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, IsActive = 1, SellPrice = 15000, AvailableStock = 100 });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.GetProducts(search: searchKeyword);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var products = Assert.IsType<List<ProductPriceDto>>(okResult.Value);
            
            Assert.Equal(expectedCount, products.Count);
            if (expectedCount > 0)
            {
                Assert.Equal(productName, products[0].ProductName);
                Assert.Equal(15000, products[0].RetailPrice);
                Assert.Equal(100, products[0].AvailableStock);
            }
        }

        [Theory]
        [InlineData("Dây điện Cadivi", 5000)]
        [InlineData("Ống nước Bình Minh", 25000)]
        [InlineData("Bóng đèn Điện Quang", 18000)]
        [InlineData("Công tắc Panasonic", 45000)]
        [InlineData("Ổ cắm Lioa", 60000)]
        [InlineData("Cáp mạng DINTEK", 12000)]
        [InlineData("Băng keo Nano", 8000)]
        [InlineData("Tua vít bake", 35000)]
        [InlineData("Búa nhổ đinh", 55000)]
        [InlineData("Cờ lê mỏ lết", 85000)]
        public async Task GetPrice_ValidProduct_ReturnsPriceInfo(string productName, decimal sellPrice)
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            
            context.Products.Add(new Product { ProductId = productId, ProductName = productName, Status = 1 });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, IsActive = 1, SellPrice = sellPrice });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.GetPrice(productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<ProductPriceDto>(okResult.Value);
            
            Assert.Equal(productId, dto.ProductId);
            Assert.Equal(sellPrice, dto.RetailPrice);
        }

        [Theory]
        [InlineData("CN Hà Nội", "Bóng đèn", 150)]
        [InlineData("CN Đà Nẵng", "Ống nước", 500)]
        [InlineData("CN TP.HCM", "Cáp điện", 1200)]
        [InlineData("CN Cần Thơ", "Công tắc", 300)]
        [InlineData("CN Hải Phòng", "Ổ cắm", 850)]
        [InlineData("CN Đồng Nai", "Máy bơm", 45)]
        [InlineData("CN Bình Dương", "Máy mài", 20)]
        [InlineData("CN Vũng Tàu", "Bình nóng lạnh", 100)]
        [InlineData("CN Nha Trang", "Quạt trần", 65)]
        [InlineData("CN Quảng Ninh", "Điều hòa", 10)]
        public async Task GetStock_ValidProduct_ReturnsStockInfo(string branchName, string productName, decimal availableStock)
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = branchName });
            context.Products.Add(new Product { ProductId = productId, ProductName = productName, Status = 1 });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, IsActive = 1, AvailableStock = availableStock });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.GetStock(productId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<ProductStockDto>(okResult.Value);
            
            Assert.Equal(productId, dto.ProductId);
            Assert.Equal(availableStock, dto.InStockQty);
            Assert.Single(dto.ByBranch);
            Assert.Equal(availableStock, dto.ByBranch[0].AvailableStock);
            Assert.Equal(branchName, dto.ByBranch[0].BranchName);
        }
    }
}
