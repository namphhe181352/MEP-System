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
    public class CustomersControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public CustomersControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private CustomersController CreateController(MEPDatabaseContext context, Guid? userId = null, string role = "SalesStaff")
        {
            var controller = new CustomersController(context);

            var claims = new List<Claim>();
            if (userId.HasValue)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
                claims.Add(new Claim("userId", userId.Value.ToString()));
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
        // TEST GET /api/customers
        // ==========================================
        [Fact]
        public async Task GetCustomers_WithRealVietnameseData_ReturnsPaginatedList()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            context.Customers.Add(new Customer { CustomerId = Guid.NewGuid(), CustomerName = "Công ty Xây dựng Kiến Vàng", CustomerType = "DOANH_NGHIEP", Phone = "0283838383" });
            context.Customers.Add(new Customer { CustomerId = Guid.NewGuid(), CustomerName = "Thầu xây dựng Hữu Thắng", CustomerType = "CA_NHAN", Phone = "0977888999" });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetCustomers(search: "Thắng", group: null, type: null, isActive: null, pageNumber: 1, pageSize: 10);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<PageResultDto<CustomerDto>>(okResult.Value);
            
            Assert.Single(dto.Items);
            Assert.Equal("Thầu xây dựng Hữu Thắng", dto.Items.First().CustomerName);
            Assert.Equal("0977888999", dto.Items.First().Phone);
        }

        // ==========================================
        // TEST POST /api/customers
        // ==========================================
        [Fact]
        public async Task CreateCustomer_ValidBusinessCustomer_ReturnsCreatedResult()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            var request = new CustomerCreateDto
            {
                CustomerName = "Cửa hàng Thiết bị điện Thịnh Phát",
                CustomerType = "DOANH_NGHIEP",
                Phone = "1900232389",
                Email = "contact@thinhphat.net",
                TaxCode = "0101245486"
            };

            // Act
            var result = await controller.CreateCustomer(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            var dto = Assert.IsType<CustomerDto>(createdResult.Value);
            
            Assert.NotEqual(Guid.Empty, dto.CustomerId);
            Assert.Equal("Cửa hàng Thiết bị điện Thịnh Phát", dto.CustomerName);
            
            var dbCustomer = await context.Customers.FindAsync(dto.CustomerId);
            Assert.NotNull(dbCustomer);
            Assert.Equal("0101245486", dbCustomer.TaxCode);
        }

        [Fact]
        public async Task CreateCustomer_MissingRequiredName_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            var request = new CustomerCreateDto
            {
                CustomerName = "", // Missing Name
                CustomerType = "CA_NHAN",
                Phone = "0988777666"
            };

            // Act
            var result = await controller.CreateCustomer(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            var valueType = badRequestResult.Value.GetType();
            var error = valueType.GetProperty("Error")?.GetValue(badRequestResult.Value, null);
            var message = error?.GetType().GetProperty("Message")?.GetValue(error, null) as string;
            
            Assert.NotNull(message);
        }
    }
}
