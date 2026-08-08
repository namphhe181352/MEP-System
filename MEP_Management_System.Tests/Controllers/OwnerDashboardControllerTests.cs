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
            var customerDebtService = new MEP_Management_System.Services.CustomerDebtService(context);
            var controller = new OwnerDashboardController(context, _mockCurrentBranch.Object, customerDebtService);

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

        // ==========================================
        // TEST GET /api/owner/dashboard/overview
        // ==========================================
        [Fact]
        public async Task GetOverview_WithValidBranch_ReturnsAccurateBranchMetrics()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, userId);

            _mockCurrentBranch.Setup(m => m.GetBranchId()).Returns(branchId);

            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Chi nhánh Quận Tân Bình", IsActive = 1 });
            context.StaffAssignments.Add(new StaffAssignment { UserId = userId, BranchId = branchId });

            // Customers in this branch
            context.Customers.Add(new Customer { CustomerId = Guid.NewGuid(), BranchId = branchId, Status = 1, CurrentDebt = 1200000, CustomerName = "Cửa hàng Hữu Nghị" });
            
            // Orders in this branch (Last 30 days)
            var orderId = Guid.NewGuid();
            var order = new Order 
            { 
                OrderId = orderId, 
                OrderCode = "HD-001", 
                BranchId = branchId, 
                Status = "COMPLETED", 
                TotalAmount = 5000000, 
                CreatedAt = DateTime.UtcNow.AddDays(-2) 
            };
            context.Orders.Add(order);
            
            // Corresponding Invoice
            context.Invoices.Add(new Invoice 
            { 
                InvoiceId = Guid.NewGuid(), 
                InvoiceCode = "INV-001", 
                OrderId = orderId, 
                Order = order, 
                Status = "COMPLETED", 
                TotalAmount = 5000000, 
                CreatedAt = DateTime.UtcNow.AddDays(-2) 
            });
            
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetOverview();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<DashboardOverviewDto>(okResult.Value);
            
            Assert.Equal(branchId, dto.BranchId);
            Assert.Equal("Chi nhánh Quận Tân Bình", dto.BranchName);
            Assert.Equal(5000000, dto.TotalRevenueLast30Days); // Doanh thu 30 ngày
            Assert.Equal(1, dto.TotalOrdersLast30Days); // Tổng đơn hàng
            Assert.Equal(1, dto.ActiveCustomerCount); // Tổng KH hoạt động
            Assert.Equal(1200000, dto.ActiveCustomerTotalDebt); // Nợ tồn của khách
        }

        [Fact]
        public async Task GetOverview_NoBranchAssigned_ReturnsUnauthorized()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            _mockCurrentBranch.Setup(m => m.GetBranchId()).Returns((Guid?)null); // Branch missing

            // Act
            var result = await controller.GetOverview();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(unauthorizedResult.Value);
            Assert.False(response.Success);
            Assert.Contains("chi nhánh", response.Message.ToLower());
        }
    }
}
