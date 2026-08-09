using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers;
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

namespace MEP_Management_System.UnitTests.Controllers
{
    public class ExpenseControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<ICurrentBranchAccessor> _mockBranchAccessor;

        public ExpenseControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            
            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
        }

        private ExpenseController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var controller = new ExpenseController(context, _mockBranchAccessor.Object);

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
                _mockBranchAccessor.Setup(b => b.GetBranchId()).Returns(branchId.Value);
            }

            return controller;
        }

        [Fact]
        public async Task GetExpenseCategories_WithValidBranch_ReturnsCategories()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            context.Branches.Add(new Branch { BranchId = branchId, IsActive = 1 });
            context.Users.Add(new User { UserId = userId, DefaultBranchId = branchId });
            context.ExpenseCategories.Add(new ExpenseCategory { CategoryId = Guid.NewGuid(), CategoryName = "Cat1", BranchId = branchId, CreatedBy = userId });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var result = await controller.GetExpenseCategories(branchId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<List<ExpenseCategoryDto>>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.Single(response.Data!);
        }

        [Fact]
        public async Task CreateExpenseCategory_ValidData_CreatesSuccessfully()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            context.Branches.Add(new Branch { BranchId = branchId, IsActive = 1 });
            context.Users.Add(new User { UserId = userId, DefaultBranchId = branchId });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var request = new ExpenseCategoryUpsertDto { CategoryName = "Cat2" };

            var result = await controller.CreateExpenseCategory(request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<ExpenseCategoryDto>>(okResult.Value);
            
            Assert.True(response.Success);
            var cat = await context.ExpenseCategories.FirstOrDefaultAsync(c => c.CategoryName == "Cat2");
            Assert.NotNull(cat);
        }

        [Fact]
        public async Task CreateExpenseVoucher_ValidData_CreatesSuccessfully()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            context.Branches.Add(new Branch { BranchId = branchId, IsActive = 1 });
            context.Users.Add(new User { UserId = userId, DefaultBranchId = branchId });
            context.ExpenseCategories.Add(new ExpenseCategory { CategoryId = categoryId, CategoryName = "Test", BranchId = branchId, IsActive = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var request = new ExpenseVoucherCreateDto { CategoryId = categoryId, Amount = 1000, Reason = "Test reason" };

            var result = await controller.CreateExpenseVoucher(request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<ExpenseVoucherResponseDto>>(okResult.Value);
            
            Assert.True(response.Success);
            var voucher = await context.ExpenseVouchers.FirstOrDefaultAsync();
            Assert.NotNull(voucher);
            Assert.Equal("PENDING", voucher.Status);
        }
        
        [Fact]
        public async Task ConfirmExpenseVoucher_ValidPending_ConfirmsSuccessfully()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var voucherId = Guid.NewGuid();
            context.Branches.Add(new Branch { BranchId = branchId, IsActive = 1 });
            context.Users.Add(new User { UserId = userId, DefaultBranchId = branchId });
            var catId = Guid.NewGuid();
            context.ExpenseCategories.Add(new ExpenseCategory { CategoryId = catId, CategoryName = "Test", BranchId = branchId });
            context.ExpenseVouchers.Add(new ExpenseVoucher { VoucherId = voucherId, VoucherCode = "VOUCHER1", Reason = "Reason", BranchId = branchId, Status = "PENDING", Amount = 100, CreatedBy = userId, CategoryId = catId });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var result = await controller.ConfirmExpenseVoucher(voucherId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<ExpenseVoucherResponseDto>>(okResult.Value);
            
            Assert.True(response.Success);
            var voucher = await context.ExpenseVouchers.FindAsync(voucherId);
            Assert.Equal("COMPLETED", voucher!.Status);
        }

        [Fact]
        public async Task CancelExpenseVoucher_ValidPending_CancelsSuccessfully()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var voucherId = Guid.NewGuid();
            context.Branches.Add(new Branch { BranchId = branchId, IsActive = 1 });
            context.Users.Add(new User { UserId = userId, DefaultBranchId = branchId });
            var catId = Guid.NewGuid();
            context.ExpenseCategories.Add(new ExpenseCategory { CategoryId = catId, CategoryName = "Test", BranchId = branchId });
            context.ExpenseVouchers.Add(new ExpenseVoucher { VoucherId = voucherId, VoucherCode = "VOUCHER1", Reason = "Reason", BranchId = branchId, Status = "PENDING", Amount = 100, CreatedBy = userId, CategoryId = catId });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            var request = new CancelExpenseVoucherDto { CancelReason = "Test cancel" };
            var result = await controller.CancelExpenseVoucher(voucherId, request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<ExpenseVoucherResponseDto>>(okResult.Value);
            
            Assert.True(response.Success);
            var voucher = await context.ExpenseVouchers.FindAsync(voucherId);
            Assert.Equal("CANCELLED", voucher!.Status);
            Assert.Equal("Test cancel", voucher.CancelReason);
        }
    }
}

