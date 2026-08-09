using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Controllers.AdminController;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.AdminController
{
    public class SystemNotificationsControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<INotificationSender> _mockSender;

        public SystemNotificationsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockMapper = new Mock<IMapper>();
            _mockSender = new Mock<INotificationSender>();
        }

        private SystemNotificationsController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new SystemNotificationsController(context, _mockMapper.Object, _mockSender.Object);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Role, "Admin")
            };
            
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
        public async Task GetNotifications_ReturnsPagedList()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            context.SystemNotifications.Add(new SystemNotification { NotificationId = Guid.NewGuid(), Status = "SENT", Title = "T1", Target = "ALL", Content = "C1" });
            context.SystemNotifications.Add(new SystemNotification { NotificationId = Guid.NewGuid(), Status = "DRAFT", Title = "T2", Target = "ALL", Content = "C2" });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            _mockMapper.Setup(m => m.Map<List<SystemNotificationDto>>(It.IsAny<List<SystemNotification>>())).Returns(new List<SystemNotificationDto>());

            var result = await controller.GetNotifications("SENT", 1, 10);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var pageResult = Assert.IsType<PageResultDto<SystemNotificationDto>>(okResult.Value);
            
            Assert.Equal(1, pageResult.TotalCount); // Only one is SENT
        }

        [Fact]
        public async Task CreateNotification_ValidTarget_CreatesDraft()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var controller = CreateController(context, userId);
            
            var request = new CreateNotificationRequest { Title = "Test", Content = "Content", Target = "ALL" };

            var result = await controller.CreateNotification(request);

            var created = Assert.IsType<CreatedAtActionResult>(result);
            var notification = await context.SystemNotifications.FirstAsync();
            
            Assert.Equal("Test", notification.Title);
            Assert.Equal("ALL", notification.Target);
            Assert.Equal("DRAFT", notification.Status);
            Assert.Equal(userId, notification.CreatedBy);
        }

        [Fact]
        public async Task CreateNotification_InvalidTarget_ReturnsBadRequest()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());
            var request = new CreateNotificationRequest { Title = "Test", Content = "Content", Target = "INVALID" };

            var result = await controller.CreateNotification(request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Target không hợp lệ", badRequest.Value?.ToString() ?? "");
        }

        [Fact]
        public async Task UpdateNotification_DraftStatus_UpdatesSuccessfully()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var notifId = Guid.NewGuid();
            context.SystemNotifications.Add(new SystemNotification { NotificationId = notifId, Status = "DRAFT", Title = "Old", Target = "ALL", Content = "C1" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid());
            var request = new UpdateNotificationRequest { Title = "New", Target = "OWNER" };

            var result = await controller.UpdateNotification(notifId, request);

            Assert.IsType<OkObjectResult>(result);
            var updated = await context.SystemNotifications.FindAsync(notifId);
            Assert.Equal("New", updated!.Title);
            Assert.Equal("OWNER", updated.Target);
        }

        [Fact]
        public async Task SendNotification_SendsAndCallsService()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var notifId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            context.SystemNotifications.Add(new SystemNotification { NotificationId = notifId, Status = "DRAFT", Target = "ALL", Title = "Hello", Content = "C1" });
            context.Branches.Add(new Branch { BranchId = branchId, IsActive = 1 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid());

            var result = await controller.SendNotification(notifId);

            Assert.IsType<OkObjectResult>(result);
            var notif = await context.SystemNotifications.FindAsync(notifId);
            Assert.Equal("SENT", notif!.Status);
            
            _mockSender.Verify(s => s.SendAsync("ALL", "Hello", It.IsAny<string>(), false), Times.Once);
        }

        [Fact]
        public async Task CancelNotification_Draft_CancelsSuccessfully()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var notifId = Guid.NewGuid();
            context.SystemNotifications.Add(new SystemNotification { NotificationId = notifId, Status = "SCHEDULED", Title = "T", Target = "ALL", Content = "C" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid());
            var result = await controller.CancelNotification(notifId);

            Assert.IsType<OkObjectResult>(result);
            var notif = await context.SystemNotifications.FindAsync(notifId);
            Assert.Equal("CANCELLED", notif!.Status);
        }

        [Fact]
        public async Task DeleteNotification_Draft_DeletesSuccessfully()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var notifId = Guid.NewGuid();
            context.SystemNotifications.Add(new SystemNotification { NotificationId = notifId, Status = "DRAFT", Title = "T", Target = "ALL", Content = "C" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid());
            var result = await controller.DeleteNotification(notifId);

            Assert.IsType<OkObjectResult>(result);
            var notif = await context.SystemNotifications.FindAsync(notifId);
            Assert.Null(notif);
        }

        [Fact]
        public async Task DeleteNotification_Sent_ReturnsBadRequest()
        {
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var notifId = Guid.NewGuid();
            context.SystemNotifications.Add(new SystemNotification { NotificationId = notifId, Status = "SENT", Title = "T", Target = "ALL", Content = "C" });
            await context.SaveChangesAsync();

            var controller = CreateController(context, Guid.NewGuid());
            var result = await controller.DeleteNotification(notifId);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Chỉ xóa được DRAFT/CANCELLED", badRequest.Value?.ToString() ?? "");
        }
    }
}
