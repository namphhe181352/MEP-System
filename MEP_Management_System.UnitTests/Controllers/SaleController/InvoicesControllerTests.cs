using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.SaleController
{
    public class InvoicesControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IOrderDiscountService> _mockDiscountService;
        private readonly Mock<ILogger<InvoicesController>> _mockLogger;

        public InvoicesControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockMapper = new Mock<IMapper>();
            _mockDiscountService = new Mock<IOrderDiscountService>();
            _mockLogger = new Mock<ILogger<InvoicesController>>();
            
            // Default mock mapper behavior
            _mockMapper.Setup(m => m.Map<InvoiceDto>(It.IsAny<Invoice>())).Returns((Invoice i) => new InvoiceDto { InvoiceCode = i.InvoiceCode, Status = i.Status });
        }

        private InvoicesController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var mockMepHub = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<MEP_Management_System.Hubs.MepHub>>();
            var mockPosHub = new Mock<Microsoft.AspNetCore.SignalR.IHubContext<MEP_Management_System.Hubs.PosHub>>();
            var mockNotifications = new Mock<MepNotificationService>(mockMepHub.Object, mockPosHub.Object);

            var controller = new InvoicesController(context, _mockMapper.Object, mockNotifications.Object, _mockDiscountService.Object, _mockLogger.Object);

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
        public async Task CreateInvoice_OpenShift_CreatesInvoiceAndOrder()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            context.Shifts.Add(new Shift { ShiftId = Guid.NewGuid(), BranchId = branchId, Status = "OPEN" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var request = new CreateInvoiceRequest { Note = "Khách quen" };

            // Act
            var result = await controller.CreateInvoice(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<InvoiceDto>(createdResult.Value);
            
            var dbInvoice = await context.Invoices.Include(i => i.Order).FirstOrDefaultAsync();
            Assert.NotNull(dbInvoice);
            Assert.Equal("Draft", dbInvoice.Status);
            Assert.Equal(branchId, dbInvoice.Order.BranchId);
        }

        [Fact]
        public async Task FinalizeInvoice_ValidData_UpdatesStatusAndInventory()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var branchProductId = Guid.NewGuid();
            var shiftId = Guid.NewGuid();
            
            context.Users.Add(new User { UserId = userId, Username = "test" });
            context.Shifts.Add(new Shift { ShiftId = shiftId, BranchId = branchId, UserId = userId, Status = "OPEN" });
            
            context.Products.Add(new Product { ProductId = productId, ProductCode = "ONG-21", BaseUnit = "Cái" });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = branchProductId, BranchId = branchId, ProductId = productId, AvailableStock = 100, ActualStock = 100, CostPrice = 15000 });
            
            context.Orders.Add(new Order { OrderId = orderId, BranchId = branchId, UserId = userId, Status = "Draft" });
            context.Invoices.Add(new Invoice
            {
                InvoiceId = invoiceId,
                OrderId = orderId,
                ShiftId = shiftId,
                UserId = userId,
                Order = context.Orders.Local.First(),
                Status = "Draft",
                InvoiceItems = new List<InvoiceItem>
                {
                    new InvoiceItem { BranchProductId = branchProductId, Quantity = 10, SellPrice = 20000, ConvertValue = 1 }
                },
                Payments = new List<Payment>
                {
                    new Payment { Amount = 200000, Status = "Confirmed" }
                }
            });
            await context.SaveChangesAsync();

            _mockDiscountService.Setup(s => s.CalculateDiscountAsync(200000)).ReturnsAsync((0, 0, 200000));

            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.FinalizeInvoice(invoiceId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            
            var dbInvoice = await context.Invoices.FindAsync(invoiceId);
            Assert.Equal("Completed", dbInvoice.Status);
            
            var dbBranchProduct = await context.BranchProducts.FindAsync(branchProductId);
            Assert.Equal(90, dbBranchProduct.AvailableStock); // 100 - 10
            Assert.Equal(90, dbBranchProduct.ActualStock); // 100 - 10
            
            var ledgerCount = await context.InventoryLedgers.CountAsync(l => l.DocId == invoiceId);
            Assert.Equal(1, ledgerCount);
        }

        [Fact]
        public async Task FinalizeInvoice_InsufficientStock_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var branchProductId = Guid.NewGuid();
            var shiftId = Guid.NewGuid();
            
            context.Users.Add(new User { UserId = userId, Username = "test" });
            context.Shifts.Add(new Shift { ShiftId = shiftId, BranchId = branchId, UserId = userId, Status = "OPEN" });
            
            var productId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = productId, ProductCode = "ONG-21", BaseUnit = "Cái" });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = branchProductId, BranchId = branchId, ProductId = productId, AvailableStock = 5, ActualStock = 5 });
            
            var order = new Order { OrderId = Guid.NewGuid(), BranchId = branchId, UserId = userId, Status = "Draft" };
            context.Orders.Add(order);
            context.Invoices.Add(new Invoice
            {
                InvoiceId = invoiceId,
                OrderId = order.OrderId,
                ShiftId = shiftId,
                UserId = userId,
                Order = order,
                Status = "Draft",
                InvoiceItems = new List<InvoiceItem>
                {
                    new InvoiceItem { BranchProductId = branchProductId, Quantity = 10, SellPrice = 20000, ConvertValue = 1 } // Trying to sell 10, only 5 available
                },
                Payments = new List<Payment> { new Payment { Amount = 200000, Status = "Confirmed" } }
            });
            await context.SaveChangesAsync();

            _mockDiscountService.Setup(s => s.CalculateDiscountAsync(200000)).ReturnsAsync((0, 0, 200000));
            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.FinalizeInvoice(invoiceId);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var response = Assert.IsType<MEP_Management_System.DTOs.SaleDTOs.ApiErrorResponse>(badRequest.Value);
            Assert.Contains("Insufficient stock", response.Error.Message);
        }

        [Fact]
        public async Task FinalizeInvoice_InsufficientPayment_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var branchProductId = Guid.NewGuid();
            var shiftId = Guid.NewGuid();
            
            context.Users.Add(new User { UserId = userId, Username = "test" });
            context.Shifts.Add(new Shift { ShiftId = shiftId, BranchId = branchId, UserId = userId, Status = "OPEN" });
            
            var productId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = productId, ProductCode = "ONG-21", BaseUnit = "Cái" });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = branchProductId, BranchId = branchId, ProductId = productId, AvailableStock = 100, ActualStock = 100 });
            
            var order = new Order { OrderId = Guid.NewGuid(), BranchId = branchId, UserId = userId, Status = "Draft" };
            context.Orders.Add(order);
            context.Invoices.Add(new Invoice
            {
                InvoiceId = invoiceId,
                OrderId = order.OrderId,
                ShiftId = shiftId,
                UserId = userId,
                Order = order,
                Status = "Draft",
                InvoiceItems = new List<InvoiceItem>
                {
                    new InvoiceItem { BranchProductId = branchProductId, Quantity = 10, SellPrice = 20000, ConvertValue = 1 } // Total 200k
                },
                Payments = new List<Payment> { new Payment { Amount = 150000, Status = "Confirmed" } } // Only paid 150k
            });
            await context.SaveChangesAsync();

            _mockDiscountService.Setup(s => s.CalculateDiscountAsync(200000)).ReturnsAsync((0, 0, 200000));
            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.FinalizeInvoice(invoiceId);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var response = Assert.IsType<MEP_Management_System.DTOs.SaleDTOs.ApiErrorResponse>(badRequest.Value);
            Assert.Contains("Thanh toán chưa đủ", response.Error.Message);
        }
        [Theory]
        [InlineData("Draft", -20, 1, 1, 10, 1)] // Status filter
        [InlineData("Completed", -20, 1, 1, 10, 1)] // Status filter
        [InlineData(null, -20, 1, 1, 10, 2)] // Date range valid
        [InlineData(null, -40, -30, 1, 10, 0)] // Date range no match
        [InlineData(null, -20, 1, 1, 1, 1)] // Page 1 size 1
        [InlineData(null, -20, 1, 2, 1, 1)] // Page 2 size 1
        [InlineData(null, -20, 1, -1, 10, 2)] // Invalid page -> valid page
        public async Task GetInvoices_ReturnsPagedList(string? status, int? fromDays, int? toDays, int page, int size, int expectedCount)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            
            var testUser = new User { UserId = Guid.NewGuid(), Username = "testuser", FullName = "Test User" };
            context.Users.Add(testUser);
            
            var inv1 = new Invoice { InvoiceId = Guid.NewGuid(), InvoiceCode = "INV-001", Status = "Draft", CreatedAt = now.AddDays(-10) };
            var order1 = new Order { OrderId = Guid.NewGuid(), BranchId = branchId, UserId = testUser.UserId, User = testUser };
            inv1.OrderId = order1.OrderId;
            inv1.Order = order1;

            var inv2 = new Invoice { InvoiceId = Guid.NewGuid(), InvoiceCode = "INV-002", Status = "Completed", CreatedAt = now.AddDays(-2) };
            var order2 = new Order { OrderId = Guid.NewGuid(), BranchId = branchId, UserId = testUser.UserId, User = testUser };
            inv2.OrderId = order2.OrderId;
            inv2.Order = order2;

            context.Orders.AddRange(order1, order2);
            context.Invoices.AddRange(inv1, inv2);
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid(), branchId);
            
            DateTime? from = fromDays.HasValue ? now.AddDays(fromDays.Value) : null;
            DateTime? to = toDays.HasValue ? now.AddDays(toDays.Value) : null;

            var dbCount = await context.Invoices.CountAsync();
            var dbMatchCount = await context.Invoices.Include(i => i.Order).Where(i => i.Order.BranchId == branchId).CountAsync();
            Assert.Equal(2, dbCount); // Verify they are saved
            Assert.Equal(2, dbMatchCount); // Verify they match branchId

            var result = await controller.GetInvoices(status, from, to, null, null, page, size);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pageResult = Assert.IsType<MEP_Management_System.DTOs.SaleDTOs.PageResultDto<InvoiceDto>>(okResult.Value);
            
            Assert.Equal(expectedCount, pageResult.Items.Count());
        }

        [Fact]
        public async Task FinalizeInvoice_EmptyItems_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var shiftId = Guid.NewGuid();
            
            context.Users.Add(new User { UserId = userId, Username = "test" });
            context.Shifts.Add(new Shift { ShiftId = shiftId, BranchId = branchId, UserId = userId, Status = "OPEN" });
            
            var order = new Order { OrderId = Guid.NewGuid(), BranchId = branchId, UserId = userId, Status = "Draft" };
            context.Orders.Add(order);
            context.Invoices.Add(new Invoice
            {
                InvoiceId = invoiceId,
                OrderId = order.OrderId,
                ShiftId = shiftId,
                UserId = userId,
                Order = order,
                Status = "Draft",
                InvoiceItems = new List<InvoiceItem>() // Empty items
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.FinalizeInvoice(invoiceId);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            var response = Assert.IsType<MEP_Management_System.DTOs.SaleDTOs.ApiErrorResponse>(badRequest.Value);
            Assert.Contains("at least 1 item", response.Error.Message);
        }

        [Fact]
        public async Task AddItem_ProductInactive_ReturnsNotFound()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            
            var order = new Order { OrderId = Guid.NewGuid(), BranchId = branchId, UserId = userId, Status = "Draft" };
            context.Orders.Add(order);
            context.Invoices.Add(new Invoice { InvoiceId = invoiceId, OrderId = order.OrderId, Order = order, Status = "Draft" });
            
            // Inactive product
            context.Products.Add(new Product { ProductId = productId, ProductCode = "INACTIVE" });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, IsActive = 0 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var request = new AddItemRequest { ProductId = productId, Quantity = 1 };

            // Act
            var result = await controller.AddItem(invoiceId, request);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
            var response = Assert.IsType<MEP_Management_System.DTOs.SaleDTOs.ApiErrorResponse>(notFound.Value);
            Assert.Contains("inactive", response.Error.Message);
        }

    }
}



