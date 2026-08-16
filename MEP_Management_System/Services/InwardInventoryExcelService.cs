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
                        ErrorMessage = "File Excel không chứa dữ liệu nhập kho nào."
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

                // Parse từng dòng - nếu trùng ProductCode thì cộng dồn số lượng,
                // tính đơn giá bình quân gia quyền, nối ghi chú
                var items = new List<ExcelInwardItemDto>();
                var productCodeIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                // Lưu raw rows để phát hiện chênh lệch giá
                var rawRowsByCode = new Dictionary<string, List<(int RowNumber, decimal DonGiaNhap, string TenSanPham)>>(StringComparer.OrdinalIgnoreCase);

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

                        // Parse số lượng
                        if (!decimal.TryParse(soLuongStr, out var soLuong) || soLuong <= 0)
                        {
                            preview.Errors.Add(new ExcelValidationErrorDto
                            {
                                RowNumber = rowNumber,
                                MaSanPham = maSanPham,
                                ErrorMessage = $"Dòng {rowNumber}: Số lượng nhập phải lớn hơn 0."
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
                                ErrorMessage = $"Dòng {rowNumber}: Đơn giá nhập phải là số dương."
                            });
                            continue;
                        }

                        // Lưu raw row để phát hiện chênh lệch giá sau này
                        if (!rawRowsByCode.ContainsKey(maSanPham))
                            rawRowsByCode[maSanPham] = new List<(int, decimal, string)>();
                        rawRowsByCode[maSanPham].Add((rowNumber, donGiaNhap, tenSanPham ?? maSanPham));

                        // Nếu mã SP đã tồn tại trong danh sách -> cộng dồn (aggregation)
                        if (productCodeIndexMap.TryGetValue(maSanPham, out var existingIndex))
                        {
                            var existing = items[existingIndex];
                            var oldTotal = existing.SoLuong * existing.DonGiaNhap;
                            var newTotal = soLuong * donGiaNhap;
                            existing.SoLuong += soLuong;
                            // Đơn giá bình quân gia quyền
                            existing.DonGiaNhap = Math.Round((oldTotal + newTotal) / existing.SoLuong, 2);
                            // Nối ghi chú
                            if (!string.IsNullOrWhiteSpace(ghiChu))
                            {
                                existing.GhiChu = string.IsNullOrWhiteSpace(existing.GhiChu)
                                    ? ghiChu
                                    : existing.GhiChu + "; " + ghiChu;
                            }
                            // Cập nhật RowNumber thành row cuối cùng
                            existing.RowNumber = rowNumber;
                        }
                        else
                        {
                            productCodeIndexMap[maSanPham] = items.Count;
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

                // Bước 1: Kiểm tra tính nhất quán nội bộ file Excel
                // Cùng 1 Mã hàng bắt buộc chỉ có 1 Tên hàng và 1 Đơn giá
                foreach (var kvp in rawRowsByCode)
                {
                    var rowsForCode = kvp.Value;
                    if (rowsForCode.Count < 2) continue;

                    var productName = rowsForCode.First().TenSanPham;

                    // 1a. Kiểm tra Tên hàng: 1 mã không được gắn cho nhiều tên khác nhau
                    var distinctNames = rowsForCode
                        .Select(r => (r.TenSanPham ?? "").Trim().ToLower())
                        .Where(n => !string.IsNullOrEmpty(n))
                        .Distinct()
                        .ToList();
                    if (distinctNames.Count > 1)
                    {
                        var nameDetails = string.Join("\n", rowsForCode.Select(r =>
                            $"  - Dòng {r.RowNumber}: \"{r.TenSanPham}\""));
                        preview.Errors.Add(new ExcelValidationErrorDto
                        {
                            RowNumber = rowsForCode.First().RowNumber,
                            MaSanPham = kvp.Key,
                            ErrorMessage =
                                $"Mã hàng {kvp.Key} đang bị đặt cho nhiều tên sản phẩm khác nhau:\n" +
                                $"{nameDetails}\n" +
                                $"→ Vui lòng sửa lại mã hàng trong file Excel."
                        });
                        continue;
                    }

                    // 1b. Kiểm tra Đơn giá: 1 mã không được có nhiều đơn giá khác nhau
                    var prices = rowsForCode.Select(r => r.DonGiaNhap).Distinct().ToList();
                    if (prices.Count >= 2)
                    {
                        var lineDetails = string.Join("\n", rowsForCode.Select(r =>
                            $"  - Dòng {r.RowNumber}: {r.DonGiaNhap:N0} VNĐ"));
                        preview.Errors.Add(new ExcelValidationErrorDto
                        {
                            RowNumber = rowsForCode.First().RowNumber,
                            MaSanPham = kvp.Key,
                            ErrorMessage =
                                $"Sản phẩm {kvp.Key} ({productName}) xuất hiện {rowsForCode.Count} lần với đơn giá khác nhau:\n" +
                                $"{lineDetails}\n" +
                                $"→ Vui lòng thống nhất đơn giá cho sản phẩm này trong file Excel trước khi import."
                        });
                    }
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

                // Lấy toàn bộ tên sản phẩm trong DB để kiểm tra trùng tên cho mã mới (Bước 3)
                var allProductNames = await _context.Products
                    .Select(p => new { p.ProductId, p.ProductCode, p.ProductName })
                    .ToListAsync();

                foreach (var item in items)
                {
                    if (!existingProductCodes.Contains(item.MaSanPham))
                    {
                        // Bước 3: Mã chưa có trong DB -> kiểm tra Tên có bị trùng với sản phẩm khác không
                        var excelName = (item.TenSanPham ?? "").Trim();
                        if (!string.IsNullOrEmpty(excelName))
                        {
                            var duplicateByName = allProductNames
                                .FirstOrDefault(p => string.Equals(p.ProductName.Trim(), excelName, StringComparison.OrdinalIgnoreCase));
                            if (duplicateByName != null)
                            {
                                preview.Errors.Add(new ExcelValidationErrorDto
                                {
                                    RowNumber = item.RowNumber,
                                    MaSanPham = item.MaSanPham,
                                    ErrorMessage =
                                        $"Dòng {item.RowNumber}: Tên sản phẩm '{item.TenSanPham}' " +
                                        $"đã tồn tại trên hệ thống với mã '{duplicateByName.ProductCode}'. " +
                                        $"Vui lòng kiểm tra lại mã '{item.MaSanPham}' trong file Excel xem có phải gõ nhầm mã hay không."
                                });
                                continue;
                            }
                        }
                        // Mã mới hoàn toàn, không trùng tên -> tạo Product ngay trong Parse
                        var newProductId = Guid.NewGuid();
                        var newProduct = new Product
                        {
                            ProductId = newProductId,
                            ProductCode = item.MaSanPham,
                            ProductName = item.TenSanPham ?? item.MaSanPham,
                            BaseUnit = "Cái",
                            Status = 1,
                            SalesChannelLinked = 0,
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = userId
                        };
                        _context.Products.Add(newProduct);
                        await _context.SaveChangesAsync();
                        item.ResolvedProductId = newProductId;
                    }
                    else
                    {
                        // Resolve ProductId
                        var product = productLookup[item.MaSanPham];
                        item.ResolvedProductId = product.ProductId;
                        // Bước 2: Kiểm tra bất nhất Tên hàng giữa Excel và Database
                        var excelName = (item.TenSanPham ?? "").Trim();
                        var dbName = (product.ProductName ?? "").Trim();
                        if (!string.IsNullOrEmpty(excelName) && !string.IsNullOrEmpty(dbName) &&
                            !string.Equals(excelName, dbName, StringComparison.OrdinalIgnoreCase))
                        {
                            preview.Errors.Add(new ExcelValidationErrorDto
                            {
                                RowNumber = item.RowNumber,
                                MaSanPham = item.MaSanPham,
                                ErrorMessage =
                                    $"Dòng {item.RowNumber}: Bất nhất dữ liệu! Mã '{item.MaSanPham}' " +
                                    $"trên hệ thống là '{product.ProductName}', " +
                                    $"nhưng file Excel ghi là '{item.TenSanPham}'. " +
                                    $"Vui lòng kiểm tra lại mã hoặc tên sản phẩm."
                            });
                        }
                        else
                        {
                            item.TenSanPham = product.ProductName; // Cập nhật tên chính xác từ DB
                        }
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
                    ConfirmedBy = userId,
                    TotalAmount = 0 // sẽ set sau khi tính tổng
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
                            DirectSale = 0
                        };

                        _context.BranchProducts.Add(branchProduct);
                        await _context.SaveChangesAsync();
                        actualBranchProductId = branchProduct.BranchProductId;
                    }
                    else
                    {
                        actualBranchProductId = branchProduct.BranchProductId;
                        // Cập nhật CostPrice theo giá vốn bình quân di động
                        var oldStock = branchProduct.ActualStock;
                        var oldCost = branchProduct.CostPrice;
                        if (oldStock + item.SoLuong > 0)
                        {
                            branchProduct.CostPrice = Math.Round(
                                ((oldStock * oldCost) + (item.SoLuong * item.DonGiaNhap)) / (oldStock + item.SoLuong),
                                2
                            );
                        }
                        else
                        {
                            branchProduct.CostPrice = item.DonGiaNhap;
                        }
                    }

                    // Lưu tồn kho trước khi nhập
                    var systemQuantity = branchProduct.ActualStock;

                    // Cập nhật tồn kho (Nhập kho: CỘNG)
                    branchProduct.ActualStock += item.SoLuong;
                    branchProduct.AvailableStock += item.SoLuong;

                    // Ghi inventory_ledger cho import Excel
                    _context.InventoryLedgers.Add(new InventoryLedger
                    {
                        LedgerId = Guid.NewGuid(),
                        BranchId = preview.BranchId,
                        BranchProductId = actualBranchProductId,
                        DocType = "PURCHASE",
                        DocId = stockTicket.StockTicketId,
                        DocCode = stockTicket.TicketCode,
                        TransactionTime = DateTime.UtcNow,
                        QtyChange = item.SoLuong,
                        UnitCostPrice = Math.Round(item.DonGiaNhap, 4),
                        TotalCostValue = Math.Round(item.SoLuong * item.DonGiaNhap, 4),
                        StockAfter = branchProduct.ActualStock,
                        AvgCostAfter = Math.Round(branchProduct.CostPrice, 4),
                        CreatedAt = DateTime.UtcNow
                    });

                    // Tạo StockTicketItem - snapshot UnitPrice tại thời điểm nhập
                    var ticketItem = new StockTicketItem
                    {
                        TicketItemId = Guid.NewGuid(),
                        StockTicketId = stockTicket.StockTicketId,
                        BranchProductId = actualBranchProductId,
                        SystemQuantity = systemQuantity,
                        ActualQuantity = branchProduct.ActualStock,
                        Discrepancy = item.SoLuong,
                        UnitPrice = item.DonGiaNhap
                    };

                    ticketItems.Add(ticketItem);
                    totalValue += item.SoLuong * item.DonGiaNhap;
                }

                // Bulk insert StockTicketItems
                if (ticketItems.Any())
                {
                    _context.StockTicketItems.AddRange(ticketItems);
                }

                // Gán TotalAmount cho StockTicket trước khi SaveChanges (để công nợ NCC tính đúng)
                stockTicket.TotalAmount = totalValue;

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
                    CreatedAt = stockTicket.CreatedAt ?? DateTime.UtcNow
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

            if (user?.DefaultBranchId is Guid defaultBranchId && defaultBranchId != Guid.Empty)
            {

                return defaultBranchId;

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
                    return (false, $"Cấu trúc file Excel không đúng định dạng mẫu. Vui lòng kiểm tra lại các cột tiêu đề. Cột {i + 1}: mong đợi '{ExpectedHeaders[i]}', thực tế là '{header}'");
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
