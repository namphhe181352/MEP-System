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

namespace MEP_Management_System.UnitTests.Controllers.InventoryController.Supplier
{
    public class SupplierDebtControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<ISupplierDebtService> _mockSupplierDebtService;

        public SupplierDebtControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockSupplierDebtService = new Mock<ISupplierDebtService>();
        }

        private SupplierDebtController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var controller = new SupplierDebtController(context, _mockSupplierDebtService.Object);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            }
            if (branchId.HasValue)
            {
                claims.Add(new Claim("branchId", branchId.Value.ToString()));
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
        public async Task GetSupplierDebts_ReturnsCalculatedDebts()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();

            context.Suppliers.Add(new MEP_Management_System.Models.Supplier { SupplierId = supplierId, SupplierName = "Công ty Cổ phần Nhựa Bình Minh", Status = 1 });
            
            // Add opening debt (last month PO)
            var lastMonth = DateTime.UtcNow.AddMonths(-1);
            context.PurchaseOrders.Add(new PurchaseOrder 
            { 
                PurchaseOrderId = Guid.NewGuid(), 
                SupplierId = supplierId, 
                TotalAmount = 55000000, 
                Status = "COMPLETED",
                CreatedAt = lastMonth
            });

            // Add payment in current month
            context.SupplierPayments.Add(new SupplierPayment
            {
                PaymentId = Guid.NewGuid(),
                SupplierId = supplierId,
                Amount = 20000000,
                PaymentDate = DateTime.UtcNow,
                IsDeleted = 0
            });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);
            var query = new SupplierDebtParameters { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await controller.GetSupplierDebts(query);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<SupplierDebtPagedResponse>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.Single(response.Data.Items);
            
            var debt = response.Data.Items[0];
            Assert.Equal("Công ty Cổ phần Nhựa Bình Minh", debt.Name);
            Assert.Equal(55000000, debt.OpeningDebt);
            Assert.Equal(0, debt.PurchasedInPeriod);
            Assert.Equal(20000000, debt.PaidInPeriod);
            Assert.Equal(35000000, debt.ClosingDebt);
        }

        [Fact]
        public async Task GetSupplierDebtDetail_ValidId_ReturnsDetail()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();

            var mockDetail = new SupplierDebtDetailResponse
            {
                SupplierId = supplierId,
                SupplierName = "Nhà phân phối điện cơ Trần Phú"
            };

            _mockSupplierDebtService.Setup(s => s.GetSupplierDebtDetailAsync(supplierId, It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(mockDetail);

            var controller = CreateController(context, userId);

            // Act
            var result = await controller.GetSupplierDebtDetail(supplierId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<SupplierDebtDetailResponse>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.Equal("Nhà phân phối điện cơ Trần Phú", response.Data.SupplierName);
        }
        
        [Fact]
        public async Task ExportCsv_ReturnsFileResult()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();

            context.Suppliers.Add(new MEP_Management_System.Models.Supplier { SupplierId = supplierId, SupplierName = "Đại lý Rạng Đông", Status = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);
            var query = new SupplierDebtParameters { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await controller.ExportCsv(query);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/csv", fileResult.ContentType);
            Assert.StartsWith("cong-no-ncc-", fileResult.FileDownloadName);
            
            var csvContent = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
            Assert.Contains("Đại lý Rạng Đông", csvContent);
        }
    }
}
