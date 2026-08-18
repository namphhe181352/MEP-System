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

namespace MEP_Management_System.UnitTests.Controllers.OwnerController
{
    public class OwnerDashboardControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<ICurrentBranchAccessor> _mockBranchAccessor;
        private readonly Mock<ISupplierDebtService> _mockSupplierDebtService;
        private readonly CustomerDebtService _customerDebtService;

        public OwnerDashboardControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
            _mockSupplierDebtService = new Mock<ISupplierDebtService>();
            
            var dbContext = new MEPDatabaseContext(_dbContextOptions);
            _customerDebtService = new CustomerDebtService(dbContext);
        }

        private OwnerDashboardController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var controller = new OwnerDashboardController(new MEP_Management_System.Services.OwnerServices.OwnerDashboardService(context, _customerDebtService, _mockSupplierDebtService.Object), _mockBranchAccessor.Object);
            
            _mockSupplierDebtService.Setup(s => s.CalculateCurrentDebtBatchAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new Dictionary<Guid, SupplierDebtSummary>());

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
                claims.Add(new Claim("userId", userId.Value.ToString()));
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
        }        [Theory]
        [InlineData("COMPLETED", 1000000, 1000000)] // Single completed order
        [InlineData("CANCELLED", 1000000, 0)] // Cancelled order shouldn't count towards revenue
        [InlineData("DRAFT", 1000000, 0)] // Draft shouldn't count
        [InlineData("COMPLETED", 0, 0)] // Zero value order
        [InlineData("COMPLETED", 500000, 500000)] // Different amount
        public async Task GetOverview_CalculatesMetricsCorrectly(string orderStatus, decimal orderAmount, decimal expectedRevenue)
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Test Branch", IsActive = 1 });
            var orderId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();

            context.Orders.Add(new Order
            {
                OrderId = orderId,
                BranchId = branchId,
                Status = orderStatus,
                TotalAmount = orderAmount,
                CreatedAt = DateTime.UtcNow
            });
            
            context.Invoices.Add(new Invoice
            {
                InvoiceId = invoiceId,
                OrderId = orderId,
                Status = orderStatus,
                TotalAmount = orderAmount,
                CreatedAt = DateTime.UtcNow
            });

            context.Payments.Add(new Payment
            {
                PaymentId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                BranchId = branchId,
                Amount = orderAmount,
                Status = "Confirmed",
                Method = "Cash",
                PaidAt = DateTime.UtcNow
            });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            var result = await controller.GetOverview();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<DashboardOverviewDto>(okResult.Value);
            
            Assert.NotNull(response);
            Assert.Equal(expectedRevenue, response.TotalRevenueToday);
        }

        [Fact]
        public async Task GetCustomerDebtOverview_ValidBranch_ReturnsOverview()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();

            context.Branches.Add(new Branch { BranchId = branchId, IsActive = 1 });
            context.Customers.Add(new Customer { CustomerId = Guid.NewGuid(), BranchId = branchId, Status = 1, DebtLimit = 1000, CurrentDebt = 1500, CustomerName = "Test" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            var result = await controller.GetCustomerDebtOverview();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<CustomerDebtOverviewDto>(okResult.Value);
            
            Assert.Equal(1, response.TotalCustomers);
        }
        
        [Fact]
        public async Task GetTopProducts_ValidBranch_ReturnsProducts()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();

            context.Branches.Add(new Branch { BranchId = branchId, IsActive = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            var result = await controller.GetTopProducts(null, null, 10, "revenue");

            var okResult = Assert.IsType<OkObjectResult>(result);
        }
    }
}




