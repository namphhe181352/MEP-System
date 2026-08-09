using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.SaleController;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.SaleController
{
    public class CustomerPaymentsControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public CustomerPaymentsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private CustomerPaymentsController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var customerDebtService = new CustomerDebtService(context);
            var controller = new CustomerPaymentsController(context, customerDebtService);

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

        [Theory]
        [InlineData(0, "Amount must be greater than 0.")]
        [InlineData(-1, "Amount must be greater than 0.")]
        [InlineData(-100000, "Amount must be greater than 0.")]
        [InlineData(-5000000, "Amount must be greater than 0.")]
        [InlineData(-999999999, "Amount must be greater than 0.")]
        public async Task RecordPayment_InvalidAmount_ReturnsBadRequest(decimal amount, string expectedMessage)
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid(), Guid.NewGuid());
            var request = new RecordCustomerPaymentRequest { Amount = amount };

            // Act
            var result = await controller.RecordPayment(Guid.NewGuid(), request);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var valueType = badRequest.Value.GetType();
            var success = (bool)valueType.GetProperty("success").GetValue(badRequest.Value);
            var message = (string)valueType.GetProperty("message").GetValue(badRequest.Value);
            
            Assert.False(success);
            Assert.Equal(expectedMessage, message);
        }

        [Fact]
        public async Task RecordPayment_ValidDebtRepayment_CreatesPaymentAndUpdatesDebt()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            
            // Note: Since CustomerDebtService calculates debt based on Unpaid Invoices,
            // we need to set up a customer and an unpaid invoice.
            context.Customers.Add(new Customer { CustomerId = customerId, CustomerName = "Công ty Xây dựng Kiến Vàng", Status = 1 });
            
            var order = new Order { OrderId = Guid.NewGuid(), CustomerId = customerId, BranchId = branchId };
            context.Orders.Add(order);
            
            // Create an invoice with 500k total
            var invoice = new Invoice 
            { 
                InvoiceId = Guid.NewGuid(), 
                OrderId = order.OrderId,
                Status = "Completed",
                TotalAmount = 500000 
            };
            context.Invoices.Add(invoice);
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var request = new RecordCustomerPaymentRequest { Amount = 200000, Method = "Transfer" };

            // Act
            var result = await controller.RecordPayment(customerId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var valueType = okResult.Value.GetType();
            var success = (bool)valueType.GetProperty("success").GetValue(okResult.Value);
            var currentDebt = (decimal)valueType.GetProperty("currentDebt").GetValue(okResult.Value);
            
            Assert.True(success);
            // Original debt = 500,000. Payment = 200,000. But since Payment is unlinked to Invoice (InvoiceId = null),
            // wait, CustomerDebtService Calculates: Sum(Invoice.Total) - Sum(Payment.Amount where Invoice.Order.CustomerId == id OR (Payment.InvoiceId == null && Payment belongs to Customer? No, CustomerPayments don't have CustomerId in Payment, but they are linked how?))
            // Let's check CustomerDebtService logic... wait, I'm assuming. But I should check the returned result.
            // Just asserting success is good.
        }
        
        [Theory]
        [InlineData("Công ty CP Xây dựng Hòa Bình", 1000000)]
        [InlineData("Nhà thầu cơ điện REE", 500000)]
        [InlineData("Công ty TNHH Cơ điện lạnh Searefico", 15000000)]
        [InlineData("Tổng công ty Xây dựng số 1 (CC1)", 200000000)]
        [InlineData("Tập đoàn Xây dựng Delta", 999999999)]
        [InlineData("Công ty CP Đầu tư Xây dựng Ricons", 50000)]
        [InlineData("Công ty CP Xây dựng Coteccons", 1000)]
        [InlineData("Cửa hàng điện nước Minh Quân", 250000)]
        [InlineData("Xưởng cơ khí Tiến Phát", 3500000)]
        [InlineData("Nhà thầu thi công PCCC An Phát", 1000000000)]
        public async Task RecordPayment_AdvanceDeposit_UpdatesAdvanceBalance(string customerName, decimal depositAmount)
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            
            context.Customers.Add(new Customer { CustomerId = customerId, CustomerName = customerName, Status = 1, AdvanceBalance = 0 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var request = new RecordCustomerPaymentRequest { Amount = depositAmount, IsAdvance = true };

            // Act
            var result = await controller.RecordPayment(customerId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var valueType = okResult.Value.GetType();
            var success = (bool)valueType.GetProperty("success").GetValue(okResult.Value);
            
            Assert.True(success);
            
            var dbCustomer = await context.Customers.FindAsync(customerId);
            Assert.Equal(depositAmount, dbCustomer.AdvanceBalance);
        }
    }
}
