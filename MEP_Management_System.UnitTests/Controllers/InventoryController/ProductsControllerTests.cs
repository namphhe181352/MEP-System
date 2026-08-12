using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
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
    public class ProductsControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<ICurrentBranchAccessor> _mockBranchAccessor;
        private readonly Mock<IMapper> _mockMapper;

        public ProductsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
            _mockMapper = new Mock<IMapper>();
        }

        private ProductsController CreateController(MEPDatabaseContext context, Guid? branchId = null)
        {
            var controller = new ProductsController(context, _mockMapper.Object, _mockBranchAccessor.Object);
            var claims = new List<Claim>();
            if (branchId.HasValue) 
            {
                claims.Add(new Claim("branchId", branchId.Value.ToString()));
                _mockBranchAccessor.Setup(x => x.GetBranchId()).Returns(branchId.Value);
            }
            claims.Add(new Claim(ClaimTypes.Role, "Owner"));
            
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
            return controller;
        }

        // =====================================
        // 1. SEARCH PRODUCTS
        // =====================================

        [Theory]
        [InlineData("Ống", 1)] // Matches 1 active
        [InlineData("NotExist", 0)] // Matches 0
        public async Task SearchProducts_FiltersCorrectly(string? search, int expectedCount)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var catId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            context.ProductTaxonomies.Add(new ProductTaxonomy { TaxonomyId = catId, Name = "Cat 1", Type = "Category", BranchId = branchId });
            context.Products.Add(new Product { ProductId = productId, ProductCode = "ONG-21", ProductName = "Ống nhựa", Status = 1, CategoryTaxonomyId = catId });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, IsActive = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, branchId);
            var result = await controller.SearchProducts(search);
            
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            var items = response.Data.GetType().GetProperty("items")?.GetValue(response.Data, null) as List<ProductSearchResultDto>;
            Assert.Equal(expectedCount, items?.Count ?? 0);
        }

        // =====================================
        // 2. CREATE PRODUCT
        // =====================================

        [Theory]
        [InlineData("DUP_CODE", "NewName", "exists")] // Duplicate Code
        [InlineData("", "NewName", "productCode is required")] // Empty Code
        [InlineData("Code1", "", "productName is required")] // Empty Name
        public async Task CreateProduct_InvalidData_ReturnsBadRequest(string code, string name, string expectedError)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var existingId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = existingId, ProductCode = "DUP_CODE", ProductName = "DUP_NAME", Status = 1 });
            context.BranchProducts.Add(new BranchProduct { BranchId = branchId, ProductId = existingId, IsActive = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, branchId);
            
            if (string.IsNullOrEmpty(code)) controller.ModelState.AddModelError("ProductCode", "Mã sản phẩm là bắt buộc");
            if (string.IsNullOrEmpty(name)) controller.ModelState.AddModelError("ProductName", "Tên sản phẩm là bắt buộc");

            var request = new ProductUpsertDto { ProductCode = code, ProductName = name, Unit = "Cái", CostPrice = 10, SalePrice = 20 };
            var result = await controller.CreateProduct(request);
            
            var badRequest = Assert.IsAssignableFrom<ObjectResult>(result);
            var serialized = System.Text.Json.JsonSerializer.Serialize(badRequest.Value);
            Assert.Contains(expectedError, serialized, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CreateProduct_ValidData_CreatesSuccessfully()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId);
            
            var request = new ProductUpsertDto { ProductCode = "DAY-1", ProductName = "Day", Unit = "Cuộn", CostPrice = 10, SalePrice = 20 };
            _mockMapper.Setup(m => m.Map<Product>(It.IsAny<ProductUpsertDto>())).Returns(new Product { ProductCode = "DAY-1", ProductName = "Day" });

            var result = await controller.CreateProduct(request);
            var created = Assert.IsType<CreatedAtActionResult>(result);
            Assert.True(((ApiResponse<ProductUpsertDto>)created.Value!).Success);
        }

        // =====================================
        // 3. UPDATE PRODUCT
        // =====================================

        [Fact]
        public async Task UpdateProduct_NotFound_ReturnsNotFound()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());
            var result = await controller.UpdateProduct(Guid.NewGuid(), new UpdateProductDto());
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Theory]
        [InlineData("DUP_CODE", "NewName", "exists")] // Target code overlaps with existing
        public async Task UpdateProduct_InvalidConditions_ReturnsAppropriateResult(string newCode, string newName, string expectedError)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            
            context.Products.Add(new Product { ProductId = targetId, ProductCode = "OLD", ProductName = "OLD_NAME" });
            context.BranchProducts.Add(new BranchProduct { ProductId = targetId, BranchId = branchId, IsActive = 1 });
            // Conflicting product
            context.Products.Add(new Product { ProductId = Guid.NewGuid(), ProductCode = "DUP_CODE" });
            context.BranchProducts.Add(new BranchProduct { ProductId = context.Products.Local.Last().ProductId, BranchId = branchId, IsActive = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, branchId);
            var request = new UpdateProductDto { ProductCode = newCode, ProductName = newName, Unit = "Cái", CostPrice = 10, SalePrice = 20 };

            var result = await controller.UpdateProduct(targetId, request);
            
            var badRequest = Assert.IsAssignableFrom<ObjectResult>(result);
            var serialized = System.Text.Json.JsonSerializer.Serialize(badRequest.Value);
            Assert.Contains(expectedError, serialized, StringComparison.OrdinalIgnoreCase);
        }

        // =====================================
        // 4. DELETE PRODUCT
        // =====================================

        [Fact]
        public async Task DeleteProduct_NotFound_ReturnsNotFound()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());
            var result = await controller.DeleteProduct(Guid.NewGuid());
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task DeleteProduct_NoDependencies_PerformsHardDelete()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var targetId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = targetId, Status = 1 });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = targetId, IsActive = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, branchId);
            var result = await controller.DeleteProduct(targetId);
            Assert.IsType<OkObjectResult>(result);
            
            var count = await context.BranchProducts.CountAsync();
            Assert.Equal(0, count); // Verify hard delete
        }

        // =====================================
        // 5. SUGGEST PRODUCT
        // =====================================

        [Theory]
        [InlineData("Đinh", "ĐINH")] // 0 matches, start with 001 without padding if logic dictates (baseCode) -> Actually, logic says "ĐINH" and padding starts when duplicate!
        [InlineData("Ống nhựa", "ỐNG_NHỰA002")] // 1 match existing, jump to 002
        public async Task SuggestNewProduct_ReturnsFormattedCode(string inputName, string expectedCode)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            if (inputName == "Ống nhựa")
            {
                var pid = Guid.NewGuid();
                context.Products.Add(new Product { ProductId = pid, ProductCode = "ỐNG_NHỰA" });
                context.BranchProducts.Add(new BranchProduct { BranchId = branchId, ProductId = pid });
                await context.SaveChangesAsync();
            }

            var controller = CreateController(context, branchId);
            var result = await controller.SuggestNewProduct(inputName);
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            var suggestion = response.Data as NewProductSuggestionDto;
            
            Assert.Equal(expectedCode, suggestion?.SuggestedCode);
        }



    }
}
