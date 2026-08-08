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

        private ProductTaxonomiesController CreateController(MEPDatabaseContext context, Guid? userId = null, string role = "Admin")
        {
            var controller = new ProductTaxonomiesController(context);

            var claims = new List<Claim>();
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
        public async Task CreateCategory_ValidData_CreatesCategory()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            var request = new CreateCategoryDto
            {
                CategoryName = "Thiết bị điện tử",
                Description = "Điện thoại, Laptop, Phụ kiện"
            };

            // Act
            var result = await controller.CreateCategory(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<CategoryDto>>(createdResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Equal("Thiết bị điện tử", apiResponse.Data.CategoryName);
            
            var dbCategory = await context.ProductCategories.FirstOrDefaultAsync(c => c.CategoryName == "Thiết bị điện tử");
            Assert.NotNull(dbCategory);
        }
    }
}
