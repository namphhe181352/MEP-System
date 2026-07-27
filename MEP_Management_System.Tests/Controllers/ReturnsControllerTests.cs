using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Controllers.SaleController;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class ReturnsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<IMapper> _mockMapper;

        public ReturnsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockMapper = new Mock<IMapper>();
        }

        private ReturnsController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var controller = new ReturnsController(context, _mockMapper.Object);

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
        public async Task CreateReturn_InvoiceNotCompleted_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var invoiceId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var branchProductId = Guid.NewGuid();
            
            var branch = new Branch { BranchId = branchId, BranchName = "Test" };
            context.Branches.Add(branch);
            
            context.BranchProducts.Add(new BranchProduct { BranchProductId = branchProductId, BranchId = branchId });
            var order = new Order { OrderId = orderId, OrderCode = "O1", BranchId = branchId, Branch = branch, Status = "Draft" };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            var invoice = new Invoice { InvoiceId = invoiceId, InvoiceCode = "I1", OrderId = orderId, Status = "Draft", CreatedAt = DateTime.UtcNow };
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();

            var request = new CreateReturnOrderRequest { InvoiceId = invoiceId, RefundMethod = "CASH", ReturnType = "REFUND", Note = "Test" };

            // Act
            var result = await controller.CreateReturn(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(badRequestResult.Value);
            Assert.Equal("MSG-78", errorResponse.Error.Code);
            Assert.Equal("Chỉ hóa đơn đã hoàn tất mới được đổi trả.", errorResponse.Error.Message);
        }

        [Fact]
        public async Task CreateReturn_Over30Days_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var invoiceId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var branchProductId = Guid.NewGuid();
            
            var branch = new Branch { BranchId = branchId, BranchName = "Test" };
            context.Branches.Add(branch);
            
            context.BranchProducts.Add(new BranchProduct { BranchProductId = branchProductId, BranchId = branchId });

            var order = new Order { OrderId = orderId, OrderCode = "O2", BranchId = branchId, Branch = branch, Status = "Completed" };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            var invoice = new Invoice { InvoiceId = invoiceId, InvoiceCode = "I2", OrderId = orderId, Status = "Completed", CreatedAt = DateTime.UtcNow.AddDays(-31) };
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();

            var request = new CreateReturnOrderRequest { InvoiceId = invoiceId, RefundMethod = "CASH", ReturnType = "REFUND", Note = "Test" };

            // Act
            var result = await controller.CreateReturn(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(badRequestResult.Value);
            Assert.Equal("MSG-65", errorResponse.Error.Code);
        }

        [Fact]
        public async Task CreateReturn_Valid_ReturnsCreated()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var invoiceId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var branchProductId = Guid.NewGuid();
            
            var branch = new Branch { BranchId = branchId, BranchName = "Test" };
            context.Branches.Add(branch);
            
            context.BranchProducts.Add(new BranchProduct { BranchProductId = branchProductId, BranchId = branchId });

            var order = new Order { OrderId = orderId, OrderCode = "O3", BranchId = branchId, Branch = branch, Status = "Completed" };
            context.Orders.Add(order);
            await context.SaveChangesAsync();

            var invoice = new Invoice { InvoiceId = invoiceId, InvoiceCode = "I3", OrderId = orderId, Status = "Completed", CreatedAt = DateTime.UtcNow.AddDays(-1) };
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();

            var request = new CreateReturnOrderRequest { InvoiceId = invoiceId, RefundMethod = "CASH", ReturnType = "REFUND", Note = "Valid return" };
            _mockMapper.Setup(m => m.Map<ReturnOrderDto>(It.IsAny<ReturnOrder>())).Returns(new ReturnOrderDto { Note = "Valid return" });

            // Act
            var result = await controller.CreateReturn(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<ReturnOrderDto>(createdResult.Value);
            Assert.Equal("Valid return", dto.Note);
            
            var returnInDb = await context.ReturnOrders.FirstOrDefaultAsync();
            Assert.NotNull(returnInDb);
            Assert.Equal("Pending", returnInDb.Status);
        }

        [Fact]
        public async Task FinalizeReturn_NotPending_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var returnOrderId = Guid.NewGuid();
            var order = new Order { OrderId = Guid.NewGuid(), OrderCode = "O4", BranchId = branchId, Status = "Completed" };
            var returnOrder = new ReturnOrder { ReturnOrderId = returnOrderId, ReturnCode = "R1", OrderId = order.OrderId, Order = order, Status = "Completed" };
            context.ReturnOrders.Add(returnOrder);
            await context.SaveChangesAsync();

            var request = new FinalizeReturnRequest { Note = "Finalize" };

            // Act
            var result = await controller.FinalizeReturn(returnOrderId, request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(badRequestResult.Value);
            Assert.Equal("MSG-78", errorResponse.Error.Code);
            Assert.Equal("Phiếu đổi trả không ở trạng thái chờ xử lý.", errorResponse.Error.Message);
        }

        [Fact]
        public async Task CancelReturn_Valid_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var returnOrderId = Guid.NewGuid();
            var order = new Order { OrderId = Guid.NewGuid(), OrderCode = "O5", BranchId = branchId, Status = "Completed" };
            var returnOrder = new ReturnOrder { ReturnOrderId = returnOrderId, ReturnCode = "R2", OrderId = order.OrderId, Order = order, Status = "Pending" };
            context.ReturnOrders.Add(returnOrder);
            await context.SaveChangesAsync();

            // Act
            var result = await controller.CancelReturn(returnOrderId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var returnInDb = await context.ReturnOrders.FirstOrDefaultAsync(r => r.ReturnOrderId == returnOrderId);
            Assert.NotNull(returnInDb);
            Assert.Equal("Cancelled", returnInDb.Status);
        }
    }
}
