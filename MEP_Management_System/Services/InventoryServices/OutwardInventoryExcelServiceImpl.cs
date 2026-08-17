using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.InventoryServices;

/// <summary>
/// Service implementing Excel-based outward inventory business logic.
/// </summary>
public class OutwardInventoryExcelServiceImpl : IOutwardInventoryExcelService
{
    private readonly MEPDatabaseContext _context;

    public OutwardInventoryExcelServiceImpl(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<OutwardExcelPreviewDto>> ParseAndValidateExcelAsync(
        IFormFile file, Guid branchId, Guid userId, Guid? targetBranchId)
    {
        if (file == null || file.Length == 0)
            throw new BusinessException("MSG-400", "Vui long chon file Excel.");

        var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (extension != ".xlsx" && extension != ".xls")
            throw new BusinessException("MSG-400", "Chi chap nhan file Excel (.xlsx, .xls).");

        var resolvedBranchId = await ResolveBranchIdInternalAsync(targetBranchId, userId);

        using var stream = file.OpenReadStream();
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
                throw new BusinessException("MSG-400", $"File Excel thieu cot bat buoc: {req}");
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

            // Validate quantity
            if (!int.TryParse(soLuongStr, out int soLuong) || soLuong <= 0)
            {
                errors.Add(new OutwardExcelErrorDto { RowNumber = row, MaSanPham = maSanPham, ErrorMessage = "SoLuong phai la so nguyen duong." });
                continue;
            }

            var ticketType = GetTicketTypeFromReason(lyDo);
            if (ticketType == null)
            {
                errors.Add(new OutwardExcelErrorDto { RowNumber = row, MaSanPham = maSanPham, ErrorMessage = $"LyDo '{lyDo}' khong hop le. Chi chap nhan: Xuat huy / Dieu chuyen." });
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

        // Validate MaSanPham exists in DB
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
                errors.Add(new OutwardExcelErrorDto { RowNumber = item.RowNumber, MaSanPham = item.MaSanPham, ErrorMessage = $"Ma san pham '{item.MaSanPham}' khong ton tai trong he thong." });
                validRows.Remove(item);
                continue;
            }

            var dbProduct = productLookup[item.MaSanPham];
            item.ProductId = dbProduct.ProductId;

            // Cross-validate product name
            var excelName = (item.TenSanPham ?? "").Trim();
            var dbName = (dbProduct.ProductName ?? "").Trim();
            if (!string.IsNullOrEmpty(excelName) && !excelName.Equals(dbName, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new OutwardExcelErrorDto
                {
                    RowNumber = item.RowNumber,
                    MaSanPham = item.MaSanPham,
                    ErrorMessage = $"Ma san pham '{item.MaSanPham}' co ten trong file la '{excelName}', khong khop voi ten tren he thong ('{dbName}'). Vui long kiem tra lai"
                });
                validRows.Remove(item);
                continue;
            }

            item.TenSanPham = dbName;
        }

        // Check products exist in branch warehouse
        if (validRows.Count > 0)
        {
            var validProductIds = validRows.Select(x => x.ProductId).Where(pid => pid != Guid.Empty).Distinct().ToList();
            var branchProducts = await _context.BranchProducts
                .Where(bp => validProductIds.Contains(bp.ProductId) && bp.BranchId == resolvedBranchId)
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
                        ErrorMessage = $"San pham '{productName}' (ma: {item.MaSanPham}) chua co trong kho cua chi nhanh. Vui long nhap kho truoc."
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

        return new ApiResponse<OutwardExcelPreviewDto>
        {
            Success = true,
            Message = errors.Any() ? "Co loi trong file Excel." : "Validate thanh cong.",
            Data = new OutwardExcelPreviewDto
            {
                ValidRows = validRows,
                Errors = errors,
                Groups = groups,
                HasErrors = errors.Any()
            }
        };
    }

    public async Task<ApiResponse<OutwardExcelResultDto>> CommitImportAsync(
        OutwardExcelCommitDto dto, Guid branchId, Guid userId)
    {
        var resolvedBranchId = await ResolveBranchIdInternalAsync(dto.BranchId, userId, dto.TargetBranchId);

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
                BranchId = resolvedBranchId,
                UserId = userId,
                TicketType = grp.Key,
                Status = "PENDING",
                Notes = $"Import tu Excel - {first.LyDo}",
                CreatedAt = now,
                TotalAmount = 0
            };

            _context.StockTickets.Add(stockTicket);
            await _context.SaveChangesAsync();

            decimal totalValue = 0;
            var items = new List<StockTicketItem>();
            foreach (var row in grp)
            {
                var branchProduct = await _context.BranchProducts
                    .FirstOrDefaultAsync(bp => bp.ProductId == row.ProductId && bp.BranchId == resolvedBranchId);

                if (branchProduct == null)
                    throw new BusinessException("MSG-400", $"San pham '{row.MaSanPham}' chua co trong kho chi nhanh. Vui long nhap kho truoc.");

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

        return new ApiResponse<OutwardExcelResultDto>
        {
            Success = true,
            Message = $"Tao thanh cong {createdTickets.Count} phieu xuat kho (PENDING). Xac nhan tung phieu de tru ton kho.",
            Data = new OutwardExcelResultDto
            {
                CreatedTickets = createdTickets,
                TotalTickets = createdTickets.Count,
                TotalValue = createdTickets.Sum(x => x.TotalValue)
            }
        };
    }

    public async Task ResolveBranchIdAsync(Guid? requestedBranchId, Guid userId, Guid? targetBranchId)
    {
        await ResolveBranchIdInternalAsync(requestedBranchId, userId, targetBranchId);
    }

    private async Task<Guid> ResolveBranchIdInternalAsync(Guid? requestedBranchId, Guid userId, Guid? targetBranchId = null)
    {
        if (requestedBranchId.HasValue && requestedBranchId != Guid.Empty)
            return requestedBranchId.Value;

        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId);
        if (user?.DefaultBranchId is Guid defaultBranchId && defaultBranchId != Guid.Empty)
            return defaultBranchId;

        if (targetBranchId.HasValue && targetBranchId != Guid.Empty)
            return targetBranchId.Value;

        var activeBranch = await _context.Branches.AsNoTracking()
            .Where(b => b.IsActive == 1).FirstOrDefaultAsync();
        if (activeBranch != null)
            return activeBranch.BranchId;

        throw new BusinessException("MSG-404", "Khong tim thay chi nhanh hop le.");
    }

    private static string? GetTicketTypeFromReason(string reason)
    {
        var r = (reason ?? "").Trim().ToLower();
        if (r == "xuat huy" || r == "xuat huy" || r == "write_off") return "WRITE_OFF";
        if (r == "dieu chuyen" || r == "dieu chuyen" || r == "transfer") return "TRANSFER";
        return null;
    }
}
