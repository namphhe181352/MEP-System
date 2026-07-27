using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.InventoryController;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
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
    public class ReportsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public ReportsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private ReportsController CreateController(MEPDatabaseContext context, Guid? branchId = null, Guid? userId = null, string role = "Owner")
        {
            var mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
            mockBranchAccessor.Setup(m => m.GetBranchId()).Returns(branchId);

            var reportService = new ReportService(context, mockBranchAccessor.Object);
            var controller = new ReportsController(context, mockBranchAccessor.Object, reportService);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
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

            return controller;
        }

        [Fact]
        public async Task GetDailyEndReport_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId, Guid.NewGuid());

            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Test Branch" });
            await context.SaveChangesAsync();

            var request = new DailyEndReportRequestDto { Date = DateTime.UtcNow.Date };

            // Act
            var result = await controller.GetDailyEndReport(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<DailyEndReportDto>>(okResult.Value);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
        }

        [Fact]
        public async Task GetStockMovementReport_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId, Guid.NewGuid());

            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Test Branch" });
            await context.SaveChangesAsync();

            var request = new StockMovementReportRequestDto 
            { 
                FromDate = DateTime.UtcNow.AddDays(-7),
                ToDate = DateTime.UtcNow
            };

            // Act
            var result = await controller.GetStockMovementReport(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<StockMovementReportDto>>(okResult.Value);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
        }

        [Fact]
        public async Task GetLowStockReport_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId, Guid.NewGuid());

            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Test Branch" });
            await context.SaveChangesAsync();

            var request = new LowStockReportRequestDto { IncludeZeroStock = true };

            // Act
            var result = await controller.GetLowStockReport(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<LowStockReportDto>>(okResult.Value);
            Assert.True(response.Success);
            Assert.NotNull(response.Data);
        }
    }
}
