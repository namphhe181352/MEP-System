using System;
using System.Collections.Generic;
using System.Linq;
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
using Moq;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class InvoicesControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<IMapper> _mockMapper;
        private Mock<IOrderDiscountService> _mockOrderDiscountService;
        private Mock<ILogger<InvoicesController>> _mockLogger;
        private Mock<IHubContext<MepHub>> _mockMepHub;
        private Mock<IHubContext<PosHub>> _mockPosHub;
        private MepNotificationService _notificationService;

        public InvoicesControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockMapper = new Mock<IMapper>();
            _mockOrderDiscountService = new Mock<IOrderDiscountService>();
            _mockLogger = new Mock<ILogger<InvoicesController>>();

            _mockMepHub = new Mock<IHubContext<MepHub>>();
            _mockPosHub = new Mock<IHubContext<PosHub>>();

            var mockClientProxy = new Mock<IClientProxy>();
            var mockClients = new Mock<IHubClients>();
            mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);

            _mockPosHub.Setup(h => h.Clients).Returns(mockClients.Object);

            _notificationService = new MepNotificationService(_mockMepHub.Object, _mockPosHub.Object);
        }

        private InvoicesController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var controller = new InvoicesController(
                context, 
                _mockMapper.Object, 
                _notificationService, 
                _mockOrderDiscountService.Object, 
                _mockLogger.Object);

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
        public async Task GetInvoices_ValidBranch_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var user = new User { UserId = userId, Username = "test", Email = "test@test.com", FullName = "Test", PasswordHash = "hash" };
            context.Users.Add(user);
            
            var orderId = Guid.NewGuid();
            var order = new Order { OrderId = orderId, OrderCode = "ORD-001", BranchId = branchId, UserId = userId, Status = "Draft", CreatedAt = DateTime.UtcNow, User = user };
            var invoice = new Invoice { InvoiceId = Guid.NewGuid(), InvoiceCode = "INV-001", OrderId = orderId, Status = "Draft", CreatedAt = DateTime.UtcNow, UserId = userId, Order = order };
            order.Invoices.Add(invoice);
            
            context.Orders.Add(order);
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();

            _mockMapper.Setup(m => m.Map<InvoiceDto>(It.IsAny<Invoice>())).Returns(new InvoiceDto { Status = "Draft" });

            // Act
            var result = await controller.GetInvoices(status: null, dateFrom: null, dateTo: null, customerId: null, staffId: null, page: 1, pageSize: 20);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pageResult = Assert.IsType<PageResultDto<InvoiceDto>>(okResult.Value);
            Assert.Single(pageResult.Items);
            Assert.Equal(1, pageResult.TotalCount);
        }

        [Fact]
        public async Task CreateInvoice_NoOpenShift_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var request = new CreateInvoiceRequest { Note = "Test" };

            // Act
            var result = await controller.CreateInvoice(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(badRequestResult.Value);
            Assert.Equal("Vui lòng mở ca bán trước khi tạo đơn hàng", errorResponse.Error.Message);
        }

        [Fact]
        public async Task CreateInvoice_WithOpenShift_ReturnsCreated()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            context.Shifts.Add(new Shift { ShiftId = Guid.NewGuid(), BranchId = branchId, UserId = userId, Status = "OPEN", StartedAt = DateTime.UtcNow, ShiftCode = "TEST" });
            await context.SaveChangesAsync();

            var request = new CreateInvoiceRequest { Note = "Test Invoice" };

            _mockMapper.Setup(m => m.Map<InvoiceDto>(It.IsAny<Invoice>())).Returns(new InvoiceDto { InvoiceId = Guid.NewGuid() });

            // Act
            var result = await controller.CreateInvoice(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<InvoiceDto>(createdResult.Value);
            Assert.Equal(userId, dto.UserId);
        }
    }
}
