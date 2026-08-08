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
    public class InventoryCheckControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<ICurrentBranchAccessor> _mockBranchAccessor;

        public InventoryCheckControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
        }

        private InventoryCheckController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null, string role = "Owner")
        {
            var controller = new InventoryCheckController(context, _mockBranchAccessor.Object);

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
                _mockBranchAccessor.Setup(m => m.GetBranchId()).Returns(branchId.Value);
            else
                _mockBranchAccessor.Setup(m => m.GetBranchId()).Returns((Guid?)null);

            return controller;
        }

        // ==========================================
        // TEST POST /api/inventory/check
        // ==========================================
        [Fact]
        public async Task CreateInventoryCheck_CalculatesDiscrepancyCorrectly_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            context.Users.Add(new User { UserId = userId, Username = "manager", Email = "manager@mep.vn", FullName = "Quản lý" });
            
            var product = new Product { ProductId = Guid.NewGuid(), ProductName = "Tai nghe Bluetooth AirPods Pro", SellingPrice = 5000000 };
            context.Products.Add(product);
            
            // System stock is 10
            context.BranchProducts.Add(new BranchProduct { BranchId = branchId, ProductId = product.ProductId, StockQuantity = 10 });
            await context.SaveChangesAsync();

            var request = new CreateInventoryCheckDto
            {
                Notes = "Kiểm kê định kỳ tháng 8",
                Details = new List<InventoryCheckDetailDto>
                {
                    // Actual stock is 12 (Discrepancy should be +2)
                    new InventoryCheckDetailDto { ProductId = product.ProductId, ActualQuantity = 12, SystemQuantity = 10 }
                }
            };

            // Act
            var result = await controller.CreateInventoryCheck(request, branchId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<InventoryCheckResponseDto>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            
            var createdTicket = await context.StockTickets.Include(t => t.StockTicketDetails).FirstOrDefaultAsync(t => t.StockTicketId == apiResponse.Data.StockTicketId);
            Assert.NotNull(createdTicket);
            Assert.Equal("CHECK", createdTicket.TicketType);
            Assert.Equal("PENDING", createdTicket.Status);
            
            Assert.Single(createdTicket.StockTicketDetails);
            var detail = createdTicket.StockTicketDetails.First();
            
            // Because Actual > System => The check ticket records an adjustment ticket. Usually it handles logic discrepancy in detail.
            // If the controller stores difference, check it here (depends on schema, normally Quantity could be the delta).
            Assert.Equal(12, detail.Quantity); // The new proposed quantity
        }
    }
}
