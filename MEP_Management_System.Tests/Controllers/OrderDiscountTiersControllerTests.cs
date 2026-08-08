using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.AdminController;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class OrderDiscountTiersControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public OrderDiscountTiersControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private OrderDiscountTiersController CreateController(MEPDatabaseContext context, Guid? userId = null, string role = "Admin")
        {
            var controller = new OrderDiscountTiersController(context);

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
        public async Task CreateTier_ValidData_CreatesSystemWideDiscountTier()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            var request = new CreateOrderDiscountTierDto
            {
                TierName = "Giảm giá khách hàng VIP",
                MinOrderAmount = 10000000, // 10 triệu
                DiscountType = "PERCENT", // Phần trăm
                DiscountValue = 10, // 10%
                IsActive = true
            };

            // Act
            var result = await controller.CreateTier(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var tierDto = Assert.IsType<OrderDiscountTierDto>(okResult.Value);
            
            Assert.Equal("Giảm giá khách hàng VIP", tierDto.TierName);
            
            var dbTier = await context.OrderDiscountTiers.FirstOrDefaultAsync(t => t.TierId == tierDto.TierId);
            Assert.NotNull(dbTier);
            Assert.Equal(10, dbTier.DiscountValue);
        }
    }
}
