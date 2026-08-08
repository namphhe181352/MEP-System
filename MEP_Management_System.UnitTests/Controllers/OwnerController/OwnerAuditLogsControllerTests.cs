using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Controllers.OwnerController;
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

namespace MEP_Management_System.UnitTests.Controllers.OwnerController
{
    public class OwnerAuditLogsControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentBranchAccessor> _mockBranchAccessor;

        public OwnerAuditLogsControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockMapper = new Mock<IMapper>();
            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();

            _mockMapper.Setup(m => m.Map<List<SystemLogDto>>(It.IsAny<List<SystemLog>>()))
                .Returns((List<SystemLog> logs) => logs.Select(l => new SystemLogDto { LogId = l.LogId }).ToList());
            
            _mockMapper.Setup(m => m.Map<SystemLogDto>(It.IsAny<SystemLog>()))
                .Returns((SystemLog l) => new SystemLogDto { LogId = l.LogId });
        }

        private OwnerAuditLogsController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null)
        {
            var controller = new OwnerAuditLogsController(context, _mockMapper.Object, _mockBranchAccessor.Object);

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

            if (branchId.HasValue)
            {
                _mockBranchAccessor.Setup(x => x.GetBranchId()).Returns(branchId.Value);
            }
            else
            {
                _mockBranchAccessor.Setup(x => x.GetBranchId()).Returns((Guid?)null);
            }

            return controller;
        }

        [Theory]
        [InlineData("LOGIN", null, null, null, null, null, null, 1, 10, 1)] // Filter by action
        [InlineData(null, "validUserId", null, null, null, null, null, 1, 10, 3)] // Filter by userId (3 logs for this user)
        [InlineData(null, null, "INFO", null, null, null, null, 1, 10, 2)] // Filter by level INFO
        [InlineData(null, null, "WARN", null, null, null, null, 1, 10, 1)] // Filter by level WARN
        [InlineData(null, null, null, "User", null, null, null, 1, 10, 2)] // Filter by entityType
        [InlineData(null, null, null, null, "2024-01-01", "2024-12-31", null, 1, 10, 3)] // Filter by DateRange valid
        [InlineData(null, null, null, null, "2025-01-01", "2025-12-31", null, 1, 10, 0)] // Filter by DateRange no match
        [InlineData(null, null, null, null, null, null, "admin", 1, 10, 3)] // Filter by userName
        [InlineData("CREATE", "validUserId", "WARN", "Product", null, null, "admin", 1, 10, 1)] // Combined filters
        public async Task GetLogs_ReturnsFilteredPagedList_ExtendedMatrix(string? action, string? userIdFlag, string? level, string? entityType, string? fromDate, string? toDate, string? userName, int page, int pageSize, int expectedCount)
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var logUserId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            context.Users.Add(new User { UserId = logUserId, FullName = "admin" });
            context.Users.Add(new User { UserId = otherUserId, FullName = "other" });
            
            context.StaffAssignments.Add(new StaffAssignment { UserId = logUserId, BranchId = branchId });
            // otherUser is not in the branch

            // Seed Logs
            context.SystemLogs.Add(new SystemLog { LogId = Guid.NewGuid(), UserId = logUserId, Action = "LOGIN", Level = "INFO", Timestamp = new DateTime(2024, 5, 1), EntityType = "User", TableName = "User" });
            context.SystemLogs.Add(new SystemLog { LogId = Guid.NewGuid(), UserId = logUserId, Action = "LOGOUT", Level = "INFO", Timestamp = new DateTime(2024, 5, 2), EntityType = "User", TableName = "User" });
            context.SystemLogs.Add(new SystemLog { LogId = Guid.NewGuid(), UserId = logUserId, Action = "CREATE", Level = "WARN", Timestamp = new DateTime(2024, 6, 1), EntityType = "Product", TableName = "Product" });
            // This log belongs to a user not in the branch, should never be returned
            context.SystemLogs.Add(new SystemLog { LogId = Guid.NewGuid(), UserId = otherUserId, Action = "LOGIN", Level = "INFO", Timestamp = new DateTime(2024, 5, 1), EntityType = "User", TableName = "User" });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);
            Guid? targetUserId = userIdFlag == "validUserId" ? logUserId : null;
            DateTime? from = fromDate != null ? DateTime.Parse(fromDate) : null;
            DateTime? to = toDate != null ? DateTime.Parse(toDate) : null;

            // Act
            var result = await controller.GetLogs(action, targetUserId, level, entityType, from, to, userName, page, pageSize);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<PageResultDto<SystemLogDto>>(okResult.Value);
            Assert.Equal(expectedCount, response.TotalCount);
        }

        [Fact]
        public async Task GetLog_ValidId_ReturnsLog()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var logId = Guid.NewGuid();
            var logUserId = Guid.NewGuid();

            context.Users.Add(new User { UserId = logUserId });
            context.StaffAssignments.Add(new StaffAssignment { UserId = logUserId, BranchId = branchId });
            context.SystemLogs.Add(new SystemLog { LogId = logId, UserId = logUserId, Action = "LOGIN", Level = "INFO", Timestamp = DateTime.UtcNow, TableName = "User" });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.GetLog(logId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<SystemLogDto>>(okResult.Value);
            Assert.True(response.Success);
            Assert.Equal(logId, response.Data!.LogId);
        }
        
        [Fact]
        public async Task GetLog_InvalidLogOrNotBranch_ReturnsNotFound()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var logId = Guid.NewGuid();
            var otherUserId = Guid.NewGuid();

            context.Users.Add(new User { UserId = otherUserId });
            // otherUser is NOT in branch
            context.SystemLogs.Add(new SystemLog { LogId = logId, UserId = otherUserId, Action = "LOGIN", Level = "INFO", Timestamp = DateTime.UtcNow, TableName = "User" });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.GetLog(logId);

            // Assert
            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(notFound.Value);
            Assert.False(response.Success);
            Assert.Contains("không tìm thấy", response.Message.ToLower());
        }

        [Fact]
        public async Task GetAuditLogUsers_ReturnsUsersInBranch()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();

            var u1 = new User { UserId = Guid.NewGuid(), Status = "ACTIVE" };
            var u2 = new User { UserId = Guid.NewGuid(), Status = "DELETED" };
            
            context.Users.Add(u1);
            context.Users.Add(u2);
            context.StaffAssignments.Add(new StaffAssignment { UserId = u1.UserId, BranchId = branchId });
            context.StaffAssignments.Add(new StaffAssignment { UserId = u2.UserId, BranchId = branchId });
            
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            // Act
            var result = await controller.GetAuditLogUsers();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
            
            Assert.True(response.Success);
            // It should exclude DELETED users
            var users = response.Data as IEnumerable<object>;
            Assert.NotNull(users);
            Assert.Single(users);
        }
    }
}
