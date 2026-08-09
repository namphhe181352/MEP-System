using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ClosedXML.Excel;
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
    public class OutwardInventoryExcelControllerTests
    {
        private readonly DbContextOptions<MEPDatabaseContext> _dbContextOptions;
        private readonly Mock<ICurrentBranchAccessor> _mockBranchAccessor;

        public OutwardInventoryExcelControllerTests()
        {
            _dbContextOptions = new DbContextOptionsBuilder<MEPDatabaseContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            _mockBranchAccessor = new Mock<ICurrentBranchAccessor>();
        }

        private OutwardInventoryExcelController CreateController(MEPDatabaseContext context, Guid? userId = null, Guid? branchId = null, string role = "Staff")
        {
            var controller = new OutwardInventoryExcelController(context, _mockBranchAccessor.Object);

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
            Assert.Equal("Template_XuatKho_Excel.xlsx", fileResult.FileDownloadName);
            Assert.NotEmpty(fileResult.FileContents);
        }

        [Fact]
        public async Task ParseAndValidateExcel_ValidData_ReturnsPreview()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();

            var productId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = productId, ProductCode = "ONG-21", ProductName = "Ống nhựa Bình Minh phi 21" });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, ActualStock = 100 });
            await context.SaveChangesAsync();
            
            var controller = CreateController(context, userId, branchId);

            // Create a valid Excel stream
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("XuatKho");
            ws.Cell(1, 1).Value = "MaSanPham";
            ws.Cell(1, 2).Value = "TenSanPham";
            ws.Cell(1, 3).Value = "SoLuong";
            ws.Cell(1, 4).Value = "LyDo";
            ws.Cell(2, 1).Value = "ONG-21";
            ws.Cell(2, 2).Value = "Ống nhựa Bình Minh phi 21";
            ws.Cell(2, 3).Value = "10";
            ws.Cell(2, 4).Value = "Xuất hủy";

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(100);
            mockFile.Setup(f => f.FileName).Returns("test.xlsx");
            mockFile.Setup(f => f.OpenReadStream()).Returns(stream);

            var request = new OutwardInventoryExcelUploadDto { File = mockFile.Object, BranchId = branchId };

            // Act
            var result = await controller.ParseAndValidateExcel(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<OutwardExcelPreviewDto>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.False(response.Data.HasErrors);
            Assert.Single(response.Data.ValidRows);
            Assert.Equal("WRITE_OFF", response.Data.ValidRows[0].TicketType);
        }

        [Fact]
        public async Task CommitImport_ValidData_CreatesStockTickets()
        {
            // Arrange
            using var context = new MEPDatabaseContext(_dbContextOptions);
            var userId = Guid.NewGuid();
            var branchId = Guid.NewGuid();

            var productId = Guid.NewGuid();
            context.Products.Add(new Product { ProductId = productId, ProductCode = "ONG-21" });
            context.BranchProducts.Add(new BranchProduct { BranchProductId = Guid.NewGuid(), BranchId = branchId, ProductId = productId, ActualStock = 100 });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId, branchId);

            var request = new OutwardExcelCommitDto
            {
                BranchId = branchId,
                Rows = new List<OutwardExcelRowDto>
                {
                    new OutwardExcelRowDto
                    {
                        ProductId = productId,
                        MaSanPham = "ONG-21",
                        SoLuong = 10,
                        TicketType = "WRITE_OFF",
                        LyDo = "Xuất hủy do vỡ"
                    }
                }
            };

            // Act
            var result = await controller.CommitImport(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApiResponse<OutwardExcelResultDto>>(okResult.Value);
            
            Assert.True(response.Success);
            Assert.Single(response.Data.CreatedTickets);
            
            var ticket = await context.StockTickets.FirstOrDefaultAsync(t => t.BranchId == branchId && t.TicketType == "WRITE_OFF");
            Assert.NotNull(ticket);
            Assert.Equal("PENDING", ticket.Status);
            
            var item = await context.StockTicketItems.FirstOrDefaultAsync(i => i.StockTicketId == ticket.StockTicketId);
            Assert.NotNull(item);
            Assert.Equal(-10, item.Discrepancy);
        }
    }
}
