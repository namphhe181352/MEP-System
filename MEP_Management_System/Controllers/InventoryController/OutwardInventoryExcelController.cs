using System;
using System.Collections.Generic;
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
    /// <summary>
    /// Xuất kho Excel - 1 template chung, dùng cột LyDo để phân loại:
    /// - RETURN_SUPPLIER (Trả NCC)
    /// - WRITE_OFF (Xuất hủy)
    /// - TRANSFER (Điều chuyển)
    /// Commit sẽ tạo NHIỀU phiếu (theo LyDo) với Status = PENDING.
    /// User confirm từng phiếu như manual flow.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Xuất kho Excel (Excel Import)")]
    [Authorize]
    public class OutwardInventoryExcelController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly ICurrentBranchAccessor _currentBranch;

        public OutwardInventoryExcelController(
            MEPDatabaseContext context,
            ICurrentBranchAccessor currentBranch)
        {
            _context = context;
            _currentBranch = currentBranch;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
        }

        private bool IsOwner() => User.IsInRole("Owner");

        private static string GetTicketTypeFromReason(string reason)
        {
            var r = (reason ?? "").Trim().ToLower();
            if (r == "xuất hủy" || r == "xuat huy" || r == "write_off") return "WRITE_OFF";
            if (r == "điều chuyển" || r == "dieu chuyen" || r == "transfer") return "TRANSFER";
            return null;
        }

        /// <summary>
        /// GET /api/outwardinventoryexcel/template
        /// Tải file template mẫu cho Excel xuất kho.
        /// </summary>
        [HttpGet("template")]
        [HasPermission(PermissionConstants.STOCK_VIEW)]
        public async Task<IActionResult> DownloadTemplate()
        {
            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("XuatKho");

                // Header: MaSanPham | TenSanPham | SoLuong | LyDo | GhiChu
                string[] headers = { "MaSanPham", "TenSanPham", "SoLuong", "LyDo", "GhiChu" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                // Sample data
                var sampleData = new object[]
                {
                    new object[] { "SP-001", "Sản phẩm mẫu 1", 2, "Xuất hủy", "Hàng hỏng" },
                    new object[] { "SP-002", "Sản phẩm mẫu 2", 1, "Xuất hủy", "Hết hạn sử dụng" },
                    new object[] { "SP-003", "Sản phẩm mẫu 3", 5, "Điều chuyển", "Chuyển chi nhánh 2" }
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
        /// POST /api/outwardinventoryexcel/parse
        /// Upload + validate file Excel xuất kho. Trả preview kèm groupBy LyDo.
        /// </summary>
        [HttpPost("parse")]
        [Consumes("multipart/form-data")]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_CREATE)]
        public async Task<IActionResult> ParseAndValidateExcel(
            [FromForm] OutwardInventoryExcelUploadDto uploadDto)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không xác định được người dùng." });

            if (uploadDto.File == null || uploadDto.File.Length == 0)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Vui lòng chọn file Excel." });

            var extension = Path.GetExtension(uploadDto.File.FileName)?.ToLowerInvariant();
            if (extension != ".xlsx" && extension != ".xls")
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Chỉ chấp nhận file Excel (.xlsx, .xls)." });

            var branchId = await ResolveBranchIdAsync(uploadDto.BranchId, currentUserId.Value, uploadDto.TargetBranchId);
            uploadDto.BranchId = branchId;

            try
            {
                using var stream = uploadDto.File.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.First();

                var validRows = new List<OutwardExcelRowDto>();
                var errors = new List<OutwardExcelErrorDto>();

                // Header row (row 1)
                var headers = new Dictionary<string, int>();
                for (int c = 1; c <= (worksheet.LastColumnUsed()?.ColumnNumber() ?? 0); c++)
                {
                    var h = worksheet.Cell(1, c).GetString().Trim();
                    if (!string.IsNullOrEmpty(h)) headers[h] = c;
                }

                // Validate required headers
                var requiredHeaders = new[] { "MaSanPham", "SoLuong", "LyDo" };
                foreach (var req in requiredHeaders)
                {
                    if (!headers.ContainsKey(req))
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = $"File Excel thiếu cột bắt buộc: {req}"
                        });
                    }
                }

                // Parse rows
                for (int row = 2; row <= (worksheet.LastRowUsed()?.RowNumber() ?? 0); row++)
                {
                    var maSanPham = worksheet.Cell(row, headers["MaSanPham"]).GetString().Trim();
                    var tenSanPhamExcel = headers.ContainsKey("TenSanPham") ? worksheet.Cell(row, headers["TenSanPham"]).GetString().Trim() : "";
                    var soLuongStr = worksheet.Cell(row, headers["SoLuong"]).GetString().Trim();
                    var lyDo = worksheet.Cell(row, headers["LyDo"]).GetString().Trim();
                    var ghiChu = headers.ContainsKey("GhiChu") ? worksheet.Cell(row, headers["GhiChu"]).GetString().Trim() : "";

                    if (string.IsNullOrEmpty(maSanPham)) continue; // skip empty rows

                    // Validate
                    if (!int.TryParse(soLuongStr, out int soLuong) || soLuong <= 0)
                    {
                        errors.Add(new OutwardExcelErrorDto { RowNumber = row, MaSanPham = maSanPham, ErrorMessage = "SoLuong phải là số nguyên dương." });
                        continue;
                    }
                    var ticketType = GetTicketTypeFromReason(lyDo);
                    if (ticketType == null)
                    {
                        errors.Add(new OutwardExcelErrorDto { RowNumber = row, MaSanPham = maSanPham, ErrorMessage = $"LyDo '{lyDo}' không hợp lệ. Chỉ chấp nhận: Xuất hủy / Điều chuyển." });
                        continue;
                    }

                    validRows.Add(new OutwardExcelRowDto
                    {
                        RowNumber = row,
                        MaSanPham = maSanPham,
                        TenSanPham = tenSanPhamExcel,
                        SoLuong = soLuong,
                        DonGiaXuat = null,
                        LyDo = lyDo,
                        TicketType = ticketType,
                        GhiChu = ghiChu
                    });
                }

                // Validate MaSanPham exists
                var productCodes = validRows.Select(x => x.MaSanPham).Distinct().ToList();
                var existingProducts = await _context.Products
                    .Where(p => productCodes.Contains(p.ProductCode))
                    .Select(p => new { p.ProductId, p.ProductCode, p.ProductName })
                    .ToListAsync();
                var existingCodes = new HashSet<string>(existingProducts.Select(p => p.ProductCode), StringComparer.OrdinalIgnoreCase);
                var productLookup = existingProducts.ToDictionary(p => p.ProductCode, StringComparer.OrdinalIgnoreCase);

                foreach (var item in validRows.ToList())
                {
                    if (!existingCodes.Contains(item.MaSanPham))
                    {
                        errors.Add(new OutwardExcelErrorDto { RowNumber = item.RowNumber, MaSanPham = item.MaSanPham, ErrorMessage = $"Mã sản phẩm '{item.MaSanPham}' không tồn tại trong hệ thống." });
                        validRows.Remove(item);
                        continue;
                    }

                    var dbProduct = productLookup[item.MaSanPham];
                    item.ProductId = dbProduct.ProductId;

                    // Cross-validate Tên sản phẩm từ Excel với DB
                    var excelName = (item.TenSanPham ?? "").Trim();
                    var dbName = (dbProduct.ProductName ?? "").Trim();
                    if (!string.IsNullOrEmpty(excelName) && !excelName.Equals(dbName, StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(new OutwardExcelErrorDto
                        {
                            RowNumber = item.RowNumber,
                            MaSanPham = item.MaSanPham,
                            ErrorMessage = $"Mã sản phẩm '{item.MaSanPham}' có tên trong file Excel là '{excelName}', không khớp với tên trên hệ thống ('{dbName}'). Vui lòng kiểm tra lại"
                        });
                        validRows.Remove(item);
                        continue;
                    }

                    item.TenSanPham = dbName;
                }

                // Kiểm tra sản phẩm đã tồn tại trong kho của chi nhánh chưa
                if (validRows.Count > 0)
                {
                    var validProductIds = validRows.Select(x => x.ProductId).Where(pid => pid != Guid.Empty).Distinct().ToList();
                    var branchProducts = await _context.BranchProducts
                        .Where(bp => validProductIds.Contains(bp.ProductId) && bp.BranchId == branchId)
                        .Select(bp => bp.ProductId)
                        .ToListAsync();
                    var branchProductIds = new HashSet<Guid>(branchProducts);

                    foreach (var item in validRows.ToList())
                    {
                        if (item.ProductId == null || item.ProductId == Guid.Empty || !branchProductIds.Contains(item.ProductId.Value))
                        {
                            var productName = existingProducts
                                .Where(p => p.ProductId == item.ProductId)
                                .Select(p => p.ProductName)
                                .FirstOrDefault() ?? item.MaSanPham;
                            errors.Add(new OutwardExcelErrorDto
                            {
                                RowNumber = item.RowNumber,
                                MaSanPham = item.MaSanPham,
                                ErrorMessage = $"Sản phẩm '{productName}' (mã: {item.MaSanPham}) chưa có trong kho của chi nhánh. Vui lòng nhập kho trước."
                            });
                            validRows.Remove(item);
                        }
                    }
                }

                // Group by LyDo
                var groups = validRows
                    .GroupBy(x => x.TicketType)
                    .Select(g => new OutwardExcelGroupDto
                    {
                        TicketType = g.Key,
                        LyDo = g.First().LyDo,
                        ItemCount = g.Count(),
                        TotalValue = g.Sum(x => x.SoLuong * (x.DonGiaXuat ?? 0))
                    })
                    .ToList();

                return Ok(new ApiResponse<OutwardExcelPreviewDto>
                {
                    Success = true,
                    Message = errors.Any() ? "Có lỗi trong file Excel." : "Validate thành công.",
                    Data = new OutwardExcelPreviewDto
                    {
                        ValidRows = validRows,
                        Errors = errors,
                        Groups = groups,
                        HasErrors = errors.Any()
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi khi xử lý file Excel", Errors = new() { ex.Message } });
            }
        }

        /// <summary>
        /// POST /api/outwardinventoryexcel/commit
        /// Tạo NHIỀU StockTicket (groupBy LyDo) với Status = PENDING.
        /// </summary>
        [HttpPost("commit")]
        [HasPermission(PermissionConstants.STOCK_OUTWARD_CREATE)]
        public async Task<IActionResult> CommitImport([FromBody] OutwardExcelCommitDto dto)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null)
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không xác định được người dùng." });

            var branchId = await ResolveBranchIdAsync(dto.BranchId, currentUserId.Value, dto.TargetBranchId);

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                // Group by LyDo
                var groups = dto.Rows.GroupBy(x => x.TicketType).ToList();
                var createdTickets = new List<OutwardExcelCreatedTicketDto>();

                foreach (var grp in groups)
                {
                    var first = grp.First();
                    var ticketCode = $"XK{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";

                    var stockTicket = new StockTicket
                    {
                        StockTicketId = Guid.NewGuid(),
                        TicketCode = ticketCode,
                        BranchId = branchId,
                        UserId = currentUserId.Value,
                        TicketType = grp.Key,
                        Status = "PENDING",
                        Notes = $"Import từ Excel - {first.LyDo}",
                        CreatedAt = now,
                        TotalAmount = 0 // sẽ tính sau
                    };

                    _context.StockTickets.Add(stockTicket);
                    await _context.SaveChangesAsync();

                    decimal totalValue = 0;
                    var items = new List<StockTicketItem>();
                    foreach (var row in grp)
                    {
                        var branchProduct = await _context.BranchProducts
                            .FirstOrDefaultAsync(bp => bp.ProductId == row.ProductId && bp.BranchId == branchId);

                        if (branchProduct == null)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new ApiResponse<object>
                            {
                                Success = false,
                                Message = $"Sản phẩm '{row.MaSanPham}' chưa có trong kho chi nhánh. Vui lòng nhập kho trước."
                            });
                        }

                        var item = new StockTicketItem
                        {
                            TicketItemId = Guid.NewGuid(),
                            StockTicketId = stockTicket.StockTicketId,
                            BranchProductId = branchProduct.BranchProductId,
                            SystemQuantity = branchProduct.ActualStock,
                            ActualQuantity = branchProduct.ActualStock - row.SoLuong,
                            Discrepancy = -row.SoLuong,
                            UnitPrice = row.DonGiaXuat
                        };
                        items.Add(item);
                        totalValue += row.SoLuong * (row.DonGiaXuat ?? 0);
                    }

                    stockTicket.TotalAmount = totalValue;
                    _context.StockTicketItems.AddRange(items);
                    await _context.SaveChangesAsync();

                    createdTickets.Add(new OutwardExcelCreatedTicketDto
                    {
                        TicketId = stockTicket.StockTicketId,
                        TicketCode = stockTicket.TicketCode,
                        TicketType = stockTicket.TicketType,
                        ItemCount = grp.Count(),
                        TotalValue = totalValue
                    });
                }

                await transaction.CommitAsync();

                return Ok(new ApiResponse<OutwardExcelResultDto>
                {
                    Success = true,
                    Message = $"Tạo thành công {createdTickets.Count} phiếu xuất kho (PENDING). Xác nhận từng phiếu để trừ tồn kho.",
                    Data = new OutwardExcelResultDto
                    {
                        CreatedTickets = createdTickets,
                        TotalTickets = createdTickets.Count,
                        TotalValue = createdTickets.Sum(x => x.TotalValue)
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi khi tạo phiếu", Errors = new() { ex.Message } });
            }
        }

        private async Task<Guid> ResolveBranchIdAsync(Guid? requestedBranchId, Guid userId, Guid? targetBranchId)
        {
            if (requestedBranchId.HasValue && requestedBranchId != Guid.Empty)
                return requestedBranchId.Value;

            var user = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);
            if (user?.DefaultBranchId is Guid defaultBranchId && defaultBranchId != Guid.Empty)
                return defaultBranchId;

            var jwtBranchId = _currentBranch.GetBranchId();
            if (jwtBranchId.HasValue)
                return jwtBranchId.Value;

            var activeBranch = await _context.Branches.AsNoTracking()
                .Where(b => b.IsActive == 1).FirstOrDefaultAsync();
            if (activeBranch != null)
                return activeBranch.BranchId;

            throw new InvalidOperationException("Không tìm thấy chi nhánh hợp lệ.");
        }
    }
}
