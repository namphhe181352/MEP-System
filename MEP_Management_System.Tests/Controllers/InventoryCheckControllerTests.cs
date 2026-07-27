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
    public class InventoryCheckControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<ICurrentBranchAccessor> _mockBranchAccessor;
        private Mock<IHubContext<InventoryHub>> _mockHub;

        public InventoryCheckControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
            _mockHub = new Mock<IHubContext<InventoryHub>>();
        }

        private InventoryCheckController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null, string role = "Owner")
        {
            var controller = new InventoryCheckController(context, _mockHub.Object, _mockBranchAccessor.Object);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            }
            if (branchId.HasValue)
            {
                claims.Add(new Claim("branchId", branchId.Value.ToString()));
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
        public async Task GetInventoryChecks_ValidBranch_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, Guid.NewGuid(), branchId);

            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, Username = "test", Email = "test@test.com", FullName = "Test", PasswordHash = "hash" });
            
            var ticket = new InventoryCheckTicket
            {
                TicketId = Guid.NewGuid(),
                TicketCode = "IC-001",
                BranchId = branchId,
                CreatedByUserId = userId,
                Status = InventoryCheckTicketStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };
            context.InventoryCheckTickets.Add(ticket);
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetInventoryChecks(status: null, startDate: null, endDate: null, pageNumber: 1, pageSize: 20, branchId: branchId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<PaginatedResponse<InventoryCheckListItemDto>>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Single(apiResponse.Data.Items);
            Assert.Equal("IC-001", apiResponse.Data.Items.First().TicketCode);
        }

        [Fact]
        public async Task GetInventoryCheckById_ExistingId_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, Guid.NewGuid(), branchId);

            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, Username = "test", Email = "test@test.com", FullName = "Test", PasswordHash = "hash" });
            
            var ticketId = Guid.NewGuid();
            var ticket = new InventoryCheckTicket
            {
                TicketId = ticketId,
                TicketCode = "IC-002",
                BranchId = branchId,
                CreatedByUserId = userId,
                Status = InventoryCheckTicketStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };
            context.InventoryCheckTickets.Add(ticket);
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetInventoryCheckById(ticketId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<InventoryCheckResponseDto>>(okResult.Value);
            Assert.True(apiResponse.Success);
            Assert.Equal("IC-002", apiResponse.Data.TicketCode);
        }

        [Fact]
        public async Task GetInventoryCheckById_NonExistingId_ReturnsNotFound()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid(), Guid.NewGuid());

            // Act
            var result = await controller.GetInventoryCheckById(Guid.NewGuid());

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Contains("không tồn tại", apiResponse.Message);
        }
    }
}
