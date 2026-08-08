using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.AdminController;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Hubs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class SystemNotificationsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private Mock<IHubContext<MepHub>> _mockHub;
        private Mock<IClientProxy> _mockClientProxy;
        private Mock<IHubClients> _mockClients;

        public SystemNotificationsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockClientProxy = new Mock<IClientProxy>();
            _mockClients = new Mock<IHubClients>();
            _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
            
            _mockHub = new Mock<IHubContext<MepHub>>();
            _mockHub.Setup(h => h.Clients).Returns(_mockClients.Object);
        }

        private SystemNotificationsController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new SystemNotificationsController(context, _mockHub.Object);

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

        // ==========================================
        // TEST POST /api/admin/system-notifications
        // ==========================================
        [Fact]
        public async Task CreateNotification_ValidData_SavesToDbAndBroadcastsViaSignalR()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            var request = new CreateNotificationDto
            {
                Title = "Bảo trì hệ thống",
                Message = "Hệ thống sẽ bảo trì từ 22h - 24h tối nay.",
                Type = "SYSTEM_ALERT"
            };

            // Act
            var result = await controller.CreateNotification(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var apiResponse = Assert.IsType<ApiResponse<object>>(okResult.Value);
            
            Assert.True(apiResponse.Success);
            
            // Check Database 
            var dbNoti = await context.SystemNotifications.FirstOrDefaultAsync(n => n.Title == "Bảo trì hệ thống");
            Assert.NotNull(dbNoti);
            Assert.Equal("SYSTEM_ALERT", dbNoti.Type);

            // Check SignalR Broadcast invocation
            _mockClientProxy.Verify(
                clientProxy => clientProxy.SendCoreAsync(
                    "ReceiveSystemNotification", 
                    It.Is<object[]>(o => o != null && o.Length == 1), 
                    default(CancellationToken)), 
                Times.Once);
        }

        // ==========================================
        // TEST GET /api/admin/system-notifications
        // ==========================================
        [Fact]
        public async Task GetNotifications_ReturnsPaginatedList()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            context.SystemNotifications.Add(new SystemNotification 
            { 
                NotificationId = Guid.NewGuid(), Title = "Noti 1", Message = "Message 1", Type = "INFO", CreatedAt = DateTime.UtcNow 
            });
            context.SystemNotifications.Add(new SystemNotification 
            { 
                NotificationId = Guid.NewGuid(), Title = "Noti 2", Message = "Message 2", Type = "WARNING", CreatedAt = DateTime.UtcNow 
            });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetNotifications(page: 1, pageSize: 10);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<PageResultDto<SystemNotificationDto>>(okResult.Value);
            
            Assert.Equal(2, dto.TotalCount);
            Assert.Equal(2, dto.Items.Count());
        }
    }
}
