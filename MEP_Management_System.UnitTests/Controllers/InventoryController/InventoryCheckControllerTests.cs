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
    public class InventoryCheckControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<ICurrentBranchAccessor> _mockBranchAccessor;
        private readonly Mock<IHubContext<InventoryHub>> _mockHub;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly Mock<IHubClients> _mockClients;

        public InventoryCheckControllerTests()
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
            _mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(_mockClientProxy.Object);
        }

        private InventoryCheckController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null, string role = "Owner")
        {
            var controller = new InventoryCheckController(context, _mockHub.Object, _mockBranchAccessor.Object);
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
                claims.Add(new Claim("role", role));
            }
            if (branchId.HasValue) _mockBranchAccessor.Setup(x => x.GetBranchId()).Returns(branchId.Value);
            
            var identity = new ClaimsIdentity(claims, "TestAuthType", ClaimTypes.Name, ClaimTypes.Role);
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
            return controller;
        }

        // =====================================
        // 1. CREATE INVENTORY CHECK
        // =====================================

        [Theory]
        [InlineData("Staff", false, "Bạn không có quyền gán phiếu kiểm kê")] // Staff assigns someone else
        [InlineData("Owner", false, null)] // Owner assigns someone else
        [InlineData("Staff", true, null)] // Staff assigns self
        public async Task CreateInventoryCheck_AssigneeValidation_ReturnsAppropriateResult(string role, bool assignSelf, string? expectedError)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var assigneeId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var category1 = Guid.NewGuid();
            var category2 = Guid.NewGuid();

            context.Users.Add(new User { UserId = userId, Username = "actor" });
            context.Users.Add(new User { UserId = assigneeId, Username = "target" });
            context.Products.Add(new Product { ProductId = Guid.NewGuid(), ProductCode = "P3", CategoryTaxonomyId = category1 });
            context.Products.Add(new Product { ProductId = Guid.NewGuid(), ProductCode = "P4", CategoryTaxonomyId = category2 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId, role);
            var targetAssignee = assignSelf ? userId : assigneeId;
            var request = new CreateInventoryCheckDto { ProductIds = new List<Guid> { Guid.NewGuid() }, AssigneeUserId = targetAssignee };

            var result = await controller.CreateInventoryCheck(request);
            if (expectedError != null)
            {
                var badRequest = Assert.IsType<BadRequestObjectResult>(result);
                Assert.Contains(expectedError, badRequest.Value?.ToString() ?? "");
            }
            else
            {
                Assert.IsType<CreatedAtActionResult>(result);
            }
        }

        [Fact]
        public async Task CreateInventoryCheck_ProductAlreadyInActiveCheck_ReturnsBadRequest()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            var activeTicket = new InventoryCheckTicket { TicketId = Guid.NewGuid(), BranchId = branchId, Status = "PENDING" };
            activeTicket.InventoryCheckDetails.Add(new InventoryCheckDetail { DetailId = Guid.NewGuid(), ProductId = productId, TicketId = activeTicket.TicketId });
            context.InventoryCheckTickets.Add(activeTicket);
            
            var bp = new BranchProduct { BranchProductId = productId, BranchId = branchId, ProductId = productId };
            context.BranchProducts.Add(bp);
            context.Products.Add(new Product { ProductId = productId, ProductCode = "P1", CategoryTaxonomyId = targetId });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid(), branchId);
            var request = new CreateInventoryCheckDto { ProductIds = new List<Guid> { bp.ProductId } };

            var result = await controller.CreateInventoryCheck(request);
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("đang được kiểm kê trong phiếu đang mở", badRequest.Value?.ToString() ?? "");
        }

        // =====================================
        // 2. UPDATE INVENTORY CHECK
        // =====================================

        [Theory]
        [InlineData("CONFIRMED", "Chỉ có thể cập nhật phiếu ở trạng thái")]
        [InlineData("CANCELLED", "Chỉ có thể cập nhật phiếu ở trạng thái")]
        [InlineData("PENDING", null)] // Success
        public async Task UpdateInventoryCheck_StatusValidation_ReturnsAppropriateResult(string status, string? expectedError)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ticketId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var detailId = Guid.NewGuid();

            context.InventoryCheckTickets.Add(new InventoryCheckTicket { TicketId = ticketId, BranchId = branchId, Status = status });
            context.InventoryCheckDetails.Add(new InventoryCheckDetail { DetailId = detailId, TicketId = ticketId, ActualQuantity = 5 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid(), branchId);
            var request = new UpdateInventoryCheckDto { Details = new List<UpdateInventoryCheckDetailDto> { new UpdateInventoryCheckDetailDto { DetailId = detailId, ActualQuantity = 10, Note = "Update" } } };

            var result = await controller.UpdateInventoryCheck(ticketId, request);
            if (expectedError != null)
            {
                var badRequest = Assert.IsType<BadRequestObjectResult>(result);
                Assert.Contains(expectedError, badRequest.Value?.ToString() ?? "");
            }
            else
            {
                Assert.IsType<OkObjectResult>(result);
                var detail = await context.InventoryCheckDetails.FindAsync(detailId);
                Assert.Equal(10, detail!.ActualQuantity);
            }
        }

        // =====================================
        // 3. CANCEL INVENTORY CHECK
        // =====================================

        [Theory]
        [InlineData(InventoryCheckTicketStatus.Completed, "Phiếu kiểm kê đã hoàn tất.")]
        [InlineData(InventoryCheckTicketStatus.Cancelled, "Phiếu kiểm kê đã được hủy trước đó.")]
        [InlineData(InventoryCheckTicketStatus.Draft, null)]
        public async Task CancelInventoryCheck_StatusValidation_ReturnsAppropriateResult(InventoryCheckTicketStatus initialStatus, string? expectedError)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ticketId = Guid.NewGuid();
            var branchId = Guid.NewGuid();

            context.InventoryCheckTickets.Add(new InventoryCheckTicket { TicketId = ticketId, BranchId = branchId, Status = initialStatus });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid(), branchId);
            var result = await controller.CancelInventoryCheck(ticketId, new MEP_Management_System.DTOs.InventoryDTOs.CancelInventoryCheckDto { Reason = "Lý do" });

            if (expectedError != null)
            {
                var badRequest = Assert.IsType<BadRequestObjectResult>(result);
                Assert.Contains(expectedError, badRequest.Value?.ToString() ?? "");
            }
            else
            {
                Assert.IsType<OkObjectResult>(result);
                var t = await context.InventoryCheckTickets.FindAsync(ticketId);
                Assert.Equal(InventoryCheckTicketStatus.Cancelled, t!.Status);
            }
        }

        // =====================================
        // 4. APPROVE INVENTORY CHECK
        // =====================================

        [Fact]
        public async Task ApproveInventoryCheck_Pending_UpdatesInventoryAndCreatesLedger()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var ticketId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var bpId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var bp = new BranchProduct { BranchProductId = bpId, BranchId = branchId, ProductId = productId, ActualStock = 10, AvailableStock = 10, CostPrice = 50000 };
            context.Products.Add(new Product { ProductId = productId, ProductCode = "P1" });
            context.BranchProducts.Add(bp);
            
            var ticket = new InventoryCheckTicket { TicketId = ticketId, BranchId = branchId, Status = InventoryCheckTicketStatus.WaitingForApproval };
            ticket.Details.Add(new InventoryCheckDetail
            {
                DetailId = Guid.NewGuid(),
                ProductId = productId,
                SystemQuantity = 100,
                ActualQuantity = 80,
                IsCounted = true,
                Discrepancy = -20
            });
            context.InventoryCheckTickets.Add(ticket);
            await context.SaveChangesAsync();

            var controller = CreateController(context, branchId, Guid.NewGuid(), "Owner");
            var result = await controller.ApproveInventoryCheck(ticketId);
            
            Assert.IsType<OkObjectResult>(result);

            var ticketInDb = await context.InventoryCheckTickets.FindAsync(ticketId);
            Assert.Equal(InventoryCheckTicketStatus.Completed, ticketInDb!.Status);

            var updatedBp = await context.BranchProducts.FirstAsync();
            Assert.Equal(80, updatedBp.ActualStock);

            var stockTicket = await context.StockTickets.Include(t => t.StockTicketItems).FirstOrDefaultAsync();
            Assert.NotNull(stockTicket);
            Assert.Equal("WRITE_OFF", stockTicket.TicketType);
            Assert.Equal(20, stockTicket.StockTicketItems.First().Discrepancy);
        }
    }
}
