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
    public class CustomersControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<IMapper> _mockMapper;

        public CustomersControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockMapper = new Mock<IMapper>();
        }

        private CustomersController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var controller = new CustomersController(context, _mockMapper.Object);

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
        public async Task GetCustomers_ValidBranch_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var controller = CreateController(context, Guid.NewGuid(), branchId);

            context.Customers.Add(new Customer { CustomerId = Guid.NewGuid(), CustomerName = "John Doe", BranchId = branchId });
            await context.SaveChangesAsync();

            _mockMapper.Setup(m => m.Map<CustomerDto>(It.IsAny<Customer>())).Returns(new CustomerDto { CustomerName = "John Doe" });

            // Act
            var result = await controller.GetCustomers(search: null, group: null, page: 1, pageSize: 20);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var pageResult = Assert.IsType<PageResultDto<CustomerDto>>(okResult.Value);
            Assert.Single(pageResult.Items);
            Assert.Equal("John Doe", pageResult.Items.First().CustomerName);
        }

        [Fact]
        public async Task CreateCustomer_InvalidPhoneFormat_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid(), Guid.NewGuid());

            var request = new CreateCustomerRequest { CustomerName = "Jane Doe", PhoneNumber = "invalid-phone" };

            // Act
            var result = await controller.CreateCustomer(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(badRequestResult.Value);
            Assert.Equal("MSG-67", errorResponse.Error.Code);
        }

        [Fact]
        public async Task CreateCustomer_ValidData_ReturnsCreated()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var request = new CreateCustomerRequest { CustomerName = "Jane Doe", PhoneNumber = "0987654321" };

            _mockMapper.Setup(m => m.Map<Customer>(request)).Returns(new Customer { CustomerName = "Jane Doe", PhoneNumber = "0987654321" });
            _mockMapper.Setup(m => m.Map<CustomerDto>(It.IsAny<Customer>())).Returns(new CustomerDto { CustomerName = "Jane Doe", PhoneNumber = "0987654321" });

            // Act
            var result = await controller.CreateCustomer(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var dto = Assert.IsType<CustomerDto>(createdResult.Value);
            Assert.Equal("Jane Doe", dto.CustomerName);
            
            var customerInDb = await context.Customers.FirstOrDefaultAsync(c => c.PhoneNumber == "0987654321");
            Assert.NotNull(customerInDb);
            Assert.Equal(branchId, customerInDb.BranchId);
        }

        [Fact]
        public async Task CreateCustomer_DuplicatePhone_ReturnsConflict()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            context.Customers.Add(new Customer { CustomerId = Guid.NewGuid(), CustomerName = "Existing", PhoneNumber = "0987654321", BranchId = branchId });
            await context.SaveChangesAsync();

            var request = new CreateCustomerRequest { CustomerName = "New Customer", PhoneNumber = "0987654321" };

            // Act
            var result = await controller.CreateCustomer(request);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(conflictResult.Value);
            Assert.Equal("MSG-68", errorResponse.Error.Code);
        }
    }
}
