using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.SaleController;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.SaleController
{
    public class InvoicePrintTemplateControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public InvoicePrintTemplateControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private InvoicePrintTemplateController CreateController(MEPDatabaseContext context, Guid? branchId = null)
        {
            var service = new InvoicePrintTemplateService(context);
            var controller = new InvoicePrintTemplateController(context, service);

            var claims = new List<Claim>();
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
        public async Task GetTemplate_NoTemplateExists_ReturnsDefault()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId);

            // Act
            var result = await controller.GetTemplate();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var template = Assert.IsType<InvoicePrintTemplate>(okResult.Value);
            Assert.Equal("K80", template.PaperSize);
            Assert.True(template.ShowCustomerInfo);
            Assert.Equal("Cảm ơn quý khách!", template.ThankYouMessage);
        }

        [Fact]
        public async Task UpsertTemplate_NewTemplate_CreatesTemplate()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId);
            
            var request = new UpsertInvoicePrintTemplateRequest
            {
                BranchName = "Cửa hàng Thiết bị điện",
                PaperSize = "A4",
                ThankYouMessage = "Hẹn gặp lại quý khách!"
            };

            // Act
            var result = await controller.UpsertTemplate(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var dbTemplate = await context.InvoicePrintTemplates.FirstOrDefaultAsync(t => t.BranchId == branchId);
            Assert.NotNull(dbTemplate);
            Assert.Equal("Cửa hàng Thiết bị điện", dbTemplate.BranchName);
            Assert.Equal("A4", dbTemplate.PaperSize);
            Assert.Equal("Hẹn gặp lại quý khách!", dbTemplate.ThankYouMessage);
        }

        [Fact]
        public async Task UpsertTemplate_ExistingTemplate_UpdatesTemplate()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            
            context.InvoicePrintTemplates.Add(new InvoicePrintTemplate
            {
                TemplateId = Guid.NewGuid(),
                BranchId = branchId,
                BranchName = "Old Name"
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, branchId);
            
            var request = new UpsertInvoicePrintTemplateRequest
            {
                BranchName = "Cửa hàng Thiết bị điện Thịnh Phát",
                PaperSize = "K80",
                ThankYouMessage = "Hẹn gặp lại quý khách!"
            };

            // Act
            var result = await controller.UpsertTemplate(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var dbTemplate = await context.InvoicePrintTemplates.FirstOrDefaultAsync(t => t.BranchId == branchId);
            Assert.NotNull(dbTemplate);
            Assert.Equal("Cửa hàng Thiết bị điện Thịnh Phát", dbTemplate.BranchName); // Updated
        }

        [Fact]
        public async Task Preview_ValidInvoice_ReturnsHtml()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Chi nhánh chính" });
            
            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, Username = "testuser" });

            var productId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = productId, ProductCode = "P01", ProductName = "Prod" });

            var branchProductId = Guid.NewGuid();
            context.BranchProducts.Add(new BranchProduct { BranchProductId = branchProductId, BranchId = branchId, ProductId = productId });

            var orderId = Guid.NewGuid();
            context.Orders.Add(new Order { OrderId = orderId, BranchId = branchId, UserId = userId });
            context.Invoices.Add(new Invoice
            {
                InvoiceId = invoiceId,
                InvoiceCode = "INV-123",
                OrderId = orderId,
                CreatedAt = DateTime.UtcNow,
                InvoiceItems = new List<InvoiceItem>
                {
                    new InvoiceItem { BranchProductId = branchProductId, Quantity = 1, SellPrice = 100000 }
                }
            });
            
            context.InvoicePrintTemplates.Add(new InvoicePrintTemplate
            {
                TemplateId = Guid.NewGuid(),
                BranchId = branchId,
                ThankYouMessage = "Cảm ơn quý khách!"
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, branchId);

            var testInvoice = await context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);
            Assert.NotNull(testInvoice);
            var testInvoiceWithInclude = await context.Invoices.Include(i => i.Order).ThenInclude(o => o.Branch).FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
            Assert.NotNull(testInvoiceWithInclude?.Order?.Branch);

            // Act
            var result = await controller.Preview(invoiceId);

            // Assert
            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal("text/html", contentResult.ContentType);
            Assert.Contains("INV-123", contentResult.Content);
            Assert.Contains("Cảm ơn quý khách!", contentResult.Content);
        }
    }
}
