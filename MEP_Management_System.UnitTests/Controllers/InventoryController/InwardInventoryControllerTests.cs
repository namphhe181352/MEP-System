using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.InventoryController;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Hubs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.InventoryController
{
    public class InwardInventoryControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<ICurrentBranchAccessor> _mockBranchAccessor;
        private readonly Mock<IHubContext<InventoryHub>> _mockHub;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly Mock<IHubClients> _mockClients;
        private readonly InwardInventoryQueryService _queryService;

        public InwardInventoryControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
            
            _mockHub = new Mock<IHubContext<InventoryHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            
            _mockHub.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

            var dbContext = new MEPDatabaseContext(_dbContextOptions);
            _queryService = new InwardInventoryQueryService(dbContext, _mockBranchAccessor.Object);
        }

        private InwardInventoryController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null, string role = "Owner")
        {
            var controller = new InwardInventoryController(context, _mockBranchAccessor.Object, _mockHub.Object, _queryService);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
                claims.Add(new Claim("userId", userId.Value.ToString()));
                claims.Add(new Claim("name", "Test User"));
            }
            if (!string.IsNullOrEmpty(role))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            if (branchId.HasValue)
            {
                _mockBranchAccessor.Setup(x => x.GetBranchId()).Returns(branchId.Value);
            }
            else
            {
                _mockBranchAccessor.Setup(x => x.GetBranchId()).Returns((Guid?)null);
            }

            return controller;
        }

        [Fact]
        public async Task CreateInwardInventory_ValidPurchase_CreatesPendingTicket()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            context.Users.Add(new User { UserId = userId, DefaultBranchId = branchId });
            context.Branches.Add(new Branch { BranchId = branchId, IsActive = 1 });
            context.Suppliers.Add(new MEP_Management_System.Models.Supplier { SupplierId = supplierId, SupplierName = "NPP Rạng Đông", Status = 1 });
            
            var product = new Product { ProductId = productId, ProductCode = "P01", ProductName = "Prod1" };
            context.Products.Add(product);
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, CostPrice = 100 });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            
            var request = new InwardInventoryCreateDto
            {
                InwardType = InwardInventoryType.Purchase,
                SupplierId = supplierId,
                Items = new List<InwardInventoryItemDto>
                {
                    new InwardInventoryItemDto { Id = "P01", Quantity = 10, CostPrice = 105 }
                }
            };

            // Act
            var result = await controller.CreateInwardInventory(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value;
            var success = (bool?)response?.GetType().GetProperty("Success")?.GetValue(response, null);
            Assert.True(success);

            var ticketDb = await context.StockTickets.Include(st => st.StockTicketItems).FirstOrDefaultAsync(t => t.UserId == userId);
            Assert.NotNull(ticketDb);
            Assert.Equal("PENDING", ticketDb.Status);
            Assert.Single(ticketDb.StockTicketItems);
            
            // Should send Notification
            _mockClients.Verify(c => c.Group($"Branch_{branchId}"), Times.Once);
            _mockClientProxy.Verify(p => p.SendCoreAsync("ReceiveInventoryNotification", It.IsAny<object[]>(), default), Times.Once);
        }

        [Fact]
        public async Task CreateInwardInventory_InactiveSupplier_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();

            context.Users.Add(new User { UserId = userId, DefaultBranchId = branchId });
            context.Suppliers.Add(new MEP_Management_System.Models.Supplier { SupplierId = supplierId, Status = 0, SupplierName = "Inactive NCC" }); // Inactive
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            
            var request = new InwardInventoryCreateDto
            {
                InwardType = InwardInventoryType.Purchase,
                SupplierId = supplierId,
                Items = new List<InwardInventoryItemDto> { new InwardInventoryItemDto { Id = "P01", Quantity = 1 } }
            };

            // Act
            var result = await controller.CreateInwardInventory(request);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(badRequest.Value);
            Assert.Contains("inactive", response.Message.ToLower());
        }

        [Fact]
        public async Task UpdateInwardInventory_CancelledTicket_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var ticketId = Guid.NewGuid();

            context.StockTickets.Add(new StockTicket
            {
                StockTicketId = ticketId,
                TicketType = "PURCHASE",
                Status = "CANCELLED",
                BranchId = branchId
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var request = new InwardInventoryUpdateDto { Note = "New Note" };

            // Act
            var result = await controller.UpdateInwardInventory(ticketId, request);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(badRequest.Value);
            Assert.Equal("Cannot update cancelled ticket", response.Message);
        }
    }
}
