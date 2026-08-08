using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers;
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
    public class ExpenseControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<ICurrentBranchAccessor> _mockBranchAccessor;

        public ExpenseControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
        }

        private ExpenseController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null, string role = "Owner")
        {
            var controller = new ExpenseController(context, _mockBranchAccessor.Object);

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

        // ==========================================
        // TEST POST /api/expense
        // ==========================================
        [Fact]
        public async Task CreateExpense_ValidData_CreatesPendingExpense()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var request = new CreateExpenseDto
            {
                ExpenseType = "UTILITY", // Điện nước
                Amount = 1500000,
                Description = "Thanh toán tiền điện tháng 8",
                ReferenceCode = "EVN-08-2026"
            };

            // Act
            var result = await controller.CreateExpense(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<ExpenseDto>>(createdResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Equal(1500000, apiResponse.Data.Amount);
            
            var dbExpense = await context.Expenses.FirstOrDefaultAsync(e => e.ExpenseId == apiResponse.Data.ExpenseId);
            Assert.NotNull(dbExpense);
            Assert.Equal("PENDING", dbExpense.Status); // Should require approval
            Assert.Equal(branchId, dbExpense.BranchId);
        }

        // ==========================================
        // TEST PUT /api/expense/{id}/approve
        // ==========================================
        [Fact]
        public async Task ApproveExpense_ByOwner_UpdatesStatusToApprovedAndCreatesTransaction()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var controller = CreateController(context, ownerId, branchId, "Owner");

            var expenseId = Guid.NewGuid();
            context.Expenses.Add(new Expense 
            { 
                ExpenseId = expenseId, 
                BranchId = branchId, 
                Amount = 500000, 
                ExpenseType = "OFFICE_SUPPLIES", 
                Status = "PENDING", 
                Description = "Mua giấy in A4" 
            });
            await context.SaveChangesAsync();

            var request = new ApproveExpenseDto
            {
                IsApproved = true,
                Notes = "Đã duyệt, lấy từ két sắt"
            };

            // Act
            var result = await controller.ApproveExpense(expenseId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<ExpenseDto>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Equal("APPROVED", apiResponse.Data.Status);
            
            var transaction = await context.PaymentTransactions.FirstOrDefaultAsync(t => t.ReferenceId == expenseId);
            Assert.NotNull(transaction); // Approving an expense should log an OUTFLOW transaction
            Assert.Equal(500000, transaction.Amount);
            Assert.Equal("EXPENSE", transaction.TransactionType);
        }
    }
}
