using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.AdminController;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace MEP_Management_System.Tests.Controllers
{
    public class SystemLogsControllerTests
    {
        private DbContextOptions<MEPDatabaseContext> _dbContextOptions;

        public SystemLogsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
        }

        private SystemLogsController CreateController(MEPDatabaseContext context, Guid? userId = null)
        {
            var controller = new SystemLogsController(context);

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
        // TEST GET /api/admin/system-logs
        // ==========================================
        [Fact]
        public async Task GetLogs_FiltersByLevel_ReturnsFilteredLogs()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context, Guid.NewGuid());

            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, FullName = "Hệ thống Admin", Email = "admin@mep.vn", Username = "admin", PasswordHash = "hash" });
            
            context.SystemLogs.Add(new SystemLog 
            { 
                LogId = Guid.NewGuid(), Action = "CREATE_USER", Level = "INFO", 
                Description = "Tạo user abc", Timestamp = DateTime.UtcNow, UserId = userId 
            });
            context.SystemLogs.Add(new SystemLog 
            { 
                LogId = Guid.NewGuid(), Action = "DELETE_USER", Level = "WARN", 
                Description = "Xóa user nguy hiểm", Timestamp = DateTime.UtcNow, UserId = userId 
            });
            context.SystemLogs.Add(new SystemLog 
            { 
                LogId = Guid.NewGuid(), Action = "DB_ERROR", Level = "ERROR", 
                Description = "Lỗi kết nối DB", Timestamp = DateTime.UtcNow, UserId = userId 
            });
            await context.SaveChangesAsync();

            // Act - Fetch only WARNING level logs
            var result = await controller.GetLogs(search: null, level: "WARN", page: 1, pageSize: 10);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<PageResultDto<SystemLogDto>>(okResult.Value);
            
            Assert.Single(dto.Items); // Expect 1
            Assert.Equal("WARN", dto.Items.First().Level);
            Assert.Contains("Xóa user", dto.Items.First().Description);
        }
    }
}
