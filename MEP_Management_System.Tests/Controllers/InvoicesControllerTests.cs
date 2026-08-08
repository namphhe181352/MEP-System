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

        // ==========================================
        // TEST CREATE INVOICE
        // ==========================================
        [Fact]
        public async Task CreateInvoice_WithOpenShift_CreatesDraftInvoiceSuccessfully()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            // Shift must be open to create invoice
            context.Shifts.Add(new Shift { ShiftId = Guid.NewGuid(), BranchId = branchId, UserId = userId, Status = "OPEN", StartedAt = DateTime.UtcNow, ShiftCode = "CA-01" });
            
            var user = new User { UserId = userId, Username = "sale1", FullName = "Nhân viên Bán hàng 1" };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var request = new CreateInvoiceRequest { Note = "Hóa đơn bàn 5" };

            // Mock auto-mapper behavior if the controller relies heavily on it
            _mockMapper.Setup(m => m.Map<InvoiceDto>(It.IsAny<Invoice>())).Returns((Invoice src) => new InvoiceDto { InvoiceId = src.InvoiceId, Note = src.Note });

            // Act
            var result = await controller.CreateInvoice(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<InvoiceDto>(createdResult.Value);
            
            Assert.Equal("Hóa đơn bàn 5", dto.Note);
            
            // Validate DB insertion
            var invoiceInDb = await context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == dto.InvoiceId);
            Assert.NotNull(invoiceInDb);
            Assert.Equal("Draft", invoiceInDb.Status);
        }

        [Fact]
        public async Task CreateInvoice_NoOpenShift_ReturnsApiErrorResponse()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            // Do not add any active shift
            var request = new CreateInvoiceRequest { Note = "Lỗi do chưa mở ca" };

            // Act
            var result = await controller.CreateInvoice(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(badRequestResult.Value);
            
            Assert.NotNull(errorResponse.Error);
            Assert.Contains("mở ca bán", errorResponse.Error.Message);
        }
    }
}
