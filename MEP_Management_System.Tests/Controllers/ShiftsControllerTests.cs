using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Controllers.SaleController;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Hubs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class ShiftsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<IMapper> _mockMapper;
        private Mock<IHubContext<MepHub>> _mockMepHub;
        private Mock<IHubContext<PosHub>> _mockPosHub;
        private MepNotificationService _notificationService;

        public ShiftsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockMapper = new Mock<IMapper>();

            _mockMepHub = new Mock<IHubContext<MepHub>>();
            _mockPosHub = new Mock<IHubContext<PosHub>>();

            var mockClientProxy = new Mock<IClientProxy>();
            var mockClients = new Mock<IHubClients>();
            mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
            _mockPosHub.Setup(h => h.Clients).Returns(mockClients.Object);

            _notificationService = new MepNotificationService(_mockMepHub.Object, _mockPosHub.Object);
        }

        private ShiftsController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var controller = new ShiftsController(context, _mockMapper.Object, _notificationService);

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
        public async Task StartShift_WithoutBranch_ReturnsBadRequest()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid(), null); // no branch

            var request = new CreateShiftRequest { OpeningBalance = 100 };

            // Act
            var result = await controller.StartShift(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(badRequestResult.Value);
            Assert.Equal("MSG-76", errorResponse.Error.Code);
        }

        [Fact]
        public async Task StartShift_AlreadyHasOpenShift_ReturnsConflict()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            context.Shifts.Add(new Shift { ShiftId = Guid.NewGuid(), BranchId = branchId, UserId = userId, Status = "OPEN", StartedAt = DateTime.UtcNow, ShiftCode = "S1" });
            await context.SaveChangesAsync();

            var request = new CreateShiftRequest { OpeningBalance = 100 };

            // Act
            var result = await controller.StartShift(request);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result.Result);
            var errorResponse = Assert.IsType<ApiErrorResponse>(conflictResult.Value);
            Assert.Equal("You already have an open shift", errorResponse.Error.Message);
        }

        [Fact]
        public async Task StartShift_Valid_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var request = new CreateShiftRequest { OpeningBalance = 500 };

            // Act
            var result = await controller.StartShift(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var shiftDto = Assert.IsType<ShiftDto>(okResult.Value);
            Assert.Equal(500, shiftDto.OpeningBalance);
            Assert.Equal("OPEN", shiftDto.Status);
            
            var shiftInDb = await context.Shifts.FirstOrDefaultAsync(s => s.UserId == userId);
            Assert.NotNull(shiftInDb);
            Assert.Equal(500, shiftInDb.OpeningBalance);
        }

        [Fact]
        public async Task EndShift_Valid_UpdatesShiftAndReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var branchId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId, branchId);

            var shiftId = Guid.NewGuid();
            context.Shifts.Add(new Shift { ShiftId = shiftId, BranchId = branchId, UserId = userId, Status = "OPEN", StartedAt = DateTime.UtcNow.AddHours(-4), ShiftCode = "S1", OpeningBalance = 100 });
            await context.SaveChangesAsync();

            var request = new EndShiftRequest { ActualCash = 200, Note = "Ended shift" };

            _mockMapper.Setup(m => m.Map<ShiftDto>(It.IsAny<Shift>())).Returns(new ShiftDto { ShiftId = shiftId, Status = "CLOSED" });

            // Act
            var result = await controller.EndShift(shiftId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            
            var shiftInDb = await context.Shifts.FirstOrDefaultAsync(s => s.ShiftId == shiftId);
            Assert.NotNull(shiftInDb);
            Assert.Equal("CLOSED", shiftInDb.Status);
            Assert.Equal(200, shiftInDb.ActualCash);
            Assert.Equal("Ended shift", shiftInDb.Note);
        }
    }
}
