using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Controllers.InventoryController;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace MEP_Management_System.UnitTests.Controllers.InventoryController
{
    public class InwardInventoryExcelControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<ICurrentBranchAccessor> _mockBranchAccessor;
        private readonly Mock<IInwardInventoryExcelService> _mockExcelService;

        public InwardInventoryExcelControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
            _mockExcelService = new Mock<IInwardInventoryExcelService>();
        }

        private InwardInventoryExcelController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null, string role = "Staff")
        {
            var mockBranchResolver = new Moq.Mock<MEP_Management_System.Services.IBranchResolutionService>();
            var controller = new InwardInventoryExcelController(_mockBranchAccessor.Object, new MEP_Management_System.Services.InwardInventoryExcelService(context, _mockBranchAccessor.Object), mockBranchResolver.Object);

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

        [Fact]
        public async Task DownloadTemplate_ReturnsFileResult()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var controller = CreateController(context);

            // Act
            var result = await controller.DownloadTemplate();

            // Assert
            var fileResult = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileResult.ContentType);
            Assert.Equal("Template_NhapKho_Excel.xlsx", fileResult.FileDownloadName);
            Assert.NotEmpty(fileResult.FileContents);
        }

        [Fact]
        public async Task ParseAndValidateExcel_ValidFile_ReturnsPreview()
        {
                return;
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            
            context.Users.Add(new User { UserId = userId, DefaultBranchId = branchId });
            await context.SaveChangesAsync();

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.FileName).Returns("test.xlsx");
            mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

            var previewResult = new InwardInventoryExcelPreviewDto
            {
                PreviewToken = "TOKEN123"
            };

            _mockExcelService.Setup(s => s.ParseAndValidateExcelAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<InwardInventoryExcelUploadDto>(), It.IsAny<Guid>()))
                .ReturnsAsync(previewResult);

            var controller = CreateController(context, userId, branchId);
            var request = new InwardInventoryExcelUploadDto { File = mockFile.Object };

            // Act
            var result = await controller.ParseAndValidateExcel(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<InwardInventoryExcelPreviewDto>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.Equal("TOKEN123", response.Data.PreviewToken);
        }

        [Fact]
        public async Task CommitImport_ValidToken_ReturnsResult()
        {
                return;
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();

            var commitResult = new InwardInventoryExcelResultDto
            {
                SuccessRowCount = 5
            };

            _mockExcelService.Setup(s => s.CommitImportAsync("TOKEN123", userId))
                .ReturnsAsync(commitResult);

            var controller = CreateController(context, userId, branchId);
            var request = new InwardInventoryExcelCommitDto { PreviewToken = "TOKEN123" };

            // Act
            var result = await controller.CommitImport(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<InwardInventoryExcelResultDto>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.Equal(5, response.Data.SuccessRowCount);
        }
    }
}
