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

namespace MEP_Management_System.Tests.Controllers
{
    public class ProductsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<ICurrentBranchAccessor> _mockBranchAccessor;
        private Mock<IMapper> _mockMapper;

        public ProductsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            
            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
            _mockMapper = new Mock<IMapper>();
        }

        private ProductsController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null, string role = "Owner")
        {
            var controller = new ProductsController(context, _mockMapper.Object, _mockBranchAccessor.Object);

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

            if (branchId.HasValue)
            {
                _mockBranchAccessor.Setup(m => m.GetBranchId()).Returns(branchId.Value);
            }
            else
            {
                _mockBranchAccessor.Setup(m => m.GetBranchId()).Returns((Guid?)null);
            }

            return controller;
        }

        [Fact]
        public async Task GetProducts_NoValidBranch_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid(), null); // branchId is null

            var queryParams = new ProductParameters { PageSize = 10, PageNumber = 1 };

            // Act
            var result = await controller.GetProducts(queryParams);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("Không tìm thấy chi nhánh hợp lệ.", apiResponse.Message);
        }

        [Fact]
        public async Task GetProducts_InvalidPagination_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid(), Guid.NewGuid());

            var queryParams = new ProductParameters { PageSize = 0, PageNumber = 1 }; // Invalid PageSize

            // Act
            var result = await controller.GetProducts(queryParams);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("Invalid pagination parameters", apiResponse.Message);
        }

        [Fact]
        public async Task GetProducts_ValidData_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, Guid.NewGuid(), branchId);

            var product = new Product { ProductId = Guid.NewGuid(), ProductName = "Test Product", ProductCode = "P001", BaseUnit = "Cái" };
            context.Products.Add(product);
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = product.ProductId, Product = product });
            await context.SaveChangesAsync();

            var queryParams = new ProductParameters { PageSize = 10, PageNumber = 1 };

            _mockMapper.Setup(m => m.Map<IEnumerable<ProductUpsertDto>>(It.IsAny<IEnumerable<BranchProduct>>()))
                       .Returns(new List<ProductUpsertDto> { new ProductUpsertDto { ProductName = "Test Product", ProductCode = "P001" } });

            // Act
            var result = await controller.GetProducts(queryParams);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<PaginatedResponse<ProductUpsertDto>>>(okResult.Value);
            Assert.True(apiResponse.Success);
        }
    }
}
