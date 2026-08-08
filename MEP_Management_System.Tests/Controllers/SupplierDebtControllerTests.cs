using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class SupplierDebtControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public SupplierDebtControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private SupplierDebtController CreateController(MEPDatabaseContext context, Guid? userId = null, string role = "Owner")
        {
            var controller = new SupplierDebtController(context);

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
        // TEST GET /api/supplier-debt
        // ==========================================
        [Fact]
        public async Task GetSupplierDebts_ReturnsAggregatedDebtData()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            var supplierId = Guid.NewGuid();
            context.Suppliers.Add(new Supplier { SupplierId = supplierId, SupplierName = "Nhà Phân Phối CocaCola", SupplierCode = "NCC-COCA" });
            
            // Create some stock tickets which represent inward purchases resulting in debt
            context.StockTickets.Add(new StockTicket 
            { 
                StockTicketId = Guid.NewGuid(), SupplierId = supplierId, TotalAmount = 15000000, 
                PaymentStatus = "DEBT", Status = "APPROVED" 
            });
            context.StockTickets.Add(new StockTicket 
            { 
                StockTicketId = Guid.NewGuid(), SupplierId = supplierId, TotalAmount = 5000000, 
                PaymentStatus = "DEBT", Status = "APPROVED" 
            });
            // Total debt should be 20,000,000

            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetSupplierDebts(supplierId: null, search: null, pageNumber: 1, pageSize: 10);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<PaginatedResponse<SupplierDebtSummaryDto>>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Single(apiResponse.Data.Items);
            
            var debtSummary = apiResponse.Data.Items.First();
            Assert.Equal("Nhà Phân Phối CocaCola", debtSummary.SupplierName);
            Assert.Equal(20000000, debtSummary.TotalDebt);
        }

        // ==========================================
        // TEST POST /api/supplier-debt/pay
        // ==========================================
        [Fact]
        public async Task PayDebt_ValidAmount_ReducesDebtCorrectly()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId);

            var supplierId = Guid.NewGuid();
            context.Suppliers.Add(new Supplier { SupplierId = supplierId, SupplierName = "Nhà Phân Phối Nước Suối", SupplierCode = "NCC-AQUA" });
            
            var ticketId = Guid.NewGuid();
            var ticket = new StockTicket 
            { 
                StockTicketId = ticketId, 
                SupplierId = supplierId, 
                TotalAmount = 5000000, 
                AmountPaid = 1000000, // They already paid 1M
                PaymentStatus = "PARTIAL", // Remaining debt is 4M
                Status = "APPROVED" 
            };
            context.StockTickets.Add(ticket);
            await context.SaveChangesAsync();

            var request = new SupplierDebtPaymentDto
            {
                SupplierId = supplierId,
                StockTicketId = ticketId,
                AmountToPay = 2000000, // Paying 2M
                PaymentMethod = "BANK_TRANSFER",
                Notes = "Chuyển khoản thanh toán đợt 2"
            };

            // Act
            var result = await controller.PayDebt(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            Assert.True(apiResponse.Success);
            
            // Check DB changes
            var dbTicket = await context.StockTickets.FindAsync(ticketId);
            Assert.Equal(3000000, dbTicket.AmountPaid); // 1M + 2M
            Assert.Equal("PARTIAL", dbTicket.PaymentStatus); // Still 2M left to pay
            
            var transaction = await context.PaymentTransactions.FirstOrDefaultAsync(p => p.ReferenceId == ticketId);
            Assert.NotNull(transaction);
            Assert.Equal(2000000, transaction.Amount);
            Assert.Equal("BANK_TRANSFER", transaction.PaymentMethod);
        }
    }
}
