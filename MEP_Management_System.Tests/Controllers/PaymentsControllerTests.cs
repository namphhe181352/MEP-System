using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.SaleController;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class PaymentsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public PaymentsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private PaymentsController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var controller = new PaymentsController(context);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
                claims.Add(new Claim("sub", userId.Value.ToString()));
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

        // ==========================================
        // TEST POST /api/payments
        // ==========================================
        [Fact]
        public async Task ProcessPayment_ValidInvoiceAndAmount_UpdatesInvoiceStatusToPaid()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var orderId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            
            context.Orders.Add(new Order 
            { 
                OrderId = orderId, 
                OrderCode = "HD-001", 
                BranchId = branchId, 
                UserId = userId, 
                Status = "Draft",
                TotalAmount = 500000, 
                FinalAmount = 500000 
            });

            context.Invoices.Add(new Invoice 
            { 
                InvoiceId = invoiceId, 
                InvoiceCode = "INV-001", 
                OrderId = orderId, 
                Status = "Draft", 
                TotalAmount = 500000, 
                FinalAmount = 500000,
                UserId = userId 
            });
            await context.SaveChangesAsync();

            var request = new PaymentProcessRequest
            {
                InvoiceId = invoiceId,
                Amount = 500000,
                PaymentMethod = "CASH" // Tiền mặt
            };

            // Act
            var result = await controller.ProcessPayment(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var paymentResult = Assert.IsType<PaymentResultDto>(okResult.Value);
            
            Assert.True(paymentResult.Success);
            
            var invoiceInDb = await context.Invoices.FindAsync(invoiceId);
            Assert.Equal("Completed", invoiceInDb.Status);
            
            var orderInDb = await context.Orders.FindAsync(orderId);
            Assert.Equal("Completed", orderInDb.Status); // The corresponding order must also be marked completed
            
            var transaction = await context.PaymentTransactions.FirstOrDefaultAsync(pt => pt.InvoiceId == invoiceId);
            Assert.NotNull(transaction);
            Assert.Equal("CASH", transaction.PaymentMethod);
            Assert.Equal(500000, transaction.Amount);
        }

        [Fact]
        public async Task ProcessPayment_InsufficientAmount_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var orderId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            
            context.Orders.Add(new Order { OrderId = orderId, TotalAmount = 500000, FinalAmount = 500000 });
            context.Invoices.Add(new Invoice { InvoiceId = invoiceId, OrderId = orderId, Status = "Draft", TotalAmount = 500000, FinalAmount = 500000 });
            await context.SaveChangesAsync();

            var request = new PaymentProcessRequest
            {
                InvoiceId = invoiceId,
                Amount = 300000, // Short by 200,000
                PaymentMethod = "CARD"
            };

            // Act
            var result = await controller.ProcessPayment(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(badRequestResult.Value);
            
            Assert.Contains("Số tiền thanh toán không đủ", errorResponse.Error.Message);
        }
    }
}
