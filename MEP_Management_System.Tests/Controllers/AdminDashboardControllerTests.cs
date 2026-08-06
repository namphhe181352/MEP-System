using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.Controllers;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class AdminDashboardControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public AdminDashboardControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private AdminDashboardController CreateController(MEPDatabaseContext context)
        {
            return new AdminDashboardController(context);
        }

        [Fact]
        public async Task GetOverview_ReturnsSystemLevelCounts()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);
            var branchId = Guid.NewGuid();

            // 1 active branch
            context.Branches.Add(new Branch { BranchId = branchId, BranchCode = "B1", BranchName = "Branch 1", IsActive = 1 });

            // 2 active products
            context.Products.Add(new Product { ProductId = Guid.NewGuid(), ProductCode = "P1", ProductName = "Product 1", Status = 1 });
            context.Products.Add(new Product { ProductId = Guid.NewGuid(), ProductCode = "P2", ProductName = "Product 2", Status = 1 });

            // 2 active tenants (1 owner + 1 staff)
            var ownerRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Owner" };
            var owner = new User { UserId = Guid.NewGuid(), IsActive = 1, FullName = "Owner", Email = "owner@test.com", Username = "owner", PasswordHash = "hash" };
            owner.Roles.Add(ownerRole);
            var staff = new User { UserId = Guid.NewGuid(), IsActive = 1, FullName = "Staff", Email = "staff@test.com", Username = "staff", PasswordHash = "hash" };
            context.Roles.Add(ownerRole);
            context.Users.AddRange(owner, staff);

            // Order with TRANSFER payment (feeds B2bCommission = 1% × 100000 = 1000)
            var transferOrderId = Guid.NewGuid();
            context.Orders.Add(new Order { OrderId = transferOrderId, OrderCode = "OT1", BranchId = branchId, PaymentMethod = "TRANSFER", TotalAmount = 100000, Status = "PENDING" });

            // Completed invoice in last 30d (feeds SubscriptionRevenue = 50000)
            var orderId = Guid.NewGuid();
            context.Orders.Add(new Order { OrderId = orderId, OrderCode = "O1", BranchId = branchId, PaymentMethod = "CASH", TotalAmount = 50000, Status = "COMPLETED" });
            context.Invoices.Add(new Invoice { InvoiceId = Guid.NewGuid(), InvoiceCode = "I1", OrderId = orderId, Status = "COMPLETED", TotalAmount = 50000, CreatedAt = DateTime.UtcNow });

            // Pending invoice (feeds OverdueAlerts = 1)
            var pendingOrderId = Guid.NewGuid();
            context.Orders.Add(new Order { OrderId = pendingOrderId, OrderCode = "O2", BranchId = branchId, PaymentMethod = "CASH", TotalAmount = 30000, Status = "PENDING" });
            context.Invoices.Add(new Invoice { InvoiceId = Guid.NewGuid(), InvoiceCode = "I2", OrderId = pendingOrderId, Status = "PENDING", TotalAmount = 30000, CreatedAt = DateTime.UtcNow });

            // Pending expense voucher (feeds PendingExpenseCount = 1)
            context.ExpenseVouchers.Add(new ExpenseVoucher
            {
                VoucherId = Guid.NewGuid(),
                VoucherCode = "EV1",
                BranchId = branchId,
                CreatedBy = owner.UserId,
                Reason = "Test expense",
                Amount = 10000,
                Status = "PENDING"
            });

            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetOverview();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var overview = Assert.IsType<DashboardOverviewDto>(okResult.Value);

            Assert.Equal(1, overview.ActiveBranches);
            Assert.Equal(2, overview.ActiveProducts);
            Assert.Equal(2, overview.ActiveTenants);
            Assert.Equal(50000m, overview.SubscriptionRevenue);
            Assert.Equal(1000m, overview.B2bCommission);
            Assert.Equal(1, overview.OverdueAlerts);
            // 2 PENDING orders: transferOrderId + pendingOrderId
            Assert.Equal(2, overview.PendingOrderCount);
            Assert.Equal(1, overview.PendingExpenseCount);
        }

        [Fact]
        public async Task GetRevenueChart_Returns12MonthsData()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);
            var branchId = Guid.NewGuid();
            var orderId = Guid.NewGuid();

            var thisYear = DateTime.UtcNow.Year;
            context.Orders.Add(new Order { OrderId = orderId, OrderCode = "O2", BranchId = branchId, Status = "COMPLETED" });
            context.Invoices.Add(new Invoice 
            { 
                InvoiceId = Guid.NewGuid(), 
                InvoiceCode = "I2",
                OrderId = orderId,
                Status = "COMPLETED", 
                TotalAmount = 50000, 
                CreatedAt = new DateTime(thisYear, 5, 1) 
            });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetRevenueChart(thisYear);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var chartData = Assert.IsType<List<RevenueChartPointDto>>(okResult.Value);
            Assert.Equal(12, chartData.Count);
            
            var mayData = chartData.First(d => d.Month == 5);
            Assert.Equal(50000, mayData.Revenue);
            Assert.Equal(1, mayData.OrderCount);
        }

        [Fact]
        public async Task GetRecentEvents_ReturnsEvents()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, IsActive = 1, FullName = "Admin", Email = "admin@test.com", Username = "admin", PasswordHash = "hash" });
            context.SystemLogs.Add(new SystemLog { LogId = Guid.NewGuid(), UserId = userId, Action = "Test Action", Description = "Test", Timestamp = DateTime.UtcNow, TableName = "SystemLog" });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetRecentEvents(10);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var events = Assert.IsType<List<SystemEventDto>>(okResult.Value);
            Assert.Single(events);
            Assert.Equal("Test Action", events[0].Action);
        }

        [Fact]
        public async Task Export_ReturnsCsvFile()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            // Act
            var result = await controller.Export();

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/csv", fileResult.ContentType);
            Assert.StartsWith("mep-dashboard-", fileResult.FileDownloadName);
            Assert.EndsWith(".csv", fileResult.FileDownloadName);
            Assert.NotEmpty(fileResult.FileContents);
        }
    }
}
