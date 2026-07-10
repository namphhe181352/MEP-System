using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace MEP_Management_System.Services
{
    public interface IInwardInventoryExcelService
    {
        Task<InwardInventoryExcelPreviewDto> ParseAndValidateExcelAsync(
            Stream fileStream,
            string fileName,
            InwardInventoryExcelUploadDto uploadDto,
            Guid userId);

        Task<InwardInventoryExcelResultDto> CommitImportAsync(
            string previewToken,
            Guid userId);
    }

    public class InwardInventoryExcelService : IInwardInventoryExcelService
    {
        private readonly MEPDatabaseContext _context;
        private readonly ICurrentBranchAccessor _currentBranch;

        // Lưu tạm preview trong memory (Dictionary đơn giản)
        // Trong production nên dùng Redis hoặc Database
        private static readonly Dictionary<string, InwardInventoryExcelPreview> _previewCache = new();
        private const int MaxRowsPerFile = 5000;

        // Header Excel theo đúng thứ tự yêu cầu
        private static readonly string[] ExpectedHeaders = new[]
        {
            "MaSanPham", "TenSanPham", "SoLuong", "DonGiaNhap", "GhiChu"
        };

        public InwardInventoryExcelService(
            MEPDatabaseContext context,
            ICurrentBranchAccessor currentBranch)
        {
            _context = context;
            _currentBranch = currentBranch;
        }

        /// <summary>
        /// Bước 1: Parse và validate file Excel (chưa ghi DB).
        /// </summary>
        public async Task<InwardInventoryExcelPreviewDto> ParseAndValidateExcelAsync(
            Stream fileStream,
            string fileName,
            InwardInventoryExcelUploadDto uploadDto,
            Guid userId)
        {
            var preview = new InwardInventoryExcelPreviewDto
            {
                FileName = fileName,
                PreviewToken = Guid.NewGuid().ToString()
            };

            try
            {
                using var workbook = new XLWorkbook(fileStream);
                var worksheet = workbook.Worksheet(1);

                // Validate header
                var headerValidation = ValidateHeader(worksheet);
                if (!headerValidation.IsValid)
                {
                    preview.Errors.Add(new ExcelValidationErrorDto
                    {
                        RowNumber = 1,
                        ErrorMessage = headerValidation.ErrorMessage
                    });
                    return preview;
                }

                // Lấy dữ liệu từ Excel (bắt đầu từ dòng 2)
                var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1).ToList();
                if (rows == null || rows.Count == 0)
                {
                    preview.Errors.Add(new ExcelValidationErrorDto
                    {
                        RowNumber = 2,
                        ErrorMessage = "File Excel không có dữ liệu (chỉ có tiêu đề)."
                    });
                    return preview;
                }

                // Kiểm tra giới hạn số dòng
                if (rows.Count > MaxRowsPerFile)
                {
                    preview.Errors.Add(new ExcelValidationErrorDto
                    {
                        RowNumber = 1,
                        ErrorMessage = $"Số dòng vượt quá giới hạn {MaxRowsPerFile}. File có {rows.Count} dòng."
                    });
                    return preview;
                }

                preview.TotalRows = rows.Count;

                // Parse từng dòng
                var items = new List<ExcelInwardItemDto>();
                var productCodeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var rowNumber = i + 2; // +2 vì dòng 1 là header

                    try
                    {
                        var maSanPham = row.Cell(1).GetString()?.Trim() ?? "";
                        var tenSanPham = row.Cell(2).GetString()?.Trim();
                        var soLuongStr = row.Cell(3).GetString()?.Trim() ?? "";
                        var donGiaNhapStr = row.Cell(4).GetString()?.Trim() ?? "";
                        var ghiChu = row.Cell(5).GetString()?.Trim();

                        // Validate required fields
                        if (string.IsNullOrEmpty(maSanPham))
                        {
                            preview.Errors.Add(new ExcelValidationErrorDto
                            {
                                RowNumber = rowNumber,
                                MaSanPham = maSanPham,
                                ErrorMessage = "MaSanPham không được để trống."
                            });
                            continue;
                        }

                        // Kiểm tra trùng mã SP trong file
                        if (!productCodeSet.Add(maSanPham))
                        {
                            preview.Errors.Add(new ExcelValidationErrorDto
                            {
                                RowNumber = rowNumber,
                                MaSanPham = maSanPham,
                                ErrorMessage = "Mã sản phẩm bị trùng trong file Excel."
                            });
                            continue;
                        }

                        // Parse số lượng
                        if (!decimal.TryParse(soLuongStr, out var soLuong) || soLuong <= 0)
                        {
                            preview.Errors.Add(new ExcelValidationErrorDto
                            {
                                RowNumber = rowNumber,
                                MaSanPham = maSanPham,
                                ErrorMessage = "SoLuong phải là số nguyên dương."
                            });
                            continue;
                        }

                        // Parse đơn giá
                        if (!decimal.TryParse(donGiaNhapStr, out var donGiaNhap) || donGiaNhap <= 0)
                        {
                            preview.Errors.Add(new ExcelValidationErrorDto
                            {
                                RowNumber = rowNumber,
                                MaSanPham = maSanPham,
                                ErrorMessage = "DonGiaNhap phải là số dương."
                            });
                            continue;
                        }

                        items.Add(new ExcelInwardItemDto
                        {
                            RowNumber = rowNumber,
                            MaSanPham = maSanPham,
                            TenSanPham = tenSanPham,
                            SoLuong = soLuong,
                            DonGiaNhap = donGiaNhap,
                            GhiChu = ghiChu
                        });
                    }
                    catch (Exception ex)
                    {
                        preview.Errors.Add(new ExcelValidationErrorDto
                        {
                            RowNumber = rowNumber,
                            ErrorMessage = $"Lỗi đọc dữ liệu: {ex.Message}"
                        });
                    }
                }

                // Nếu có lỗi ở bước parse cơ bản thì không cần query DB
                if (preview.Errors.Any())
                {
                    return preview;
                }

                // Validate MaSanPham tồn tại trong hệ thống
                var productCodes = items.Select(x => x.MaSanPham).ToList();
                var existingProducts = await _context.Products
                    .Where(p => productCodes.Contains(p.ProductCode))
                    .Select(p => new { p.ProductId, p.ProductCode, p.ProductName })
                    .ToListAsync();

                var existingProductCodes = new HashSet<string>(
                    existingProducts.Select(p => p.ProductCode),
                    StringComparer.OrdinalIgnoreCase);

                var productLookup = existingProducts.ToDictionary(
                    p => p.ProductCode,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var item in items)
                {
                    if (!existingProductCodes.Contains(item.MaSanPham))
                    {
                        preview.Errors.Add(new ExcelValidationErrorDto
                        {
                            RowNumber = item.RowNumber,
                            MaSanPham = item.MaSanPham,
                            ErrorMessage = $"Mã sản phẩm '{item.MaSanPham}' không tồn tại trong hệ thống."
                        });
                    }
                    else
                    {
                        // Resolve ProductId
                        var product = productLookup[item.MaSanPham];
                        item.ResolvedProductId = product.ProductId;
                        item.TenSanPham = product.ProductName; // Cập nhật tên chính xác
                    }
                }

                // Chỉ lấy các dòng hợp lệ (không có lỗi)
                var validItems = items.Where(i => !preview.Errors.Any(e => e.RowNumber == i.RowNumber)).ToList();
                preview.ValidRows = validItems;

                // Lưu preview vào cache
                var previewEntity = new InwardInventoryExcelPreview
                {
                    PreviewToken = preview.PreviewToken,
                    UserId = userId,
                    FileName = fileName,
                    BranchId = uploadDto.BranchId ?? Guid.Empty,
                    SupplierId = uploadDto.SupplierId,
                    InwardType = uploadDto.InwardType,
                    Note = uploadDto.Note,
                    CreatedAt = DateTime.UtcNow,
                    ValidRows = validItems
                };

                // Lưu branchId nếu chưa có
                if (previewEntity.BranchId == Guid.Empty && uploadDto.BranchId == null)
                {
                    var resolvedBranchId = await ResolveBranchIdAsync(userId);
                    previewEntity.BranchId = resolvedBranchId;
                }

                _previewCache[preview.PreviewToken] = previewEntity;
            }
            catch (Exception ex)
            {
                preview.Errors.Add(new ExcelValidationErrorDto
                {
                    RowNumber = 1,
                    ErrorMessage = $"Lỗi đọc file Excel: {ex.Message}"
                });
            }

            return preview;
        }

        /// <summary>
        /// Bước 2: Commit dữ liệu sau khi user xác nhận preview.
        /// </summary>
        public async Task<InwardInventoryExcelResultDto> CommitImportAsync(
            string previewToken,
            Guid userId)
        {
            if (!_previewCache.TryGetValue(previewToken, out var preview))
            {
                throw new InvalidOperationException("Mã xác thực preview không hợp lệ hoặc đã hết hạn.");
            }

            // Kiểm tra user có quyền không
            if (preview.UserId != userId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền thực hiện thao tác này.");
            }

            // Kiểm tra thời hạn preview (24h)
            if ((DateTime.UtcNow - preview.CreatedAt).TotalHours > 24)
            {
                _previewCache.Remove(previewToken);
                throw new InvalidOperationException("Mã xác thực preview đã hết hạn (quá 24 giờ).");
            }

            // Bắt đầu transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                var ticketCode = GenerateTicketCode(preview.InwardType, currentTime);

                // Tạo StockTicket (trạng thái COMPLETED - nhập kho trực tiếp)
                var stockTicket = new StockTicket
                {
                    StockTicketId = Guid.NewGuid(),
                    TicketCode = ticketCode,
                    BranchId = preview.BranchId,
                    UserId = userId,
                    TicketType = GetTicketTypeName(preview.InwardType),
                    Status = "COMPLETED", // Nhập kho trực tiếp, không qua PENDING
                    Notes = preview.Note,
                    CreatedAt = currentTime,
                    CompletedAt = currentTime,
                    ConfirmedBy = userId
                };

                _context.StockTickets.Add(stockTicket);

                decimal totalValue = 0;
                var ticketItems = new List<StockTicketItem>();

                foreach (var item in preview.ValidRows)
                {
                    // Tìm hoặc tạo BranchProduct
                    var branchProduct = await _context.BranchProducts
                        .Include(bp => bp.Product)
                        .FirstOrDefaultAsync(bp =>
                            bp.ProductId == item.ResolvedProductId &&
                            bp.BranchId == preview.BranchId);

                    Guid actualBranchProductId;

                    if (branchProduct == null)
                    {
                        // Tạo BranchProduct mới nếu chưa có ở chi nhánh
                        branchProduct = new BranchProduct
                        {
                            BranchProductId = Guid.NewGuid(),
                            BranchId = preview.BranchId,
                            ProductId = item.ResolvedProductId.Value,
                            CostPrice = item.DonGiaNhap,
                            SellPrice = 0,
                            ActualStock = 0,
                            ReservedStock = 0,
                            AvailableStock = 0,
                            IsActive = 1,
                            DirectSale = 1
                        };

                        _context.BranchProducts.Add(branchProduct);
                        await _context.SaveChangesAsync();
                        actualBranchProductId = branchProduct.BranchProductId;
                    }
                    else
                    {
                        actualBranchProductId = branchProduct.BranchProductId;
                        // Cập nhật CostPrice (ghi đè theo yêu cầu)
                        branchProduct.CostPrice = item.DonGiaNhap;
                    }

                    // Lưu tồn kho trước khi nhập
                    var systemQuantity = branchProduct.ActualStock;

                    // Cập nhật tồn kho (Nhập kho: CỘNG)
                    branchProduct.ActualStock += item.SoLuong;
                    branchProduct.AvailableStock += item.SoLuong;

                    // Tạo StockTicketItem
                    var ticketItem = new StockTicketItem
                    {
                        TicketItemId = Guid.NewGuid(),
                        StockTicketId = stockTicket.StockTicketId,
                        BranchProductId = actualBranchProductId,
                        SystemQuantity = systemQuantity,
                        ActualQuantity = branchProduct.ActualStock,
                        Discrepancy = item.SoLuong
                    };

                    ticketItems.Add(ticketItem);
                    totalValue += item.SoLuong * item.DonGiaNhap;
                }

                // Bulk insert StockTicketItems
                if (ticketItems.Any())
                {
                    _context.StockTicketItems.AddRange(ticketItems);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Lấy thông tin chi nhánh để trả về
                var branch = await _context.Branches.FindAsync(preview.BranchId);

                // Xóa preview khỏi cache sau khi commit thành công
                _previewCache.Remove(previewToken);

                // Ghi audit log
                await SaveAuditLogAsync(userId, preview.FileName, preview.ValidRows.Count, preview.BranchId);

                return new InwardInventoryExcelResultDto
                {
                    TicketId = stockTicket.StockTicketId,
                    TicketCode = stockTicket.TicketCode,
                    BranchId = preview.BranchId,
                    BranchName = branch?.BranchName,
                    SuccessRowCount = preview.ValidRows.Count,
                    TotalValue = totalValue,
                    CreatedAt = stockTicket.CreatedAt
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Lỗi khi import: {ex.Message}", ex);
            }
        }

        private async Task<Guid> ResolveBranchIdAsync(Guid userId)
        {
            // Lấy thông tin user
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user?.DefaultBranchId != null && user.DefaultBranchId != Guid.Empty)
            {
                return user.DefaultBranchId;
            }

            // Lấy từ ICurrentBranch
            var branchId = _currentBranch.GetBranchId();
            if (branchId.HasValue)
            {
                return branchId.Value;
            }

            // Lấy chi nhánh active duy nhất
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

        private (bool IsValid, string ErrorMessage) ValidateHeader(IXLWorksheet worksheet)
        {
            for (int i = 0; i < ExpectedHeaders.Length; i++)
            {
                var header = worksheet.Cell(1, i + 1).GetString()?.Trim() ?? "";
                if (!string.Equals(header, ExpectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"Sai cột thứ {i + 1}:期望 '{ExpectedHeaders[i]}', thực tế '{header}'");
                }
            }
            return (true, "");
        }

        private static string GenerateTicketCode(InwardInventoryType type, DateTime currentTime)
        {
            var typePrefix = type switch
            {
                InwardInventoryType.Purchase => "IMP-",
                InwardInventoryType.CustomerReturn => "ICR-",
                InwardInventoryType.BalanceAdjust => "IBA-",
                _ => "IMP-"
            };

            return $"{typePrefix}{currentTime:yyyyMMddHHmmss}";
        }

        private static string GetTicketTypeName(InwardInventoryType type)
        {
            return type switch
            {
                InwardInventoryType.Purchase => "PURCHASE",
                InwardInventoryType.CustomerReturn => "CUSTOMER_RETURN",
                InwardInventoryType.BalanceAdjust => "BALANCE_ADJUST",
                _ => "PURCHASE"
            };
        }

        private async Task SaveAuditLogAsync(Guid userId, string fileName, int rowCount, Guid branchId)
        {
            var auditLog = new SystemLog
            {
                LogId = Guid.NewGuid(),
                Action = "EXCEL_IMPORT",
                Description = $"Import Excel nhập kho: {fileName}, {rowCount} dòng",
                TableName = "StockTickets",
                UserId = userId,
                EntityType = "StockTicket",
                EntityId = null,
                Level = "INFO",
                Source = "InwardInventoryExcel",
                Timestamp = DateTime.UtcNow
            };

            _context.SystemLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
    }
}
