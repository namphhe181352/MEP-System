using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Controllers.AdminController;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
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

        private SystemLogsController CreateController(MEPDatabaseContext context)
        {
            var mockMapper = new Mock<IMapper>();
            
            // Setup mock mapper to return a simple mapped object
            mockMapper.Setup(m => m.Map<List<SystemLogDto>>(It.IsAny<List<SystemLog>>()))
                .Returns((List<SystemLog> source) => source.Select(l => new SystemLogDto { LogId = l.LogId, Action = l.Action }).ToList());
                
            mockMapper.Setup(m => m.Map<SystemLogDto>(It.IsAny<SystemLog>()))
                .Returns((SystemLog source) => new SystemLogDto { LogId = source.LogId, Action = source.Action });

            return new SystemLogsController(context, mockMapper.Object);
        }

        [Fact]
        public async Task GetLogs_ReturnsPaginatedLogs()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, FullName = "Admin", Email = "admin@test.com", Username = "admin", PasswordHash = "hash" });
            
            context.SystemLogs.Add(new SystemLog 
            { 
                LogId = Guid.NewGuid(), 
                UserId = userId,
                Action = "LOGIN",
                TableName = "users",
                Timestamp = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetLogs("LOGIN", userId, null, null, null, null, 1, 10);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<PageResultDto<SystemLogDto>>(okResult.Value);
            Assert.Single(dto.Items);
            Assert.Equal("LOGIN", dto.Items.First().Action);
        }

        [Fact]
        public async Task ExportLogs_CsvFormat_ReturnsCsvFile()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, FullName = "Admin", Email = "admin@test.com", Username = "admin", PasswordHash = "hash" });
            
            context.SystemLogs.Add(new SystemLog 
            { 
                LogId = Guid.NewGuid(), 
                UserId = userId,
                Action = "LOGIN",
                TableName = "users",
                Timestamp = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.ExportLogs("csv");

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/csv", fileResult.ContentType);
            Assert.EndsWith(".csv", fileResult.FileDownloadName);
            var content = Encoding.UTF8.GetString(fileResult.FileContents);
            Assert.Contains("LOGIN", content);
        }

        [Fact]
        public async Task ExportLogs_TxtFormat_ReturnsTxtFile()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, FullName = "Admin", Email = "admin@test.com", Username = "admin", PasswordHash = "hash" });
            
            context.SystemLogs.Add(new SystemLog 
            { 
                LogId = Guid.NewGuid(), 
                UserId = userId,
                Action = "LOGIN",
                TableName = "users",
                Timestamp = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.ExportLogs("txt");

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/plain", fileResult.ContentType);
            Assert.EndsWith(".txt", fileResult.FileDownloadName);
            var content = Encoding.UTF8.GetString(fileResult.FileContents);
            Assert.Contains("LOGIN", content);
        }

        [Fact]
        public async Task GetLog_ValidId_ReturnsLog()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            var userId = Guid.NewGuid();
            context.Users.Add(new User { UserId = userId, FullName = "Admin", Email = "admin@test.com", Username = "admin", PasswordHash = "hash" });
            
            var logId = Guid.NewGuid();
            context.SystemLogs.Add(new SystemLog 
            { 
                LogId = logId, 
                UserId = userId,
                Action = "LOGIN",
                TableName = "users",
                Timestamp = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            // Act
            var result = await controller.GetLog(logId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var dto = Assert.IsType<SystemLogDto>(okResult.Value);
            Assert.Equal(logId, dto.LogId);
            Assert.Equal("LOGIN", dto.Action);
        }
    }
}
