using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers;
using MEP_Management_System.Services.AdminDashboardServices;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.AdminController
{
    public class AdminDashboardControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public AdminDashboardControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private AdminDashboardController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new AdminDashboardController(new AdminDashboardService(context));

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, "Admin")
            };
            
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
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
        public async Task GetOverview_ReturnsCorrectStatistics()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            context.Branches.Add(new Branch { BranchId = Guid.NewGuid(), BranchName = "B1", IsActive = 1 });
            context.Products.Add(new Product { ProductId = Guid.NewGuid(), ProductCode = "P1", ProductName = "Prod1", Status = 1 });
            context.Users.Add(new User { UserId = Guid.NewGuid(), Username = "U1", IsActive = 1 });
            context.Invoices.Add(new Invoice { InvoiceId = Guid.NewGuid(), InvoiceCode = "INV1", Status = "COMPLETED", TotalAmount = 500000, CreatedAt = DateTime.UtcNow });
            context.Orders.Add(new Order { OrderId = Guid.NewGuid(), OrderCode = "O1", PaymentMethod = "TRANSFER", TotalAmount = 1000000, CreatedAt = DateTime.UtcNow });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.GetOverview();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var overview = Assert.IsType<DashboardOverviewDto>(okResult.Value);
            
            Assert.Equal(1, overview.ActiveBranches);
            Assert.Equal(1, overview.ActiveProducts);
            Assert.Equal(1, overview.ActiveTenants);
            Assert.Equal(500000m, overview.SubscriptionRevenue);
            Assert.Equal(10000m, overview.B2bCommission); // 1% of 1000000
        }

        [Fact]
        public async Task GetRevenueChart_ReturnsDataForAllMonths()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            int year = 2026;
            context.Invoices.Add(new Invoice { InvoiceId = Guid.NewGuid(), InvoiceCode = "INV1", Status = "COMPLETED", TotalAmount = 1000, CreatedAt = new DateTime(year, 5, 15) });
            context.Invoices.Add(new Invoice { InvoiceId = Guid.NewGuid(), InvoiceCode = "INV2", Status = "COMPLETED", TotalAmount = 2000, CreatedAt = new DateTime(year, 5, 20) });
            context.Invoices.Add(new Invoice { InvoiceId = Guid.NewGuid(), InvoiceCode = "INV3", Status = "COMPLETED", TotalAmount = 1500, CreatedAt = new DateTime(year, 8, 10) });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.GetRevenueChart(year);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var chartData = Assert.IsType<List<RevenueChartPointDto>>(okResult.Value);

            Assert.Equal(12, chartData.Count);
            
            var mayData = chartData.First(d => d.Month == 5);
            Assert.Equal(3000, mayData.Revenue);
            Assert.Equal(2, mayData.OrderCount);

            var augData = chartData.First(d => d.Month == 8);
            Assert.Equal(1500, augData.Revenue);
            Assert.Equal(1, augData.OrderCount);

            var janData = chartData.First(d => d.Month == 1);
            Assert.Equal(0, janData.Revenue);
            Assert.Equal(0, janData.OrderCount);
        }

        [Fact]
        public async Task GetRecentEvents_LimitsCorrectly()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var user = new User { UserId = Guid.NewGuid(), Username = "test", FullName = "Test" };
            context.Users.Add(user);
            for (int i = 0; i < 25; i++)
            {
                context.SystemLogs.Add(new SystemLog { LogId = Guid.NewGuid(), UserId = user.UserId, Action = $"ACTION_{i}", TableName = "System", Timestamp = DateTime.UtcNow.AddMinutes(i) });
            }
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            
            // Default limit is 20
            var result1 = await controller.GetRecentEvents();
            var okResult1 = Assert.IsType<OkObjectResult>(result1);
            var events1 = Assert.IsType<List<SystemEventDto>>(okResult1.Value);
            Assert.Equal(20, events1.Count);

            // Custom limit
            var result2 = await controller.GetRecentEvents(10);
            var okResult2 = Assert.IsType<OkObjectResult>(result2);
            var events2 = Assert.IsType<List<SystemEventDto>>(okResult2.Value);
            Assert.Equal(10, events2.Count);
        }

        [Fact]
        public async Task Export_CsvFormat_ReturnsCsvFile()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.Export("csv");

            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/csv", fileResult.ContentType);
            Assert.StartsWith("mep-dashboard-", fileResult.FileDownloadName);
        }

        [Fact]
        public async Task Export_ExcelFormat_ReturnsExcelFile()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            var result = await controller.Export("excel");

            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
            Assert.StartsWith("mep-dashboard-", fileResult.FileDownloadName);
        }
    }
}


