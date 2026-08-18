using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.InventoryController;
using MEP_Management_System.Services.InventoryService;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.InventoryController
{
    public class StockPrintTemplateControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public StockPrintTemplateControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private StockPrintTemplateController CreateController(MEPDatabaseContext context, Guid branchId)
        {
            var service = new StockPrintTemplateService(context);
            var controller = new StockPrintTemplateController(context, service);

            var claims = new List<Claim>
            {
                new Claim("branchId", branchId.ToString())
            };

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            return controller;
        }

        [Fact]
        public async Task GetTemplate_ReturnsExistingTemplate()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();

            context.StockPrintTemplates.Add(new StockPrintTemplate
            {
                TemplateId = Guid.NewGuid(),
                BranchId = branchId,
                TicketType = "STOCK_CHECK",
                HeaderText = "Custom Header",
                IsActive = true
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, branchId);

            // Act
            var result = await controller.GetTemplate("STOCK_CHECK");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var template = Assert.IsType<StockPrintTemplate>(okResult.Value);
            
            Assert.Equal("Custom Header", template.HeaderText);
        }

        [Fact]
        public async Task UpsertTemplate_NewTemplate_CreatesTemplate()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, branchId);

            var request = new UpsertStockPrintTemplateRequest
            {
                TicketType = "PURCHASE",
                HeaderText = "New Header"
            };

            // Act
            var result = await controller.UpsertTemplate(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var dbTemplate = await context.StockPrintTemplates.FirstOrDefaultAsync(t => t.BranchId == branchId && t.TicketType == "PURCHASE");
            Assert.NotNull(dbTemplate);
            Assert.Equal("New Header", dbTemplate.HeaderText);
        }

        [Fact]
        public async Task DeleteTemplate_ValidId_DeletesTemplate()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var templateId = Guid.NewGuid();

            context.StockPrintTemplates.Add(new StockPrintTemplate
            {
                TemplateId = templateId,
                BranchId = branchId,
                TicketType = "STOCK_CHECK"
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, branchId);

            // Act
            var result = await controller.DeleteTemplate(templateId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            
            var dbTemplate = await context.StockPrintTemplates.FindAsync(templateId);
            Assert.Null(dbTemplate);
        }
        
        [Fact]
        public async Task Preview_ValidId_ReturnsHtml()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var ticketId = Guid.NewGuid();

            context.Branches.Add(new Branch { BranchId = branchId, BranchName = "Cửa hàng Kim khí Điện nước 1" });
            context.StockTickets.Add(new StockTicket
            {
                StockTicketId = ticketId,
                BranchId = branchId,
                TicketCode = "TCK_001",
                TicketType = "STOCK_CHECK"
            });
            
            context.StockPrintTemplates.Add(new StockPrintTemplate
            {
                TemplateId = Guid.NewGuid(),
                BranchId = branchId,
                TicketType = "STOCK_CHECK",
                IsActive = true
            });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, branchId);

            // Act
            var result = await controller.Preview(ticketId);

            // Assert
            var contentResult = Assert.IsType<ContentResult>(result);
            Assert.Equal("text/html", contentResult.ContentType);
            Assert.Contains("TCK_001", contentResult.Content);
        }
    }
}
