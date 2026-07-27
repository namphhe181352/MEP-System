using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.OwnerController;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class OwnerDashboardControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<ICurrentBranchAccessor> _mockCurrentBranch;

        public OwnerDashboardControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockCurrentBranch = new Mock<ICurrentBranchAccessor>();
        }

        private OwnerDashboardController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new OwnerDashboardController(context, _mockCurrentBranch.Object);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
                claims.Add(new Claim("sub", userId.Value.ToString()));
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
        public async Task GetOverview_NoDefaultBranch_ReturnsUnauthorized()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            _mockCurrentBranch.Setup(m => m.GetBranchId()).Returns((Guid?)null);

            // Act
            var result = await controller.GetOverview();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(unauthorizedResult.Value);
            Assert.False(response.Success);
            Assert.Equal("Không tìm thấy chi nhánh mặc định của Owner.", response.Message);
        }

        [Fact]
        public async Task GetOverview_BranchNotFound_ReturnsNotFound()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);
            var branchId = Guid.NewGuid();

            _mockCurrentBranch.Setup(m => m.GetBranchId()).Returns(branchId);

            // Act
            var result = await controller.GetOverview();

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(notFoundResult.Value);
            Assert.False(response.Success);
            Assert.Equal("Không tìm thấy chi nhánh.", response.Message);
        }

        [Fact]
        public async Task GetOverview_Valid_ReturnsOkWithDashboardOverviewDto()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, userId);

            _mockCurrentBranch.Setup(m => m.GetBranchId()).Returns(branchId);

            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Main Branch", IsActive = 1 });
            context.StaffAssignments.Add(new StaffAssignment { UserId = userId, BranchId = branchId });

            // Add some customers and orders to have non-zero metrics
            context.Customers.Add(new Customer { CustomerId = Guid.NewGuid(), BranchId = branchId, Status = 1, CurrentDebt = 50000, CustomerName = "Test" });
            
            var orderId = Guid.NewGuid();
            context.Orders.Add(new Order { OrderId = orderId, OrderCode = "O1", BranchId = branchId, Status = "COMPLETED", TotalAmount = 150000, CreatedAt = DateTime.UtcNow.AddDays(-1) });
            context.Invoices.Add(new Invoice { InvoiceId = Guid.NewGuid(), InvoiceCode = "I1", OrderId = orderId, Status = "COMPLETED", TotalAmount = 150000, CreatedAt = DateTime.UtcNow.AddDays(-1) });
            
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetOverview();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<DashboardOverviewDto>(okResult.Value);
            Assert.Equal(branchId, dto.BranchId);
            Assert.Equal("Main Branch", dto.BranchName);
            Assert.Equal(150000, dto.TotalRevenueLast30Days);
            Assert.Equal(1, dto.TotalOrdersLast30Days);
            Assert.Equal(1, dto.ActiveBranches);
            Assert.Equal(1, dto.ActiveCustomerCount);
            Assert.Equal(50000, dto.ActiveCustomerTotalDebt);
        }
    }
}
