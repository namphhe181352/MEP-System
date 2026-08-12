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

namespace MEP_Management_System.UnitTests.Controllers.InventoryController.Supplier
{
    public class SupplierPaymentControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public SupplierPaymentControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private SupplierPaymentController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new SupplierPaymentController(context);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            }
            claims.Add(new Claim(ClaimTypes.Role, "Owner"));

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            return controller;
        }

        [Fact]
        public async Task GetAll_ReturnsPayments()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();

            context.Users.Add(new User { UserId = userId, Username = "test" });
            context.Suppliers.Add(new MEP_Management_System.Models.Supplier { SupplierId = supplierId, SupplierName = "Supplier A" });

            context.SupplierPayments.Add(new SupplierPayment
            {
                PaymentId = Guid.NewGuid(),
                SupplierId = supplierId,
                RecordedBy = userId,
                Amount = 15000000,
                PaymentDate = DateTime.UtcNow,
                IsDeleted = 0
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);

            // Act
            var result = await controller.GetAll(supplierId, 1, 10);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            
            Assert.True(response.Success);
            
            var dataProp = response.Data.GetType().GetProperty("Items");
            var items = dataProp.GetValue(response.Data, null) as List<SupplierPaymentResponseDto>;
            
            Assert.NotNull(items);
            Assert.Single(items);
            Assert.Equal(15000000, items[0].Amount);
        }

        [Fact]
        public async Task Create_ValidData_CreatesPayment()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId);

            var request = new CreateSupplierPaymentDto
            {
                SupplierId = Guid.NewGuid(),
                Amount = 25000000,
                PaymentMethod = "Chuyển khoản",
                Note = "Thanh toán tiền nhập cáp điện Trần Phú tháng 8"
            };

            // Act
            var result = await controller.Create(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.Contains("Ghi nhận thanh toán", response.Message);

            var dbPayment = await context.SupplierPayments.FirstOrDefaultAsync(p => p.SupplierId == request.SupplierId);
            Assert.NotNull(dbPayment);
            Assert.Equal(25000000, dbPayment.Amount);
            Assert.Equal(userId, dbPayment.RecordedBy);
        }

        [Fact]
        public async Task Delete_ValidId_PerformsSoftDelete()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var paymentId = Guid.NewGuid();

            context.SupplierPayments.Add(new SupplierPayment
            {
                PaymentId = paymentId,
                Amount = 10000000,
                IsDeleted = 0
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);

            // Act
            var result = await controller.Delete(paymentId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var dbPayment = await context.SupplierPayments.FindAsync(paymentId);
            Assert.NotNull(dbPayment);
            Assert.Equal(1, dbPayment.IsDeleted); // Soft deleted
        }
    }
}
