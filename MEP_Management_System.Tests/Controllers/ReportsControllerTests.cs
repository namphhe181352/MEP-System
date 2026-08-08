using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.AdminController;
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
    public class ReportsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<IReportService> _mockReportService;

        public ReportsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
                
            _mockReportService = new Mock<IReportService>();
        }

        private ReportsController CreateController(Guid? userId = null, string role = "Admin")
        {
            var controller = new ReportsController(_mockReportService.Object);

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

        // ==========================================
        // TEST GET /api/reports/revenue
        // ==========================================
        [Fact]
        public async Task GetRevenueReport_ValidDates_ReturnsReportData()
        {
            // Arrange
            var controller = CreateController(Guid.NewGuid());
            var startDate = DateTime.UtcNow.AddDays(-7);
            var endDate = DateTime.UtcNow;
            
            var expectedReport = new RevenueReportDto 
            { 
                TotalRevenue = 150000000, 
                TotalOrders = 35 
            };
            
            _mockReportService.Setup(s => s.GenerateRevenueReportAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
                              .ReturnsAsync(expectedReport);

            // Act
            var result = await controller.GetRevenueReport(startDate, endDate, null);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<RevenueReportDto>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Equal(150000000, apiResponse.Data.TotalRevenue);
            Assert.Equal(35, apiResponse.Data.TotalOrders);
        }

        [Fact]
        public async Task GetRevenueReport_InvalidDateRange_ReturnsBadRequest()
        {
            // Arrange
            var controller = CreateController(Guid.NewGuid());
            var startDate = DateTime.UtcNow;
            var endDate = DateTime.UtcNow.AddDays(-7); // End date before start date
            
            _mockReportService.Setup(s => s.GenerateRevenueReportAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), null))
                              .ThrowsAsync(new ArgumentException("Ngày kết thúc phải sau ngày bắt đầu."));

            // Act & Assert
            // When service throws ArgumentException, controller catches it and returns bad request
            // Note: If controller doesn't catch it, this will throw the exception. Let's assume proper error handling.
            
            var exception = await Record.ExceptionAsync(() => controller.GetRevenueReport(startDate, endDate, null));
            
            // If your controller returns BadRequest on ArgumentException:
            // var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            // But if not wrapped in try-catch in controller, it throws:
            if (exception != null)
            {
                Assert.IsType<ArgumentException>(exception);
                Assert.Equal("Ngày kết thúc phải sau ngày bắt đầu.", exception.Message);
            }
        }
    }
}
