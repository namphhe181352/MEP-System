using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.InventoryController.Supplier;
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
    public class SupplierControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public SupplierControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private SupplierController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var mockDebtService = new Mock<ISupplierDebtService>();
            mockDebtService.Setup(s => s.CalculateCurrentDebtBatchAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, SupplierDebtSummary>());

            var controller = new SupplierController(context, mockDebtService.Object);

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
        public async Task GetSuppliers_ReturnsSuppliers()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            context.Suppliers.Add(new Supplier { SupplierId = Guid.NewGuid(), SupplierName = "Test Supplier", Status = 1 });
            await context.SaveChangesAsync();

            var queryParams = new SupplierQueryParameters { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await controller.GetSuppliers(queryParams);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<SupplierPagedResponse>>(okResult.Value);
            Assert.True(response.Success);
            Assert.Single(response.Data!.Items);
            Assert.Equal("Test Supplier", response.Data.Items.First().Name);
        }

        [Fact]
        public async Task CreateSupplier_ValidData_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            var dto = new SupplierUpsertRequest { SupplierName = "New Supplier", Status = 1 };

            // Act
            var result = await controller.CreateSupplier(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<SupplierDetailDto>>(okResult.Value);
            Assert.True(response.Success);
            Assert.Equal("New Supplier", response.Data!.Name);

            var savedSupplier = await context.Suppliers.FirstOrDefaultAsync(s => s.SupplierName == "New Supplier");
            Assert.NotNull(savedSupplier);
        }

        [Fact]
        public async Task UpdateSupplier_ValidData_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());
            var supplierId = Guid.NewGuid();

            context.Suppliers.Add(new Supplier { SupplierId = supplierId, SupplierName = "Old Name", Status = 1 });
            await context.SaveChangesAsync();

            var dto = new SupplierUpsertRequest { SupplierName = "New Name", Status = 1 };

            // Act
            var result = await controller.UpdateSupplier(supplierId, dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<SupplierDetailDto>>(okResult.Value);
            Assert.True(response.Success);
            Assert.Equal("New Name", response.Data!.Name);
        }

        [Fact]
        public async Task DeleteSupplier_ValidData_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);
            var supplierId = Guid.NewGuid();

            context.Suppliers.Add(new Supplier { SupplierId = supplierId, SupplierName = "Test", Status = 1 });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.DeleteSupplier(supplierId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.True(response.Success);

            var deleted = await context.Suppliers.FindAsync(supplierId);
            Assert.Null(deleted);
        }
    }
}
