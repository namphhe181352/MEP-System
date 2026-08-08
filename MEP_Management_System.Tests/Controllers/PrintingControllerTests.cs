using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class PrintingControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public PrintingControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private PrintingController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new PrintingController(context);

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

            return controller;
        }

        [Fact]
        public async Task GetPrintTemplate_ReturnsTemplateHtml()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());
            var branchId = Guid.NewGuid();

            context.PrintTemplates.Add(new PrintTemplate 
            { 
                TemplateId = Guid.NewGuid(), 
                BranchId = branchId, 
                TemplateType = "INVOICE", 
                TemplateContent = "<html><body>Hóa đơn bán hàng</body></html>" 
            });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetPrintTemplate(branchId, "INVOICE");

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var data = okResult.Value;
            var type = data.GetType();
            
            var content = type.GetProperty("TemplateContent")?.GetValue(data, null) as string;
            Assert.Contains("Hóa đơn bán hàng", content);
        }
    }
}
