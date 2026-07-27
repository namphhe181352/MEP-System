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
    public class InventoryProductsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private IMapper _mapper;

        public InventoryProductsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var config = new MapperConfiguration(cfg =>
            {
                // Basic mock mapping
                cfg.CreateMap<ProductUpsertDto, Product>()
                    .ForMember(dest => dest.BaseUnit, opt => opt.MapFrom(src => src.Unit));
                cfg.CreateMap<Product, ProductUpsertDto>()
                    .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => src.BaseUnit));
            });
            _mapper = config.CreateMapper();
        }

        private ProductsController CreateController(MEPDatabaseContext context, Guid branchId, Guid? userId = null)
        {
            var mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
            mockBranchAccessor.Setup(m => m.GetBranchId()).Returns(branchId);

            var controller = new ProductsController(context, _mapper, mockBranchAccessor.Object);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
                claims.Add(new Claim("userId", userId.Value.ToString()));
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
            var controller = CreateController(context, branchId);

            var product = new Product { ProductId = Guid.NewGuid(), ProductCode = "P001", ProductName = "Test Product", BaseUnit = "Cái" };
            context.Products.Add(product);
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = product.ProductId, Product = product, IsActive = 1 });
            await context.SaveChangesAsync();

            var queryParams = new ProductParameters { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await controller.GetProducts(queryParams);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<PaginatedResponse<ProductUpsertDto>>>(okResult.Value);
            Assert.True(response.Success);
            Assert.Single(response.Data!.Items);
        }

        [Fact]
        public async Task CreateProduct_ValidData_ReturnsCreated()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId, Guid.NewGuid());

            var dto = new ProductUpsertDto 
            { 
                ProductCode = "P002", 
                ProductName = "New Product", 
                Unit = "Cái",
                ActualStock = 10,
                AvailableStock = 10
            };

            // Act
            var result = await controller.CreateProduct(dto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var response = Assert.IsType<ApiResponse<ProductUpsertDto>>(createdResult.Value);
            Assert.True(response.Success);
            
            var savedProduct = await context.Products.FirstOrDefaultAsync(p => p.ProductCode == "P002");
            Assert.NotNull(savedProduct);
        }

        [Fact]
        public async Task DeleteProduct_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId);

            var product = new Product { ProductId = Guid.NewGuid(), ProductCode = "P003", ProductName = "Test", Status = 1, BaseUnit = "Cái" };
            var branchProduct = new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = product.ProductId, Product = product, IsActive = 1 };
            
            context.Products.Add(product);
            context.BranchProducts.Add(branchProduct);
            await context.SaveChangesAsync();

            // Act
            var result = await controller.DeleteProduct(product.ProductId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.True(response.Success);

            var updatedBp = await context.BranchProducts.FindAsync(branchProduct.BranchProductId);
            Assert.Equal((short)0, updatedBp!.IsActive);
        }
    }
}
