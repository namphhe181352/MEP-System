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
    public class InwardInventoryControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<ICurrentBranchAccessor> _mockBranchAccessor;
        private Mock<IHubContext<InventoryHub>> _mockHub;
        private Mock<IClientProxy> _mockClientProxy;
        private Mock<IHubClients> _mockClients;

        public InwardInventoryControllerTests()
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

        private InwardInventoryController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null, string role = "Owner")
        {
            var queryService = new InwardInventoryQueryService(context, _mockBranchAccessor.Object);
            var controller = new InwardInventoryController(context, _mockBranchAccessor.Object, _mockHub.Object, queryService);

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
        // TEST GET /api/inventory/inward
        // ==========================================
        [Fact]
        public async Task GetInwardInventories_ValidBranch_ReturnsPaginatedList()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            context.Users.Add(new User { UserId = userId, Username = "manager", Email = "manager@mep.vn", FullName = "Quản lý kho", PasswordHash = "hash" });
            
            var ticket = new StockTicket 
            { 
                StockTicketId = Guid.NewGuid(), 
                BranchId = branchId, 
                UserId = userId,
                TicketType = "PURCHASE", 
                Status = "PENDING", // Chờ duyệt
                Notes = "Nhập hàng iphone",
                CreatedAt = DateTime.UtcNow 
            };
            context.StockTickets.Add(ticket);
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetInwardInventories(status: "PENDING", ticketType: null, fromDate: null, toDate: null, pageNumber: 1, pageSize: 10, branchId: branchId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<PaginatedResponse<InwardInventoryResponseDto>>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Single(apiResponse.Data.Items);
            Assert.Equal("Nhập hàng iphone", apiResponse.Data.Items.First().Notes);
        }

        // ==========================================
        // TEST POST /api/inventory/inward
        // ==========================================
        [Fact]
        public async Task CreateInwardInventory_ValidItems_CreatesTicketAndItems()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            context.Users.Add(new User { UserId = userId, Username = "staff", Email = "staff@mep.vn", FullName = "Nhân viên kho", PasswordHash = "hash" });
            
            var product = new Product { ProductId = Guid.NewGuid(), ProductName = "iPhone 15 Pro", SellingPrice = 30000000 };
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var request = new CreateInwardInventoryDto
            {
                Notes = "Nhập hàng test",
                TicketType = "PURCHASE",
                Details = new List<InwardInventoryDetailDto>
                {
                    new InwardInventoryDetailDto { ProductId = product.ProductId, Quantity = 10, Price = 25000000 }
                }
            };

            // Act
            var result = await controller.CreateInwardInventory(request, branchId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<InwardInventoryResponseDto>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            var createdTicket = await context.StockTickets.Include(t => t.StockTicketDetails).FirstOrDefaultAsync(t => t.StockTicketId == apiResponse.Data.StockTicketId);
            
            Assert.NotNull(createdTicket);
            Assert.Equal("PENDING", createdTicket.Status); // Ticket must be pending when created
            Assert.Single(createdTicket.StockTicketDetails);
            Assert.Equal(10, createdTicket.StockTicketDetails.First().Quantity);
            Assert.Equal(25000000, createdTicket.StockTicketDetails.First().Price);
        }
    }
}
