using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.InventoryController.Supplier;
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

namespace MEP_Management_System.UnitTests.Controllers.InventoryController.Supplier
{
    public class SupplierControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<ICurrentBranchAccessor> _mockBranchAccessor;
        private readonly Mock<ISupplierDebtService> _mockSupplierDebtService;

        public SupplierControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
            _mockSupplierDebtService = new Mock<ISupplierDebtService>();
            
            _mockSupplierDebtService.Setup(s => s.CalculateCurrentDebtBatchAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(new Dictionary<Guid, SupplierDebtSummary>());
        }

        private SupplierController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var controller = new SupplierController(context, _mockSupplierDebtService.Object, _mockBranchAccessor.Object);

            var claims = new List<Claim>();
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

            if (branchId.HasValue)
            {
                _mockBranchAccessor.Setup(x => x.GetBranchId()).Returns(branchId.Value);
            }
            else
            {
                _mockBranchAccessor.Setup(x => x.GetBranchId()).Returns((Guid?)null);
            }

            return controller;
        }

        [Fact]
        public async Task GetSuppliers_ReturnsSuppliersWithDebtInfo()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();

            context.Suppliers.Add(new MEP_Management_System.Models.Supplier { SupplierId = supplierId, BranchId = branchId, SupplierName = "Công ty Cổ phần Nhựa Bình Minh", Status = 1 });
            await context.SaveChangesAsync();
            
            var mockDebtData = new Dictionary<Guid, SupplierDebtSummary>
            {
                { supplierId, new SupplierDebtSummary { CurrentDebt = 15000000, TotalPurchased = 55000000, Status = "Có nợ" } }
            };
            _mockSupplierDebtService.Setup(s => s.CalculateCurrentDebtBatchAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateTime?>())).ReturnsAsync(mockDebtData);

            var controller = CreateController(context, userId, branchId);
            var query = new SupplierQueryParameters { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await controller.GetSuppliers(query);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<SupplierPagedResponse>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.Single(response.Data.Items);
            Assert.Equal("Công ty Cổ phần Nhựa Bình Minh", response.Data.Items[0].Name);
            Assert.Equal(15000000, response.Data.Items[0].CurrentDebt);
        }

        [Fact]
        public async Task CreateSupplier_ValidData_CreatesSupplier()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var request = new SupplierUpsertRequest
            {
                SupplierName = "Nhà phân phối điện cơ Trần Phú",
                ContactPhone = "0901234567",
                Status = 1
            };

            // Act
            var result = await controller.CreateSupplier(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<SupplierDetailDto>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.Equal("Nhà phân phối điện cơ Trần Phú", response.Data.Name);

            var dbSupplier = await context.Suppliers.FirstOrDefaultAsync(s => s.SupplierName == "Nhà phân phối điện cơ Trần Phú");
            Assert.NotNull(dbSupplier);
            Assert.Equal(branchId, dbSupplier.BranchId);
        }
        
        [Fact]
        public async Task CreateSupplier_DuplicateName_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            context.Suppliers.Add(new MEP_Management_System.Models.Supplier { SupplierId = Guid.NewGuid(), BranchId = branchId, SupplierName = "Đại lý Rạng Đông", Status = 1 });
            await context.SaveChangesAsync();
            
            var controller = CreateController(context, userId, branchId);

            var request = new SupplierUpsertRequest { SupplierName = "đại lý rạng đông", Status = 1 };

            // Act
            var result = await controller.CreateSupplier(request);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(badRequest.Value);
            
            Assert.False(response.Success);
            Assert.Contains("Tên nhà cung cấp đã tồn tại", response.Message);
        }

        [Fact]
        public async Task DeleteSupplier_WithDependencies_PerformsSoftDeleteAndRename()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var supplierId = Guid.NewGuid();

            context.Suppliers.Add(new MEP_Management_System.Models.Supplier { SupplierId = supplierId, BranchId = branchId, SupplierName = "Công ty Thiết bị điện Sino", Status = 1 });
            // Add a dependency
            context.Products.Add(new Product { ProductId = Guid.NewGuid(), SupplierId = supplierId, ProductName = "Ổ cắm Sino" });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.DeleteSupplier(supplierId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var dbSupplier = await context.Suppliers.FindAsync(supplierId);
            Assert.NotNull(dbSupplier); // Still in DB
            Assert.Equal((short)-1, dbSupplier.Status); // Soft deleted
            Assert.StartsWith("d_", dbSupplier.SupplierName); // Renamed
        }
    }
}
