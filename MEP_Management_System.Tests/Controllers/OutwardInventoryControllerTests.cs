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
            var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(new DefaultHttpContext());
            var controller = new OutwardInventoryController(context, _mockBranchAccessor.Object, mockHttpContextAccessor.Object, _mockHub.Object);

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
            {
                _mockBranchAccessor.Setup(m => m.GetBranchId()).Returns(branchId.Value);
            }
            else
            {
                _mockBranchAccessor.Setup(m => m.GetBranchId()).Returns((Guid?)null);
            }

            return controller;
        }

        [Fact]
        public async Task GetOutwardInventories_ValidBranch_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, Guid.NewGuid(), branchId);

            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, Username = "test", Email = "test@test.com", FullName = "Test", PasswordHash = "hash" });
            
            var ticket = new StockTicket 
            { 
                StockTicketId = Guid.NewGuid(), 
                TicketCode = "TEST",
                BranchId = branchId, 
                UserId = userId,
                TicketType = "TRANSFER", 
                Status = "PENDING", 
                CreatedAt = DateTime.UtcNow 
            };
            context.StockTickets.Add(ticket);
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetOutwardInventories(status: null, ticketType: null, fromDate: null, toDate: null, pageNumber: 1, pageSize: 10, branchId: branchId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<PaginatedResponse<OutwardInventoryResponseDto>>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Single(apiResponse.Data!.Items);
            Assert.Equal("PENDING", apiResponse.Data.Items.First().Status);
        }

        [Fact]
        public async Task GetOutwardInventories_NoBranch_ReturnsEmptyList()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid(), null);

            var ticket = new StockTicket 
            { 
                StockTicketId = Guid.NewGuid(), 
                TicketCode = "TEST",
                BranchId = Guid.NewGuid(), 
                TicketType = "TRANSFER", 
                Status = "PENDING", 
                CreatedAt = DateTime.UtcNow 
            };
            context.StockTickets.Add(ticket);
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetOutwardInventories(status: null, ticketType: null, fromDate: null, toDate: null, pageNumber: 1, pageSize: 10, branchId: null);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(unauthorizedResult.Value);
            Assert.False(apiResponse.Success);
        }
    }
}
