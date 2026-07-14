using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ClosedXML.Excel;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Nhập kho Excel (Excel Import)")]
    [Authorize]
    public class InwardInventoryExcelController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly ICurrentBranchAccessor _currentBranch;
        private readonly IInwardInventoryExcelService _excelService;

        public InwardInventoryExcelController(
            MEPDatabaseContext context,
            ICurrentBranchAccessor currentBranch,
            IInwardInventoryExcelService excelService)
        {
            _context = context;
            _currentBranch = currentBranch;
            _excelService = excelService;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
        }

        private bool HasGlobalAccess()
        {
            return User.IsInRole("Owner");
        }

        private bool IsOwner() => User.IsInRole("Owner");

        /// <summary>
        /// GET /api/inwardinventoryexcel/template
        /// Tải file template mẫu để user download.
        /// </summary>
        [HttpGet("template")]
        [HasPermission(PermissionConstants.STOCK_VIEW)]
        public async Task<IActionResult> DownloadTemplate()
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("NhapKho");

                // Header theo đúng thứ tự yêu cầu
                string[] headers = { "MaSanPham", "TenSanPham", "SoLuong", "DonGiaNhap", "GhiChu" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                // Dữ liệu mẫu (2 dòng)
                var sampleData = new object[]
                {
                    new object[] { "SP-001", "Sản phẩm mẫu 1", 10, 100000, "Nhập kho mẫu" },
                    new object[] { "SP-002", "Sản phẩm mẫu 2", 5, 50000, "" }
                };

                for (int row = 0; row < sampleData.Length; row++)
                {
                    var data = (object[])sampleData[row];
                    for (int col = 0; col < data.Length; col++)
                    {
                        worksheet.Cell(row + 2, col + 1).Value = data[col]?.ToString() ?? "";
                    }
                }

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Template_NhapKho_Excel.xlsx");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi tạo template",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// POST /api/inwardinventoryexcel/parse
        /// Bước 1: Upload và validate file Excel (chưa ghi DB).
        /// </summary>
        [HttpPost("parse")]
        [Consumes("multipart/form-data")]
        [HasPermission(PermissionConstants.STOCK_INWARD_CREATE)]
        public async Task<IActionResult> ParseAndValidateExcel(
            [FromForm] InwardInventoryExcelUploadDto uploadDto)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không xác định được định danh người dùng từ Token.",
                    Errors = new() { "User identity missing." }
                });
            }

            var file = uploadDto.File;

            // Validate file
            if (uploadDto.File == null || uploadDto.File.Length == 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Vui lòng chọn file Excel.",
                    Errors = new() { "File is required." }
                });
            }

            var extension = Path.GetExtension(uploadDto.File.FileName)?.ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xls")
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Chỉ chấp nhận file Excel (.xlsx, .xls).",
                    Errors = new() { "Invalid file format." }
                });
            }

            // Resolve branchId theo pattern Owner/Staff
            var branchId = await ResolveBranchIdAsync(uploadDto.BranchId, currentUserId.Value);
            uploadDto.BranchId = branchId;

            try
            {
                using var stream = uploadDto.File.OpenReadStream();
                var result = await _excelService.ParseAndValidateExcelAsync(
                    stream,
                    uploadDto.File.FileName,
                    uploadDto,
                    currentUserId.Value);

                return Ok(new ApiResponse<InwardInventoryExcelPreviewDto>
                {
                    Success = true,
                    Message = result.HasErrors
                        ? "Có lỗi trong file Excel. Vui lòng kiểm tra."
                        : "Validate thành công. Sẵn sàng để import.",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi xử lý file Excel",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// POST /api/inwardinventoryexcel/commit
        /// Bước 2: Commit dữ liệu sau khi user xác nhận preview.
        /// </summary>
        [HttpPost("commit")]
        [HasPermission(PermissionConstants.STOCK_INWARD_CREATE)]
        public async Task<IActionResult> CommitImport([FromBody] InwardInventoryExcelCommitDto dto)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không xác định được định danh người dùng từ Token.",
                    Errors = new() { "User identity missing." }
                });
            }

            if (string.IsNullOrWhiteSpace(dto.PreviewToken))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "PreviewToken là bắt buộc.",
                    Errors = new() { "PreviewToken is required." }
                });
            }

            try
            {
                var result = await _excelService.CommitImportAsync(
                    dto.PreviewToken,
                    currentUserId.Value);

                return Ok(new ApiResponse<InwardInventoryExcelResultDto>
                {
                    Success = true,
                    Message = $"Import thành công! Đã nhập {result.SuccessRowCount} sản phẩm.",
                    Data = result
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi xác thực",
                    Errors = new() { ex.Message }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không có quyền",
                    Errors = new() { ex.Message }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi import dữ liệu",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// Hàm phụ trợ resolve branchId theo pattern Owner/Staff.
        /// </summary>
        private async Task<Guid> ResolveBranchIdAsync(Guid? requestedBranchId, Guid userId)
        {
            // Ưu tiên 1: requestedBranchId từ request (chỉ Owner mới truyền được)
            if (requestedBranchId.HasValue && requestedBranchId != Guid.Empty)
            {
                if (!IsOwner())
                {
                    // Staff không được phép truyền branchId khác
                    var staffBranchId = _currentBranch.GetBranchId();
                    if (staffBranchId.HasValue)
                    {
                        return staffBranchId.Value;
                    }
                }
                return requestedBranchId.Value;
            }

            // Ưu tiên 2: DefaultBranchId từ users table
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user?.DefaultBranchId is Guid defaultBranchId && defaultBranchId != Guid.Empty)
            {

                return defaultBranchId;

            }

            // Ưu tiên 3: BranchId từ JWT token
            var jwtBranchId = _currentBranch.GetBranchId();
            if (jwtBranchId.HasValue)
            {
                return jwtBranchId.Value;
            }

            // Ưu tiên 4: Lấy chi nhánh active duy nhất
            var activeBranch = await _context.Branches
                .AsNoTracking()
                .Where(b => b.IsActive == 1)
                .FirstOrDefaultAsync();

            if (activeBranch != null)
            {
                return activeBranch.BranchId;
            }

            throw new InvalidOperationException("Không tìm thấy chi nhánh hợp lệ.");
        }
    }
}
