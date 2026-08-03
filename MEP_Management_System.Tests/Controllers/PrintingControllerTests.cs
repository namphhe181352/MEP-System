using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Controllers.SaleController;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class PrintingControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<IMapper> _mockMapper;

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
            var templateService = new MEP_Management_System.Services.InvoicePrintTemplateService(context);
            var controller = new PrintingController(context, _mockMapper.Object, templateService);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
                claims.Add(new Claim("sub", userId.Value.ToString()));
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
        public async Task PrintInvoice_InvoiceNotFound_ReturnsNotFound()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid(), Guid.NewGuid());

            // Act
            var result = await controller.PrintInvoice(Guid.NewGuid());

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(notFoundResult.Value);
            Assert.Equal("MSG-01", errorResponse.Error.Code);
        }



        [Fact]
        public async Task DownloadPdf_ReceiptNotFound_ReturnsNotFound()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid(), Guid.NewGuid());

            // Act
            var result = await controller.DownloadPdf(Guid.NewGuid());

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(notFoundResult.Value);
            Assert.Equal("MSG-01", errorResponse.Error.Code);
        }

        [Fact]
        public async Task DownloadPdf_Valid_ReturnsFileResult()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var invoiceId = Guid.NewGuid();
            var orderId = Guid.NewGuid();
            
            var branch = new Branch { BranchId = branchId, BranchName = "Test" };
            context.Branches.Add(branch);
            
            var order = new Order { OrderId = orderId, OrderCode = "O1", BranchId = branchId, Branch = branch, Status = "Completed" };
            context.Orders.Add(order);
            
            var invoice = new Invoice { InvoiceId = invoiceId, InvoiceCode = "I1", OrderId = orderId, Status = "Completed", CreatedAt = DateTime.UtcNow, Order = order };
            context.Invoices.Add(invoice);

            var receipt = new Receipt { ReceiptId = Guid.NewGuid(), InvoiceId = invoiceId, Invoice = invoice, ReceiptCode = "RC-1", HtmlContent = "<html>Test</html>" };
            context.Receipts.Add(receipt);
            await context.SaveChangesAsync();

            // Act
            var result = await controller.DownloadPdf(invoiceId);

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/html", fileResult.ContentType);
            Assert.Equal("RC-1.html", fileResult.FileDownloadName);
        }
    }
}
