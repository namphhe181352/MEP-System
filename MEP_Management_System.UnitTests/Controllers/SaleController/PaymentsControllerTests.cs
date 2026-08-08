using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Controllers.SaleController;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.SaleController
{
    public class PaymentsControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<VietQrService> _mockVietQrService;

        public PaymentsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockMapper = new Mock<IMapper>();
            
            var mockHandler = new Mock<System.Net.Http.HttpMessageHandler>();
            var client = new System.Net.Http.HttpClient(mockHandler.Object);
            var options = Microsoft.Extensions.Options.Options.Create(new MEP_Management_System.DTOs.SaleDTOs.VietQrOptions { ApiBase = "https://api.vietqr.io" });
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<VietQrService>>().Object;
            
            _mockVietQrService = new Mock<VietQrService>(client, options, logger);
        }

        private PaymentsController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var mockMepHub = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<MEP_Management_System.Hubs.MepHub>>();
            var mockPosHub = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<MEP_Management_System.Hubs.PosHub>>();
            var mockNotifications = new Mock<MepNotificationService>(mockMepHub.Object, mockPosHub.Object);
            var controller = new PaymentsController(context, _mockMapper.Object, mockNotifications.Object, _mockVietQrService.Object);

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
        public async Task CreatePayment_CashValidData_CreatesPayment()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            
            context.Orders.Add(new Order { OrderId = orderId, BranchId = branchId, DiscountAmount = 0 });
            context.Invoices.Add(new Invoice
            {
                InvoiceId = invoiceId,
                OrderId = orderId,
                Status = "Draft",
                InvoiceItems = new List<InvoiceItem>
                {
                    new InvoiceItem { Quantity = 2, SellPrice = 100000 } // Total 200k
                }
            });
            await context.SaveChangesAsync();

            _mockMapper.Setup(m => m.Map<PaymentDto>(It.IsAny<Payment>()))
                .Returns((Payment p) => new PaymentDto { PaymentId = p.PaymentId, Amount = p.Amount, Status = p.Status });

            var controller = CreateController(context, userId, branchId);
            var request = new CreatePaymentRequest 
            { 
                Method = PaymentMethod.Cash, 
                Amount = 200000, 
                CashReceived = 500000 
            };

            // Act
            var result = await controller.CreatePayment(invoiceId, request);

            // Assert
            var createdResult = Assert.IsType<CreatedResult>(result.Result);
            var dto = Assert.IsType<PaymentDto>(createdResult.Value);
            
            var dbPayment = await context.Payments.FindAsync(dto.PaymentId);
            Assert.NotNull(dbPayment);
            Assert.Equal("Confirmed", dbPayment.Status);
            Assert.Equal(200000, dbPayment.Amount);
            Assert.Equal(300000, dbPayment.ChangeAmount);
        }

        [Fact]
        public async Task CreatePayment_ExceedsTotal_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            
            context.Orders.Add(new Order { OrderId = orderId, BranchId = branchId, DiscountAmount = 0 });
            context.Invoices.Add(new Invoice
            {
                InvoiceId = invoiceId,
                OrderId = orderId,
                Status = "Draft",
                InvoiceItems = new List<InvoiceItem>
                {
                    new InvoiceItem { Quantity = 1, SellPrice = 150000 } // Total 150k
                }
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var request = new CreatePaymentRequest 
            { 
                Method = PaymentMethod.Cash, 
                Amount = 200000, // Exceeds 150k
                CashReceived = 200000 
            };

            // Act
            var result = await controller.CreatePayment(invoiceId, request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var response = Assert.IsType<MEP_Management_System.DTOs.SaleDTOs.ApiErrorResponse>(badRequestResult.Value);
            Assert.Contains("exceed invoice total", response.Error.Message);
        }

        [Fact]
        public async Task ConfirmTransfer_PendingPayment_ConfirmsPayment()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var paymentId = Guid.NewGuid();
            
            context.Payments.Add(new Payment
            {
                PaymentId = paymentId,
                Method = PaymentMethod.Transfer,
                Status = "Pending",
                Amount = 150000,
                QRExpiredAt = DateTime.UtcNow.AddMinutes(5),
                BranchId = branchId
            });
            await context.SaveChangesAsync();

            _mockMapper.Setup(m => m.Map<PaymentDto>(It.IsAny<Payment>()))
                .Returns((Payment p) => new PaymentDto { PaymentId = p.PaymentId, Status = p.Status });

            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.ConfirmTransfer(paymentId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            
            var dbPayment = await context.Payments.FindAsync(paymentId);
            Assert.Equal("Confirmed", dbPayment.Status);
        }
    }
}
