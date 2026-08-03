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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class SystemNotificationsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public SystemNotificationsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private SystemNotificationsController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var mockMapper = new Mock<IMapper>();
            mockMapper.Setup(m => m.Map<List<SystemNotificationDto>>(It.IsAny<List<SystemNotification>>()))
                .Returns((List<SystemNotification> source) => source.Select(n => new SystemNotificationDto { NotificationId = n.NotificationId, Title = n.Title }).ToList());

            var mockLogger = new Mock<ILogger<LogNotificationSender>>();
            var sender = new LogNotificationSender(mockLogger.Object);

            var controller = new SystemNotificationsController(context, mockMapper.Object, sender);

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
        public async Task GetNotifications_ReturnsPaginatedList()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, FullName = "Admin", Email = "admin@test.com", Username = "admin", PasswordHash = "hash" });
            
            context.SystemNotifications.Add(new SystemNotification 
            { 
                NotificationId = Guid.NewGuid(), 
                Title = "Test Notif", 
                Content = "Content",
                Target = "ALL",
                Status = "DRAFT",
                CreatedBy = userId
            });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetNotifications(null, 1, 10);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<PageResultDto<SystemNotificationDto>>(okResult.Value);
            Assert.Single(dto.Items);
            Assert.Equal("Test Notif", dto.Items.First().Title);
        }

        [Fact]
        public async Task CreateNotification_ValidData_ReturnsCreated()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var adminId = Guid.NewGuid();
            var controller = CreateController(context, adminId);
            
            context.Users.Add(new User { UserId = adminId, FullName = "Admin", Email = "admin@test.com", Username = "admin", PasswordHash = "hash" });
            await context.SaveChangesAsync();

            var request = new CreateNotificationRequest
            {
                Title = "New Notice",
                Content = "Notice Content",
                Target = "STAFF",
                IsUrgent = false
            };

            // Act
            var result = await controller.CreateNotification(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            
            var dbNotification = await context.SystemNotifications.FirstOrDefaultAsync();
            Assert.NotNull(dbNotification);
            Assert.Equal("New Notice", dbNotification.Title);
            Assert.Equal("DRAFT", dbNotification.Status);
        }

        [Fact]
        public async Task SendNotification_ValidId_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var adminId = Guid.NewGuid();
            var controller = CreateController(context, adminId);

            var notifId = Guid.NewGuid();
            context.SystemNotifications.Add(new SystemNotification 
            { 
                NotificationId = notifId, 
                Title = "Test Notif", 
                Content = "Content",
                Target = "ALL",
                Status = "DRAFT"
            });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.SendNotification(notifId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);

            var dbNotif = await context.SystemNotifications.FindAsync(notifId);
            Assert.Equal("SENT", dbNotif!.Status);
            Assert.NotNull(dbNotif.SentAt);
        }

        [Fact]
        public async Task DeleteNotification_ValidId_ReturnsOk()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var notifId = Guid.NewGuid();
            context.SystemNotifications.Add(new SystemNotification 
            { 
                NotificationId = notifId, 
                Title = "Test Notif", 
                Content = "Content",
                Target = "ALL",
                Status = "DRAFT"
            });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.DeleteNotification(notifId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);

            var dbNotif = await context.SystemNotifications.FindAsync(notifId);
            Assert.Null(dbNotif);
        }
    }
}
