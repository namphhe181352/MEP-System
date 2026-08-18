using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Controllers.SaleController;
using MEP_Management_System.Services.SaleServices;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.SaleController
{
    public class PrintingControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<IMapper> _mockMapper;

        public PrintingControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockMapper = new Mock<IMapper>();
        }

        private PrintingController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var templateService = new MEP_Management_System.Services.SaleServices.InvoicePrintTemplateServiceImpl(context);
            var controller = new PrintingController(new MEP_Management_System.Services.PrintingService(context, templateService, _mockMapper.Object));

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

        [Fact]
        public async Task PrintInvoice_ValidData_CreatesReceiptAndReturnsDto()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Chi nhánh 1" });
            var customerId = Guid.NewGuid();
            context.Customers.Add(new Customer { CustomerId = customerId, CustomerName = "C1", PhoneNumber = "0123" });
            context.Users.Add(new User { UserId = userId, Username = "test" });
            var productId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = productId, ProductCode = "P1", ProductName = "P1" });
            var branchProductId = Guid.NewGuid();
            context.BranchProducts.Add(new BranchProduct { BranchProductId = branchProductId, BranchId = branchId, ProductId = productId });
            context.Orders.Add(new Order { OrderId = orderId, BranchId = branchId, UserId = userId, CustomerId = customerId });
            context.Invoices.Add(new Invoice
            {
                InvoiceId = invoiceId,
                InvoiceCode = "INV-12345",
                OrderId = orderId,
                CreatedAt = DateTime.UtcNow,
                InvoiceItems = new List<InvoiceItem>
                {
                    new InvoiceItem { BranchProductId = branchProductId, Quantity = 2, SellPrice = 150000 }
                }
            });
            
            context.InvoicePrintTemplates.Add(new InvoicePrintTemplate
            {
                TemplateId = Guid.NewGuid(),
                BranchId = branchId,
                ThankYouMessage = "Cảm ơn quý khách!"
            });
            await context.SaveChangesAsync();

            _mockMapper.Setup(m => m.Map<ReceiptDto>(It.IsAny<Receipt>()))
                .Returns((Receipt r) => new ReceiptDto { ReceiptId = r.ReceiptId, ReceiptCode = r.ReceiptCode, HtmlContent = r.HtmlContent });

            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.PrintInvoice(invoiceId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<ReceiptDto>(okResult.Value);
            
            Assert.NotNull(dto.ReceiptCode);
            Assert.Contains("INV-12345", dto.HtmlContent);
            Assert.Contains("Cảm ơn quý khách!", dto.HtmlContent);
            
            var dbReceipt = await context.Receipts.FirstOrDefaultAsync(r => r.InvoiceId == invoiceId);
            Assert.NotNull(dbReceipt);
            Assert.Equal(dto.ReceiptCode, dbReceipt.ReceiptCode);
        }

        [Fact]
        public async Task DownloadPdf_ExistingReceipt_ReturnsFile()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            
            var userId = Guid.NewGuid();
            var customerId = Guid.NewGuid();
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "B1" });
            context.Customers.Add(new Customer { CustomerId = customerId, CustomerName = "C1", PhoneNumber = "123" });
            context.Users.Add(new User { UserId = userId, Username = "test" });
            context.Orders.Add(new Order { OrderId = orderId, BranchId = branchId, UserId = userId, CustomerId = customerId });
            context.Invoices.Add(new Invoice { InvoiceId = invoiceId, OrderId = orderId });
            context.Receipts.Add(new Receipt
            {
                ReceiptId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                ReceiptCode = "REC-001",
                HtmlContent = "<html><body>TEST RECEIPT</body></html>",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid(), branchId);

            // Act
            var result = await controller.DownloadPdf(invoiceId);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/html", fileResult.ContentType);
            Assert.Equal("REC-001.html", fileResult.FileDownloadName);
            
            var contentString = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
            Assert.Contains("TEST RECEIPT", contentString);
        }
    }
}



