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
    public class OutwardInventoryControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<ICurrentBranchAccessor> _mockBranchAccessor;
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly Mock<IHubContext<InventoryHub>> _mockHub;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly Mock<IHubClients> _mockClients;

        public OutwardInventoryControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            
            _mockHub = new Mock<IHubContext<InventoryHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            
            _mockHub.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
        }

        private OutwardInventoryController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null, string role = "Owner")
        {
            var controller = new OutwardInventoryController(context, _mockBranchAccessor.Object, _mockHttpContextAccessor.Object, _mockHub.Object);

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

            var httpContext = new DefaultHttpContext { User = claimsPrincipal };
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

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
        public async Task CreateOutwardInventory_WriteOff_CreatesPendingTicket()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var branchProductId = Guid.NewGuid();

            context.Users.Add(new User { UserId = userId, DefaultBranchId = branchId });
            context.Branches.Add(new Branch { BranchId = branchId, IsActive = 1 });
            
            var product = new Product { ProductId = productId, ProductCode = "P01", ProductName = "Prod1" };
            context.Products.Add(product);
            context.BranchProducts.Add(new BranchProduct { BranchProductId = branchProductId, BranchId = branchId, ProductId = productId, CostPrice = 100, ActualStock = 50 });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            
            var request = new OutwardInventoryCreateDto
            {
                OutwardType = OutwardInventoryType.DamagedHaoHut,
                Reason = "Xuất hủy",
                Items = new List<OutwardInventoryItemDto>
                {
                    new OutwardInventoryItemDto { BranchProductId = branchProductId, Quantity = 5, UnitPrice = 100 }
                }
            };

            // Act
            var result = await controller.CreateOutwardInventory(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value;
            var success = (bool?)response?.GetType().GetProperty("Success")?.GetValue(response, null);
            Assert.True(success);

            var ticketDb = await context.StockTickets.Include(st => st.StockTicketItems).FirstOrDefaultAsync(t => t.UserId == userId);
            Assert.NotNull(ticketDb);
            Assert.Equal("PENDING", ticketDb.Status);
            Assert.Equal("WRITE_OFF", ticketDb.TicketType);
            Assert.Single(ticketDb.StockTicketItems);
        }

        [Fact]
        public async Task CreateOutwardInventory_ExceedsStock_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var branchProductId = Guid.NewGuid();

            context.Users.Add(new User { UserId = userId, DefaultBranchId = branchId });
            context.Branches.Add(new Branch { BranchId = branchId, IsActive = 1 });
            var productId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = productId, ProductCode = "P02", ProductName = "Prod2" });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = branchProductId, BranchId = branchId, ProductId = productId, ActualStock = 10 }); // Only 10 in stock
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            
            var request = new OutwardInventoryCreateDto
            {
                OutwardType = OutwardInventoryType.InternalUse,
                Reason = "Xuất sử dụng nội bộ",
                Items = new List<OutwardInventoryItemDto>
                {
                    new OutwardInventoryItemDto { BranchProductId = branchProductId, Quantity = 15 } // Requesting 15
                }
            };

            // Act
            var result = await controller.CreateOutwardInventory(request);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(badRequest.Value);
            Assert.False(response.Success);
            
            var errors = response.Errors as List<string>;
            Assert.NotNull(errors);
            Assert.Contains(errors, e => e.Contains("không đủ để thực hiện xuất kho"));
        }
    }
}
