using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.InventoryController.Supplier;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class SupplierDebtControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public SupplierDebtControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private SupplierDebtController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new SupplierDebtController(context);

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
        public async Task GetSupplierDebts_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var supplier = new Supplier { SupplierId = Guid.NewGuid(), SupplierName = "Test Supplier", Status = 1 };
            context.Suppliers.Add(supplier);
            
            var branchId = Guid.NewGuid();
            var branch = new Branch { BranchId = branchId, BranchName = "Test Branch" };
            context.Branches.Add(branch);

            context.PurchaseOrders.Add(new PurchaseOrder 
            { 
                PurchaseOrderId = Guid.NewGuid(), 
                SupplierId = supplier.SupplierId, 
                Status = "APPROVED", 
                TotalAmount = 1000,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var queryParams = new SupplierDebtParameters { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await controller.GetSupplierDebts(queryParams);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<SupplierDebtPagedResponse>>(okResult.Value);
            Assert.True(response.Success);
            Assert.Single(response.Data!.Items);
            Assert.Equal("Test Supplier", response.Data.Items.First().Name);
            Assert.Equal(1000, response.Data.Items.First().ClosingDebt);
        }

        [Fact]
        public async Task ExportCsv_ReturnsFile()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var supplier = new Supplier { SupplierId = Guid.NewGuid(), SupplierName = "Test Supplier", Status = 1 };
            context.Suppliers.Add(supplier);
            await context.SaveChangesAsync();

            var queryParams = new SupplierDebtParameters { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await controller.ExportCsv(queryParams);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/csv", fileResult.ContentType);
            Assert.Contains("cong-no-ncc-", fileResult.FileDownloadName);
        }
    }
}
