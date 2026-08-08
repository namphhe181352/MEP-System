using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Controllers.SaleController;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.SaleController
{
    public class ReturnsControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<IMapper> _mockMapper;

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
                claims.Add(new Claim("userId", userId.Value.ToString()));
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

        private void SetupReturnPolicies(MEPDatabaseContext context, Guid branchId, Guid categoryId)
        {
            var policies = new List<CategoryPolicyDto>
            {
                new CategoryPolicyDto
                {
                    CategoryId = categoryId,
                    CategoryName = "Thiết bị điện",
                    ReturnDays = 30,
                    ExchangeDays = 30
                }
            };
            
            context.SystemConfigs.Add(new SystemConfig
            {
                ConfigId = Guid.NewGuid(),
                BranchId = branchId,
                ConfigKey = "CATEGORY_RETURN_POLICIES",
                ConfigValue = JsonSerializer.Serialize(policies)
            });
        }

        [Fact]
        public async Task CreateReturn_ValidInvoice_CreatesReturnOrder()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            
            SetupReturnPolicies(context, branchId, categoryId);

            var productId = Guid.NewGuid();
            var branchProductId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = productId, ProductCode = "CP-20A", ProductName = "Cầu dao tự động 20A", CategoryTaxonomyId = categoryId });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = branchProductId, BranchId = branchId, ProductId = productId });
            
            var orderId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, Username = "test" });
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "b1" });
            context.Orders.Add(new Order { OrderId = orderId, BranchId = branchId, UserId = userId });
            context.Invoices.Add(new Invoice
            {
                InvoiceId = invoiceId,
                OrderId = orderId,
                Status = "Completed",
                CreatedAt = DateTime.UtcNow,
                InvoiceItems = new List<InvoiceItem>
                {
                    new InvoiceItem { BranchProductId = branchProductId, Quantity = 2, SellPrice = 150000 }
                }
            });
            await context.SaveChangesAsync();

            _mockMapper.Setup(m => m.Map<ReturnOrderDto>(It.IsAny<ReturnOrder>()))
                .Returns((ReturnOrder ro) => new ReturnOrderDto { ReturnOrderId = ro.ReturnOrderId, ReturnCode = ro.ReturnCode });

            var controller = CreateController(context, userId, branchId);
            var request = new CreateReturnOrderRequest
            {
                InvoiceId = invoiceId,
                RefundMethod = "CASH",
                ReturnType = "REFUND"
            };

            // Act
            var result = await controller.CreateReturn(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<ReturnOrderDto>(createdResult.Value);
            
            var dbReturnOrder = await context.ReturnOrders.FindAsync(dto.ReturnOrderId);
            Assert.NotNull(dbReturnOrder);
            Assert.Equal("Pending", dbReturnOrder.Status);
            Assert.Equal(orderId, dbReturnOrder.OrderId);
        }

        [Fact]
        public async Task AddReturnItem_ValidItem_AddsToReturnOrder()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            
            SetupReturnPolicies(context, branchId, categoryId);

            var productId = Guid.NewGuid();
            var branchProductId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = productId, ProductCode = "OD-LIOA", ProductName = "Ổ cắm Lioa", CategoryTaxonomyId = categoryId });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = branchProductId, BranchId = branchId, ProductId = productId });
            
            var orderId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var invoice = new Invoice
            {
                InvoiceId = invoiceId,
                OrderId = orderId,
                Status = "Completed",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                DiscountAmount = 0
            };
            
            var invoiceItem = new InvoiceItem 
            { 
                InvoiceItemId = Guid.NewGuid(), 
                InvoiceId = invoiceId, 
                BranchProductId = branchProductId, 
                Quantity = 5, 
                SellPrice = 50000 
            };
            
            context.Orders.Add(new Order { OrderId = orderId, BranchId = branchId });
            context.Invoices.Add(invoice);
            context.InvoiceItems.Add(invoiceItem);
            
            var returnOrderId = Guid.NewGuid();
            context.ReturnOrders.Add(new ReturnOrder
            {
                ReturnOrderId = returnOrderId,
                OrderId = orderId,
                Status = "Pending",
                ReturnType = "REFUND"
            });
            await context.SaveChangesAsync();

            _mockMapper.Setup(m => m.Map<ReturnItemDto>(It.IsAny<ReturnItem>()))
                .Returns((ReturnItem ri) => new ReturnItemDto { ReturnItemId = ri.ReturnItemId, Quantity = ri.Quantity, RefundAmount = ri.RefundAmount });

            var controller = CreateController(context, userId, branchId);
            var request = new AddReturnItemRequest { ProductId = productId, Quantity = 2, Reason = "Lỗi" };

            // Act
            var result = await controller.AddReturnItem(returnOrderId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<ReturnItemDto>(okResult.Value);
            
            var dbReturnItem = await context.ReturnItems.FirstOrDefaultAsync(ri => ri.ReturnOrderId == returnOrderId);
            Assert.NotNull(dbReturnItem);
            Assert.Equal(2, dbReturnItem.Quantity);
            Assert.Equal(100000, dbReturnItem.RefundAmount); // 2 * 50000
        }

        [Fact]
        public async Task FinalizeReturn_ValidData_UpdatesStockAndCreatesRefund()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            
            var returnOrderId = Guid.NewGuid();
            var branchProductId = Guid.NewGuid();
            
            context.BranchProducts.Add(new BranchProduct { BranchProductId = branchProductId, BranchId = branchId, AvailableStock = 10, ActualStock = 10 });
            
            var order = new Order { OrderId = Guid.NewGuid(), BranchId = branchId, CustomerId = customerId };
            context.Orders.Add(order);
            context.Invoices.Add(new Invoice { InvoiceId = Guid.NewGuid(), OrderId = order.OrderId }); // needed for InvoiceId lookup
            
            context.ReturnOrders.Add(new ReturnOrder
            {
                ReturnOrderId = returnOrderId,
                OrderId = order.OrderId,
                Status = "Pending",
                RefundMethod = "CASH",
                UserId = userId,
                ReturnItems = new List<ReturnItem>
                {
                    new ReturnItem { BranchProductId = branchProductId, Quantity = 2, RefundAmount = 100000 }
                }
            });
            await context.SaveChangesAsync();

            _mockMapper.Setup(m => m.Map<ReturnOrderDto>(It.IsAny<ReturnOrder>())).Returns(new ReturnOrderDto());

            var controller = CreateController(context, userId, branchId);
            var request = new FinalizeReturnRequest { Note = "Đã nhận hàng trả" };

            // Act
            var result = await controller.FinalizeReturn(returnOrderId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            
            var dbReturnOrder = await context.ReturnOrders.FindAsync(returnOrderId);
            Assert.Equal("Completed", dbReturnOrder.Status);
            Assert.Equal(100000, dbReturnOrder.RefundAmount);
            
            var dbBranchProduct = await context.BranchProducts.FindAsync(branchProductId);
            Assert.Equal(12, dbBranchProduct.AvailableStock); // 10 + 2
            
            var dbRefund = await context.RefundRecords.FirstOrDefaultAsync(r => r.OrderId == order.OrderId);
            Assert.NotNull(dbRefund);
            Assert.Equal(100000, dbRefund.RefundAmount);
        }
    }
}
