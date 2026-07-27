using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Controllers.SaleController;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Hubs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class PaymentsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<IMapper> _mockMapper;
        private Mock<IHubContext<MepHub>> _mockMepHub;
        private Mock<IHubContext<PosHub>> _mockPosHub;
        private MepNotificationService _notificationService;
        private VietQrService _vietQrService;

        public PaymentsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockMapper = new Mock<IMapper>();

            _mockMepHub = new Mock<IHubContext<MepHub>>();
            _mockPosHub = new Mock<IHubContext<PosHub>>();

            var mockClientProxy = new Mock<IClientProxy>();
            var mockClients = new Mock<IHubClients>();
            mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
            _mockPosHub.Setup(h => h.Clients).Returns(mockClients.Object);

            _notificationService = new MepNotificationService(_mockMepHub.Object, _mockPosHub.Object);

            var options = Options.Create(new VietQrOptions { ApiBase = "http://test", ClientId = "test", ApiKey = "test", AccountNumber = "123", AccountName = "Test", BankCode = "VCB", TemplateId = "qr_only" });
            var mockLogger = new Mock<ILogger<VietQrService>>();
            _vietQrService = new VietQrService(new HttpClient(), options, mockLogger.Object);
        }

        private PaymentsController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var controller = new PaymentsController(context, _mockMapper.Object, _notificationService, _vietQrService);

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

        [Fact]
        public async Task CreatePayment_InvoiceNotDraft_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var invoiceId = Guid.NewGuid();
            var order = new Order { OrderId = Guid.NewGuid(), OrderCode = "ORD1", BranchId = branchId, Status = "Draft" };
            var invoice = new Invoice { InvoiceId = invoiceId, InvoiceCode = "INV1", Status = "Completed", Order = order };
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();

            var request = new CreatePaymentRequest { Method = PaymentMethod.Cash, Amount = 1000, CashReceived = 1000 };

            // Act
            var result = await controller.CreatePayment(invoiceId, request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(badRequestResult.Value);
            Assert.Equal("MSG-78", errorResponse.Error.Code);
            Assert.Equal("Invoice already finalized", errorResponse.Error.Message);
        }

        [Fact]
        public async Task CreatePayment_CashReceivedLessThanAmount_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var invoiceId = Guid.NewGuid();
            var order = new Order { OrderId = Guid.NewGuid(), OrderCode = "ORD2", BranchId = branchId, Status = "Draft" };
            var invoice = new Invoice { InvoiceId = invoiceId, InvoiceCode = "INV2", Status = "Draft", Order = order };
            invoice.InvoiceItems.Add(new InvoiceItem { InvoiceItemId = Guid.NewGuid(), Quantity = 1, SellPrice = 1000 });
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();

            var request = new CreatePaymentRequest { Method = PaymentMethod.Cash, Amount = 1000, CashReceived = 500 };

            // Act
            var result = await controller.CreatePayment(invoiceId, request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(badRequestResult.Value);
            Assert.Equal("MSG-76", errorResponse.Error.Code);
            Assert.Equal("Cash received is less than amount", errorResponse.Error.Message);
        }

        [Fact]
        public async Task CreatePayment_Cash_ValidData_ReturnsCreated()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var invoiceId = Guid.NewGuid();
            var order = new Order { OrderId = Guid.NewGuid(), OrderCode = "ORD3", BranchId = branchId, Status = "Draft" };
            var invoice = new Invoice { InvoiceId = invoiceId, InvoiceCode = "INV3", Status = "Draft", Order = order };
            invoice.InvoiceItems.Add(new InvoiceItem { InvoiceItemId = Guid.NewGuid(), Quantity = 1, SellPrice = 1000 });
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();

            var request = new CreatePaymentRequest { Method = PaymentMethod.Cash, Amount = 1000, CashReceived = 1500 };

            _mockMapper.Setup(m => m.Map<PaymentDto>(It.IsAny<Payment>())).Returns(new PaymentDto { Amount = 1000, Status = "Confirmed" });

            // Act
            var result = await controller.CreatePayment(invoiceId, request);

            // Assert
            var createdResult = Assert.IsType<CreatedResult>(result.Result);
            var dto = Assert.IsType<PaymentDto>(createdResult.Value);
            Assert.Equal("Confirmed", dto.Status);
            
            var paymentInDb = await context.Payments.FirstOrDefaultAsync(p => p.InvoiceId == invoiceId);
            Assert.NotNull(paymentInDb);
            Assert.Equal(500, paymentInDb.ChangeAmount);
            Assert.Equal(1000, paymentInDb.Amount);
            Assert.Equal(PaymentMethod.Cash, paymentInDb.Method);
        }
    }
}
