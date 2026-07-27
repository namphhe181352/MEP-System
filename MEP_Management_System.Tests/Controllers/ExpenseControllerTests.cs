using System;
using System.Collections.Generic;
using System.Linq;
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

namespace MEP_Management_System.Tests.Controllers
{
    public class ExpenseControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public ExpenseControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private ExpenseController CreateController(MEPDatabaseContext context, Guid branchId, Guid? userId = null, string role = "Owner")
        {
            var mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
            mockBranchAccessor.Setup(m => m.GetBranchId()).Returns(branchId);

            var controller = new ExpenseController(context, mockBranchAccessor.Object);

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
        public async Task GetExpenseCategories_ReturnsCategories()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId);

            context.ExpenseCategories.Add(new ExpenseCategory { CategoryId = Guid.NewGuid(), CategoryName = "Test Category", IsActive = 1 });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetExpenseCategories();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<List<ExpenseCategoryDto>>>(okResult.Value);
            Assert.True(response.Success);
            Assert.Single(response.Data!);
            Assert.Equal("Test Category", response.Data.First().CategoryName);
        }

        [Fact]
        public async Task CreateExpenseCategory_ValidData_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, branchId, userId);

            var dto = new ExpenseCategoryUpsertDto { CategoryName = "New Category" };

            // Act
            var result = await controller.CreateExpenseCategory(dto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<ExpenseCategoryDto>>(okResult.Value);
            Assert.True(response.Success);
            Assert.Equal("New Category", response.Data!.CategoryName);
        }

        [Fact]
        public async Task CreateExpenseVoucher_ValidData_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, branchId, userId);

            var categoryId = Guid.NewGuid();
            context.ExpenseCategories.Add(new ExpenseCategory { CategoryId = categoryId, CategoryName = "Test Category", IsActive = 1 });
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Test Branch" });
            context.Users.Add(new User { UserId = userId, FullName = "Test User", Email = "test@test.com", Username = "test", PasswordHash = "hash" });
            await context.SaveChangesAsync();

            var dto = new ExpenseVoucherCreateDto { CategoryId = categoryId, Amount = 1000, Reason = "Test Reason" };

            // Act
            var result = await controller.CreateExpenseVoucher(dto, branchId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<ExpenseVoucherResponseDto>>(okResult.Value);
            Assert.True(response.Success);
            Assert.Equal(1000, response.Data!.Amount);
            Assert.Equal("Test Reason", response.Data.Reason);
            Assert.Equal("PENDING", response.Data.Status);
        }

        [Fact]
        public async Task GetExpenseVouchers_ReturnsVouchers()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, branchId, userId);

            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Test Branch" });
            context.Users.Add(new User { UserId = userId, FullName = "Test User", Email = "test@test.com", Username = "test", PasswordHash = "hash" });

            var categoryId = Guid.NewGuid();
            context.ExpenseCategories.Add(new ExpenseCategory { CategoryId = categoryId, CategoryName = "Test Category", IsActive = 1 });

            context.ExpenseVouchers.Add(new ExpenseVoucher 
            { 
                VoucherId = Guid.NewGuid(), 
                VoucherCode = "PC001",
                BranchId = branchId, 
                CategoryId = categoryId,
                CreatedBy = userId,
                Amount = 1000, 
                Reason = "Test Reason", 
                Status = "PENDING" 
            });
            await context.SaveChangesAsync();

            var queryParams = new ExpenseVoucherParameters { PageNumber = 1, PageSize = 10, Status = "PENDING" };

            // Act
            var result = await controller.GetExpenseVouchers(queryParams, branchId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<PaginatedResponse<ExpenseVoucherResponseDto>>>(okResult.Value);
            Assert.True(response.Success);
            Assert.Single(response.Data!.Items);
            Assert.Equal(1000, response.Data.Items.First().Amount);
        }
    }
}
