using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.InventoryController;
using MEP_Management_System.DTOs;
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
    public class ProductTaxonomiesControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public ProductTaxonomiesControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private ProductTaxonomiesController CreateController(MEPDatabaseContext context, Guid branchId, Guid? userId = null)
        {
            var mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
            mockBranchAccessor.Setup(m => m.GetBranchId()).Returns(branchId);

            var controller = new ProductTaxonomiesController(context, mockBranchAccessor.Object);

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

        [Fact]
        public async Task GetTaxonomies_ReturnsTaxonomies()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId);

            context.ProductTaxonomies.Add(new ProductTaxonomy { TaxonomyId = Guid.NewGuid(), Name = "Test Brand", Type = "Brand", BranchId = branchId });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetTaxonomies("Brand");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<List<ProductTaxonomyDto>>>(okResult.Value);
            Assert.True(response.Success);
            Assert.Single(response.Data!);
            Assert.Equal("Test Brand", response.Data.First().Name);
        }

        [Fact]
        public async Task CreateTaxonomy_ValidData_ReturnsCreatedAtAction()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId);

            var dto = new CreateProductTaxonomyDto { Name = "New Category", Type = "Category" };

            // Act
            var result = await controller.CreateTaxonomy(dto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var response = Assert.IsType<ApiResponse<ProductTaxonomyDto>>(createdResult.Value);
            Assert.True(response.Success);
            Assert.Equal("New Category", response.Data!.Name);
        }

        [Fact]
        public async Task UpdateTaxonomy_ValidData_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId);
            var taxonomyId = Guid.NewGuid();

            context.ProductTaxonomies.Add(new ProductTaxonomy { TaxonomyId = taxonomyId, Name = "Old Name", Type = "Brand", BranchId = branchId });
            await context.SaveChangesAsync();

            var dto = new UpdateProductTaxonomyDto { Name = "New Name" };

            // Act
            var result = await controller.UpdateTaxonomy(taxonomyId, dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<ProductTaxonomyDto>>(okResult.Value);
            Assert.True(response.Success);
            Assert.Equal("New Name", response.Data!.Name);
        }

        [Fact]
        public async Task DeleteTaxonomy_ValidData_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId);
            var taxonomyId = Guid.NewGuid();

            context.ProductTaxonomies.Add(new ProductTaxonomy { TaxonomyId = taxonomyId, Name = "Test Brand", Type = "Brand", BranchId = branchId });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.DeleteTaxonomy(taxonomyId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.Contains("Xóa thành công", apiResponse.Message);

            var deletedTaxonomy = await context.ProductTaxonomies.FindAsync(taxonomyId);
            Assert.Null(deletedTaxonomy);
        }
    }
}
