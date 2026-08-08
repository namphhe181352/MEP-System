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

namespace MEP_Management_System.Tests.Controllers
{
    public class OutwardInventoryControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<ICurrentBranchAccessor> _mockBranchAccessor;
        private Mock<IHubContext<InventoryHub>> _mockHub;
        private Mock<IClientProxy> _mockClientProxy;
        private Mock<IHubClients> _mockClients;

        public OutwardInventoryControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
            
            _mockClientProxy = new Mock<IClientProxy>();
            _mockClients = new Mock<IHubClients>();
            _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
            
            _mockHub = new Mock<IHubContext<InventoryHub>>();
            _mockHub.Setup(h => h.Clients).Returns(_mockClients.Object);
        }

        private OutwardInventoryController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null, string role = "Owner")
        {
            var queryService = new OutwardInventoryQueryService(context, _mockBranchAccessor.Object);
            var controller = new OutwardInventoryController(context, _mockBranchAccessor.Object, _mockHub.Object, queryService);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
                claims.Add(new Claim("userId", userId.Value.ToString()));
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
                _mockBranchAccessor.Setup(m => m.GetBranchId()).Returns(branchId.Value);
            else
                _mockBranchAccessor.Setup(m => m.GetBranchId()).Returns((Guid?)null);

            return controller;
        }

        // ==========================================
        // TEST POST /api/inventory/outward
        // ==========================================
        [Fact]
        public async Task CreateOutwardInventory_ValidStock_CreatesTicketAndReducesInventory()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            context.Users.Add(new User { UserId = userId, Username = "staff", Email = "staff@mep.vn", FullName = "Nhân viên xuất kho", PasswordHash = "hash" });
            
            var product = new Product { ProductId = Guid.NewGuid(), ProductName = "Samsung Galaxy S24", SellingPrice = 25000000 };
            context.Products.Add(product);
            
            // Branch has 15 items in stock
            var branchProduct = new BranchProduct { BranchId = branchId, ProductId = product.ProductId, StockQuantity = 15 };
            context.BranchProducts.Add(branchProduct);
            await context.SaveChangesAsync();

            var request = new CreateOutwardInventoryDto
            {
                Notes = "Xuất bán lẻ",
                TicketType = "SALE", // Or RETURN_TO_SUPPLIER, TRANSFER...
                Details = new List<OutwardInventoryDetailDto>
                {
                    new OutwardInventoryDetailDto { ProductId = product.ProductId, Quantity = 5, Price = 25000000 }
                }
            };

            // Act
            var result = await controller.CreateOutwardInventory(request, branchId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<OutwardInventoryResponseDto>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            
            // Check that the ticket is created as APPROVED since standard outward operations directly affect stock
            var createdTicket = await context.StockTickets.Include(t => t.StockTicketDetails).FirstOrDefaultAsync(t => t.StockTicketId == apiResponse.Data.StockTicketId);
            Assert.NotNull(createdTicket);
            Assert.Equal("SALE", createdTicket.TicketType);
            Assert.Single(createdTicket.StockTicketDetails);
            Assert.Equal(5, createdTicket.StockTicketDetails.First().Quantity);
            
            // Check that inventory is correctly reduced by 5 (15 - 5 = 10)
            var updatedStock = await context.BranchProducts.FirstOrDefaultAsync(bp => bp.BranchId == branchId && bp.ProductId == product.ProductId);
            Assert.Equal(10, updatedStock.StockQuantity);
        }

        [Fact]
        public async Task CreateOutwardInventory_NotEnoughStock_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);
            
            var product = new Product { ProductId = Guid.NewGuid(), ProductName = "Samsung Galaxy S24", SellingPrice = 25000000 };
            context.Products.Add(product);
            
            // Branch only has 3 items
            var branchProduct = new BranchProduct { BranchId = branchId, ProductId = product.ProductId, StockQuantity = 3 };
            context.BranchProducts.Add(branchProduct);
            await context.SaveChangesAsync();

            var request = new CreateOutwardInventoryDto
            {
                Notes = "Xuất lô hàng lớn",
                TicketType = "TRANSFER",
                Details = new List<OutwardInventoryDetailDto>
                {
                    // Attempting to withdraw 10 items, which is more than the 3 in stock
                    new OutwardInventoryDetailDto { ProductId = product.ProductId, Quantity = 10, Price = 25000000 } 
                }
            };

            // Act
            var result = await controller.CreateOutwardInventory(request, branchId);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
            
            Assert.False(apiResponse.Success);
            Assert.Contains("không đủ số lượng", apiResponse.Message.ToLower());
        }
    }
}
