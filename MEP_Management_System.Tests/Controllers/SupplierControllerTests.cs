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

        private SupplierController CreateController(MEPDatabaseContext context, Guid? userId = null, string role = "Owner")
        {
            var controller = new SupplierController(context);

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

        // ==========================================
        // TEST GET /api/suppliers
        // ==========================================
        [Fact]
        public async Task GetSuppliers_WithRealVietnameseData_ReturnsPaginatedList()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            context.Suppliers.Add(new Supplier { SupplierId = Guid.NewGuid(), SupplierName = "Nhà Phân Phối Masan", SupplierCode = "NCC-MS", Phone = "02877778888", IsActive = 1 });
            context.Suppliers.Add(new Supplier { SupplierId = Guid.NewGuid(), SupplierName = "Công ty Cổ phần Sữa Vinamilk", SupplierCode = "NCC-VNM", Phone = "19001568", IsActive = 1 });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetSuppliers(search: "Vinamilk", pageNumber: 1, pageSize: 10);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<PaginatedResponse<SupplierDto>>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Single(apiResponse.Data.Items);
            Assert.Equal("Công ty Cổ phần Sữa Vinamilk", apiResponse.Data.Items.First().SupplierName);
        }

        // ==========================================
        // TEST POST /api/suppliers
        // ==========================================
        [Fact]
        public async Task CreateSupplier_ValidData_ReturnsCreatedResult()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            var request = new CreateSupplierDto
            {
                SupplierName = "Đại lý Cấp 1 Bia Heineken",
                SupplierCode = "NCC-HEI",
                Phone = "0988111222",
                Email = "sales@heineken-dl1.vn",
                TaxCode = "0312456789",
                Address = "KCN Tân Bình, TPHCM"
            };

            // Act
            var result = await controller.CreateSupplier(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<SupplierDto>>(createdResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Equal("Đại lý Cấp 1 Bia Heineken", apiResponse.Data.SupplierName);
            
            var dbSupplier = await context.Suppliers.FirstOrDefaultAsync(s => s.SupplierCode == "NCC-HEI");
            Assert.NotNull(dbSupplier);
            Assert.Equal("0312456789", dbSupplier.TaxCode);
        }

        [Fact]
        public async Task CreateSupplier_DuplicateCode_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());
            
            // Seed existing
            context.Suppliers.Add(new Supplier { SupplierId = Guid.NewGuid(), SupplierName = "Nhà Phân Phối Cũ", SupplierCode = "NCC-OLD", IsActive = 1 });
            await context.SaveChangesAsync();

            var request = new CreateSupplierDto
            {
                SupplierName = "Nhà Phân Phối Mới",
                SupplierCode = "NCC-OLD", // Duplicate
                Phone = "0999888777"
            };

            // Act
            var result = await controller.CreateSupplier(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            
            Assert.False(apiResponse.Success);
            Assert.Contains("đã tồn tại", apiResponse.Message.ToLower());
        }
    }
}
