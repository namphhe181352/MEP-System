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
    public class ReturnsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public ReturnsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private ReturnsController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var controller = new ReturnsController(context);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
            }
            if (branchId.HasValue)
            {
                claims.Add(new Claim("branchId", branchId.Value.ToString()));
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
        // TEST POST /api/returns
        // ==========================================
        [Fact]
        public async Task ProcessReturn_ValidInvoice_RestoresStockAndCreatesReturnTicket()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var orderId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            context.Products.Add(new Product { ProductId = productId, ProductName = "Sạc dự phòng Anker", SellingPrice = 800000 });
            context.BranchProducts.Add(new BranchProduct { BranchId = branchId, ProductId = productId, StockQuantity = 10 }); // Currently 10 in stock
            
            // Existing Invoice
            var invoice = new Invoice 
            { 
                InvoiceId = invoiceId, 
                InvoiceCode = "INV-1234",
                OrderId = orderId, 
                Status = "Completed",
                TotalAmount = 800000
            };
            context.Invoices.Add(invoice);

            var orderDetailId = Guid.NewGuid();
            context.OrderDetails.Add(new OrderDetail 
            { 
                OrderDetailId = orderDetailId,
                OrderId = orderId, 
                ProductId = productId, 
                Quantity = 1, 
                UnitPrice = 800000 
            });

            await context.SaveChangesAsync();

            var request = new ReturnRequestDto
            {
                InvoiceId = invoiceId,
                Reason = "Sản phẩm bị lỗi nguồn",
                ReturnItems = new List<ReturnItemDto>
                {
                    new ReturnItemDto { OrderDetailId = orderDetailId, QuantityToReturn = 1 }
                }
            };

            // Act
            var result = await controller.ProcessReturn(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<ReturnResponseDto>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            Assert.Equal("Hoàn trả thành công.", apiResponse.Message);
            
            // Verify Stock was restored (10 + 1 = 11)
            var branchProduct = await context.BranchProducts.FirstOrDefaultAsync(bp => bp.ProductId == productId && bp.BranchId == branchId);
            Assert.Equal(11, branchProduct.StockQuantity);

            // Verify Return Log exists
            var returnLog = await context.Returns.FirstOrDefaultAsync(r => r.InvoiceId == invoiceId);
            Assert.NotNull(returnLog);
            Assert.Equal("Sản phẩm bị lỗi nguồn", returnLog.Reason);
            Assert.Equal(800000, returnLog.RefundAmount);
        }
    }
}
