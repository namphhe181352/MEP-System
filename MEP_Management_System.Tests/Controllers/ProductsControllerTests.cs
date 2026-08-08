using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class ProductsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public ProductsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private MEP_Management_System.Controllers.AdminController.ProductsController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new MEP_Management_System.Controllers.AdminController.ProductsController(context);

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

            return controller;
        }

        // ==========================================
        // TEST POST /api/admin/products (Global Product Catalog)
        // ==========================================
        [Fact]
        public async Task CreateProduct_ValidData_CreatesMasterProductInCatalog()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            var categoryId = Guid.NewGuid();
            context.ProductCategories.Add(new ProductCategory { CategoryId = categoryId, CategoryName = "Nước Giải Khát", IsActive = 1 });
            await context.SaveChangesAsync();

            var request = new MEP_Management_System.DTOs.AdminDTOs.CreateProductDto
            {
                ProductName = "Nước Ngọt Coca Cola 330ml",
                ProductCode = "SP-COCA-330",
                BaseUnit = "Lon",
                CostPrice = 8000,
                SellingPrice = 12000,
                CategoryId = categoryId
            };

            // Act
            var result = await controller.CreateProduct(request);

            // Assert
            var okResult = Assert.IsType<CreatedAtActionResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<MEP_Management_System.DTOs.AdminDTOs.ProductDto>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Equal("Nước Ngọt Coca Cola 330ml", apiResponse.Data.ProductName);
            
            var dbProduct = await context.Products.FirstOrDefaultAsync(p => p.ProductCode == "SP-COCA-330");
            Assert.NotNull(dbProduct);
            Assert.Equal(12000, dbProduct.SellingPrice);
            Assert.Equal(categoryId, dbProduct.CategoryId);
        }
    }
}
