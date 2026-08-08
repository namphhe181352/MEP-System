using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.OwnerController;
using MEP_Management_System.DTOs.SaleDTOs;
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
    public class OwnerOrderDiscountTiersControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<ICurrentBranchAccessor> _mockBranchAccessor;

        public OwnerOrderDiscountTiersControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
        }

        private OwnerOrderDiscountTiersController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null, string role = "Owner")
        {
            var controller = new OwnerOrderDiscountTiersController(context, _mockBranchAccessor.Object);

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

            if (branchId.HasValue)
                _mockBranchAccessor.Setup(m => m.GetBranchId()).Returns(branchId.Value);

            return controller;
        }

        [Fact]
        public async Task AssignTierToBranch_ValidTier_AssignsSuccessfully()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, Guid.NewGuid(), branchId);

            var tierId = Guid.NewGuid();
            context.OrderDiscountTiers.Add(new OrderDiscountTier { TierId = tierId, TierName = "Giảm giá khai trương", IsActive = 1 });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.AssignTierToBranch(tierId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            // Verify db assignment
            var branchTier = await context.BranchOrderDiscountTiers.FirstOrDefaultAsync(b => b.BranchId == branchId && b.TierId == tierId);
            Assert.NotNull(branchTier);
            Assert.Equal(1, branchTier.IsActive);
        }
    }
}
