using System;
using System.IO;
using System.Threading.Tasks;
using ClosedXML.Excel;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Services.InventoryServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.InventoryController;

/// <summary>
/// Xuất kho Excel - 1 template chung, dùng cột LyDo để phân loại.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Xuat kho Excel (Excel Import)")]
[Authorize]
public class OutwardInventoryExcelController : ControllerBase
{
    private readonly IOutwardInventoryExcelService _outwardExcelService;

    public OutwardInventoryExcelController(IOutwardInventoryExcelService outwardExcelService)
    {
        _outwardExcelService = outwardExcelService;
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("userId")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
    }

    /// <summary>
    /// GET /api/outwardinventoryexcel/template
    /// Tai file template mau cho Excel xuat kho.
    /// </summary>
    [HttpGet("template")]
    [HasPermission(PermissionConstants.STOCK_OUTWARD_CREATE)]
    public async Task<IActionResult> DownloadTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("XuatKho");

        string[] headers = { "MaSanPham", "TenSanPham", "SoLuong", "LyDo", "GhiChu" };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var sampleData = new object[]
        {
            new object[] { "SP-001", "San pham mau 1", 2, "Xuat huy", "Hang hong" },
            new object[] { "SP-002", "San pham mau 2", 1, "Xuat huy", "Het han su dung" },
            new object[] { "SP-003", "San pham mau 3", 5, "Dieu chuyen", "Chuyen chi nhanh 2" }
        };

        for (int row = 0; row < sampleData.Length; row++)
        {
            var data = (object[])sampleData[row];
            for (int col = 0; col < data.Length; col++)
            {
                worksheet.Cell(row + 2, col + 1).Value = data[col]?.ToString() ?? "";
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Template_XuatKho_Excel.xlsx");
    }

    /// <summary>
    /// POST /api/outwardinventoryexcel/parse
    /// Upload + validate file Excel xuat kho.
    /// </summary>
    [HttpPost("parse")]
    [Consumes("multipart/form-data")]
    [HasPermission(PermissionConstants.STOCK_OUTWARD_CREATE)]
    public async Task<IActionResult> ParseAndValidateExcel(
        [FromForm] OutwardInventoryExcelUploadDto uploadDto)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Khong xac dinh duoc nguoi dung." });

        var result = await _outwardExcelService.ParseAndValidateExcelAsync(
            uploadDto.File, currentUserId.Value, currentUserId.Value, uploadDto.TargetBranchId);

        return Ok(result);
    }

    /// <summary>
    /// POST /api/outwardinventoryexcel/commit
    /// Tao nhieu StockTicket (groupBy LyDo) voi Status = PENDING.
    /// </summary>
    [HttpPost("commit")]
    [HasPermission(PermissionConstants.STOCK_OUTWARD_CREATE)]
    public async Task<IActionResult> CommitImport([FromBody] OutwardExcelCommitDto dto)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == null)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Khong xac dinh duoc nguoi dung." });

        var result = await _outwardExcelService.CommitImportAsync(dto, currentUserId.Value, currentUserId.Value);
        return Ok(result);
    }
}
