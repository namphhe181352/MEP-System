using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.SaleServices;

public class ExpenseServiceImpl : IExpenseService
{
    private readonly MEPDatabaseContext _context;

    public ExpenseServiceImpl(MEPDatabaseContext context)
    {
        _context = context;
    }

    #region Voucher Operations

    public async Task<PaginatedResponse<ExpenseVoucherResponseDto>> GetVouchersAsync(Guid branchId, ExpenseVoucherParameters @params)
    {
        if (@params.PageNumber < 1) @params.PageNumber = 1;
        if (@params.PageSize < 1 || @params.PageSize > 100) @params.PageSize = 20;

        var query = _context.ExpenseVouchers
            .AsNoTracking()
            .Where(ev => ev.BranchId == branchId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(@params.Status))
        {
            var normalizedStatus = @params.Status.Trim().ToUpperInvariant();
            if (normalizedStatus != "ALL")
            {
                query = query.Where(ev => ev.Status == normalizedStatus);
            }
        }
        else
        {
            query = query.Where(ev => ev.Status != "CANCELLED");
        }

        if (@params.CategoryId.HasValue)
        {
            query = query.Where(ev => ev.CategoryId == @params.CategoryId.Value);
        }

        if (@params.SupplierId.HasValue)
        {
            query = query.Where(ev => ev.SupplierId == @params.SupplierId.Value);
        }

        if (@params.FromDate.HasValue)
        {
            query = query.Where(ev => ev.CreatedAt >= @params.FromDate.Value);
        }

        if (@params.ToDate.HasValue)
        {
            query = query.Where(ev => ev.CreatedAt <= @params.ToDate.Value.AddDays(1));
        }

        var sortedQuery = ApplyVoucherSorting(query, @params.Sort, @params.Order);
        var totalCount = await sortedQuery.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)@params.PageSize);

        var pageIds = await sortedQuery
            .Skip((@params.PageNumber - 1) * @params.PageSize)
            .Take(@params.PageSize)
            .Select(ev => ev.VoucherId)
            .ToListAsync();

        var pageIdSet = pageIds.Distinct().ToList();

        var items = await _context.ExpenseVouchers
            .AsNoTracking()
            .Where(ev => pageIdSet.Contains(ev.VoucherId))
            .Include(ev => ev.Category)
            .Include(ev => ev.Supplier)
            .Include(ev => ev.CreatedByUser)
            .Include(ev => ev.ConfirmedByUser)
            .Include(ev => ev.CancelledByUser)
            .Include(ev => ev.Branch)
            .ToListAsync();

        items = items
            .GroupBy(ev => ev.VoucherId)
            .Select(g => g.First())
            .OrderBy(ev => pageIds.IndexOf(ev.VoucherId))
            .ToList();

        var resultList = items.Select(MapToExpenseVoucherResponse).ToList();

        return new PaginatedResponse<ExpenseVoucherResponseDto>
        {
            Items = resultList,
            TotalCount = totalCount,
            PageNumber = @params.PageNumber,
            PageSize = @params.PageSize,
            TotalPages = totalPages,
            HasNextPage = @params.PageNumber < totalPages,
            HasPreviousPage = @params.PageNumber > 1
        };
    }

    public async Task<ExpenseVoucherResponseDto?> GetVoucherByIdAsync(Guid id, Guid branchId)
    {
        var voucher = await _context.ExpenseVouchers
            .Include(ev => ev.Category)
            .Include(ev => ev.Supplier)
            .Include(ev => ev.CreatedByUser)
            .Include(ev => ev.ConfirmedByUser)
            .Include(ev => ev.CancelledByUser)
            .Include(ev => ev.Branch)
            .FirstOrDefaultAsync(ev => ev.VoucherId == id && ev.BranchId == branchId);

        return voucher == null ? null : MapToExpenseVoucherResponse(voucher);
    }

    public async Task<ExpenseVoucherResponseDto> CreateVoucherAsync(ExpenseVoucherCreateDto dto, Guid userId, Guid branchId)
    {
        if (dto.Amount <= 0)
            throw new BusinessException("Số tiền chi phải lớn hơn 0.");
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new BusinessException("Lý do chi là bắt buộc.");

        var category = await _context.ExpenseCategories
            .FirstOrDefaultAsync(c => c.CategoryId == dto.CategoryId && c.IsActive == 1);
        if (category == null)
            throw new BusinessException("Nhóm chi phí không hợp lệ hoặc đã bị vô hiệu hóa.");

        var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var voucherCode = await GenerateVoucherCode(branchId, currentTime);

        var voucher = new ExpenseVoucher
        {
            VoucherId = Guid.NewGuid(),
            VoucherCode = voucherCode,
            BranchId = branchId,
            CategoryId = dto.CategoryId,
            SupplierId = null,
            Amount = dto.Amount,
            Reason = dto.Reason.Trim(),
            Note = dto.Note?.Trim(),
            Status = "PENDING",
            CreatedBy = userId,
            CreatedAt = currentTime
        };

        _context.ExpenseVouchers.Add(voucher);
        await _context.SaveChangesAsync();

        await WriteSystemLog("CREATE_EXPENSE_VOUCHER", "ExpenseVoucher", voucher.VoucherId,
            $"Tạo phiếu chi {voucherCode} - Số tiền: {voucher.Amount:N0}đ - Lý do: {voucher.Reason}");

        var createdVoucher = await _context.ExpenseVouchers
            .Include(ev => ev.Category)
            .Include(ev => ev.Supplier)
            .Include(ev => ev.CreatedByUser)
            .Include(ev => ev.Branch)
            .FirstAsync(ev => ev.VoucherId == voucher.VoucherId);

        return MapToExpenseVoucherResponse(createdVoucher);
    }

    public async Task<ExpenseVoucherResponseDto?> UpdateVoucherAsync(Guid id, UpdateExpenseVoucherDto dto, Guid userId, bool isOwner)
    {
        var voucher = await _context.ExpenseVouchers.FindAsync(id);
        if (voucher == null) return null;

        if (voucher.Status != "PENDING")
            throw new BusinessException("Chỉ được sửa phiếu chi ở trạng thái chờ xác nhận.");

        if (voucher.CreatedBy != userId && !isOwner)
            throw new UnauthorizedAccessException("Bạn không có quyền sửa phiếu chi này.");

        if (!string.IsNullOrWhiteSpace(dto.Reason)) voucher.Reason = dto.Reason.Trim();
        if (dto.Amount.HasValue && dto.Amount.Value > 0) voucher.Amount = dto.Amount.Value;
        if (dto.Note != null) voucher.Note = dto.Note.Trim();

        await _context.SaveChangesAsync();
        return await GetVoucherByIdAsync(id, voucher.BranchId);
    }

    public async Task<ExpenseVoucherResponseDto?> ConfirmVoucherAsync(Guid id, Guid userId, Guid branchId)
    {
        var voucher = await _context.ExpenseVouchers
            .Include(ev => ev.Category)
            .Include(ev => ev.Supplier)
            .Include(ev => ev.CreatedByUser)
            .Include(ev => ev.Branch)
            .FirstOrDefaultAsync(ev => ev.VoucherId == id && ev.BranchId == branchId);

        if (voucher == null) return null;

        var currentStatus = (voucher.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (currentStatus == "COMPLETED")
            throw new BusinessException("Phiếu đã được xác nhận trước đó.");
        if (currentStatus == "CANCELLED")
            throw new BusinessException("Phiếu đã bị hủy. Không thể xác nhận.");
        if (currentStatus != "PENDING")
            throw new BusinessException($"Trạng thái phiếu không hợp lệ để xác nhận: '{voucher.Status}'.");

        var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        voucher.Status = "COMPLETED";
        voucher.ConfirmedBy = userId;
        voucher.CompletedAt = currentTime;

        await _context.SaveChangesAsync();

        await WriteSystemLog("CONFIRM_EXPENSE_VOUCHER", "ExpenseVoucher", voucher.VoucherId,
            $"Xác nhận phiếu chi {voucher.VoucherCode} - Số tiền: {voucher.Amount:N0}đ");

        var confirmedVoucher = await _context.ExpenseVouchers
            .Include(ev => ev.Category)
            .Include(ev => ev.Supplier)
            .Include(ev => ev.CreatedByUser)
            .Include(ev => ev.ConfirmedByUser)
            .Include(ev => ev.CancelledByUser)
            .Include(ev => ev.Branch)
            .FirstAsync(ev => ev.VoucherId == voucher.VoucherId);

        return MapToExpenseVoucherResponse(confirmedVoucher);
    }

    public async Task<ExpenseVoucherResponseDto?> CancelVoucherAsync(Guid id, CancelExpenseVoucherDto dto, Guid userId, Guid branchId)
    {
        if (string.IsNullOrWhiteSpace(dto?.CancelReason))
            throw new BusinessException("Lý do hủy là bắt buộc.");

        var voucher = await _context.ExpenseVouchers
            .Include(ev => ev.Category)
            .Include(ev => ev.Supplier)
            .Include(ev => ev.CreatedByUser)
            .Include(ev => ev.Branch)
            .FirstOrDefaultAsync(ev => ev.VoucherId == id && ev.BranchId == branchId);

        if (voucher == null) return null;

        var currentStatus = (voucher.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (currentStatus == "CANCELLED")
            throw new BusinessException("Phiếu đã được hủy trước đó.");
        if (currentStatus == "COMPLETED")
            throw new BusinessException("Phiếu đã được xác nhận hoàn thành. Không thể hủy.");
        if (currentStatus != "PENDING")
            throw new BusinessException($"Trạng thái phiếu không hợp lệ để hủy: '{voucher.Status}'.");

        var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        voucher.Status = "CANCELLED";
        voucher.CancelledBy = userId;
        voucher.CancelledAt = currentTime;
        voucher.CancelReason = dto.CancelReason.Trim();

        await _context.SaveChangesAsync();

        await WriteSystemLog("CANCEL_EXPENSE_VOUCHER", "ExpenseVoucher", voucher.VoucherId,
            $"Hủy phiếu chi {voucher.VoucherCode} - Lý do: {dto.CancelReason}");

        var cancelledVoucher = await _context.ExpenseVouchers
            .Include(ev => ev.Category)
            .Include(ev => ev.Supplier)
            .Include(ev => ev.CreatedByUser)
            .Include(ev => ev.ConfirmedByUser)
            .Include(ev => ev.CancelledByUser)
            .Include(ev => ev.Branch)
            .FirstAsync(ev => ev.VoucherId == voucher.VoucherId);

        return MapToExpenseVoucherResponse(cancelledVoucher);
    }

    #endregion

    #region Category Operations

    public async Task<List<ExpenseCategoryDto>> GetCategoriesAsync(Guid branchId)
    {
        var categories = await _context.ExpenseCategories
            .Where(c => c.BranchId == branchId && c.IsActive == 1)
            .Include(c => c.ExpenseVouchers)
            .OrderBy(c => c.CategoryName)
            .ToListAsync();

        return categories.Select(c => new ExpenseCategoryDto
        {
            CategoryId = c.CategoryId,
            CategoryName = c.CategoryName,
            IsActive = c.IsActive,
            VoucherCount = c.ExpenseVouchers.Count,
            PendingCount = c.ExpenseVouchers.Count(ev => ev.Status == "PENDING")
        }).ToList();
    }

    public async Task<ExpenseCategoryDto> CreateCategoryAsync(ExpenseCategoryUpsertDto dto, Guid userId, Guid branchId)
    {
        if (string.IsNullOrWhiteSpace(dto.CategoryName))
            throw new BusinessException("Tên nhóm chi phí là bắt buộc.");

        var existing = await _context.ExpenseCategories
            .Include(c => c.ExpenseVouchers)
            .FirstOrDefaultAsync(c => c.CategoryName.ToLower() == dto.CategoryName.Trim().ToLower() && c.BranchId == branchId);
        if (existing != null)
        {
            if (existing.IsActive == 0)
            {
                existing.IsActive = 1;
                await _context.SaveChangesAsync();
                
                await WriteSystemLog("RESTORE_EXPENSE_CATEGORY", "ExpenseCategory", existing.CategoryId,
                    $"Khôi phục nhóm chi phí đã xóa: {existing.CategoryName}");

                return new ExpenseCategoryDto
                {
                    CategoryId = existing.CategoryId,
                    CategoryName = existing.CategoryName,
                    IsActive = existing.IsActive,
                    VoucherCount = existing.ExpenseVouchers?.Count ?? 0,
                    PendingCount = existing.ExpenseVouchers?.Count(ev => ev.Status == "PENDING") ?? 0
                };
            }
            throw new BusinessException("Tên nhóm chi phí đã tồn tại.");
        }

        var category = new ExpenseCategory
        {
            CategoryId = Guid.NewGuid(),
            CategoryName = dto.CategoryName.Trim(),
            IsActive = 1,
            BranchId = branchId,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.ExpenseCategories.Add(category);
        await _context.SaveChangesAsync();

        await WriteSystemLog("CREATE_EXPENSE_CATEGORY", "ExpenseCategory", category.CategoryId,
            $"Tạo nhóm chi phí: {category.CategoryName}");

        return new ExpenseCategoryDto
        {
            CategoryId = category.CategoryId,
            CategoryName = category.CategoryName,
            IsActive = category.IsActive,
            VoucherCount = 0,
            PendingCount = 0
        };
    }

    public async Task<ExpenseCategoryDto?> UpdateCategoryAsync(Guid id, ExpenseCategoryUpsertDto dto, Guid userId, Guid branchId)
    {
        if (string.IsNullOrWhiteSpace(dto.CategoryName))
            throw new BusinessException("Tên nhóm chi phí là bắt buộc.");

        var trimmedName = dto.CategoryName.Trim();

        var category = await _context.ExpenseCategories
            .Include(c => c.ExpenseVouchers)
            .FirstOrDefaultAsync(c => c.CategoryId == id && c.BranchId == branchId);

        if (category == null) return null;

        var duplicate = await _context.ExpenseCategories
            .FirstOrDefaultAsync(c => c.CategoryId != id && c.BranchId == branchId &&
                c.CategoryName.ToLower() == trimmedName.ToLower());
        if (duplicate != null)
            throw new BusinessException("Tên nhóm chi phí đã tồn tại.");

        var oldName = category.CategoryName;
        category.CategoryName = trimmedName;

        await _context.SaveChangesAsync();

        await WriteSystemLog("UPDATE_EXPENSE_CATEGORY", "ExpenseCategory", category.CategoryId,
            $"Đổi tên nhóm chi phí: '{oldName}' → '{category.CategoryName}'");

        return new ExpenseCategoryDto
        {
            CategoryId = category.CategoryId,
            CategoryName = category.CategoryName,
            IsActive = category.IsActive,
            VoucherCount = category.ExpenseVouchers.Count,
            PendingCount = category.ExpenseVouchers.Count(ev => ev.Status == "PENDING")
        };
    }

    public async Task<bool> DeleteCategoryAsync(Guid id, Guid userId, Guid branchId)
    {
        var category = await _context.ExpenseCategories
            .Include(c => c.ExpenseVouchers)
            .FirstOrDefaultAsync(c => c.CategoryId == id && c.BranchId == branchId);

        if (category == null)
            throw new KeyNotFoundException("Không tìm thấy nhóm chi phí.");

        var pendingVouchers = category.ExpenseVouchers.Where(ev => ev.Status == "PENDING").ToList();
        if (pendingVouchers.Any())
            throw new BusinessException($"Không thể xóa nhóm chi phí đang có {pendingVouchers.Count} phiếu PENDING.");

        var categoryName = category.CategoryName;
        var totalVouchers = category.ExpenseVouchers.Count;

        try {
            category.IsActive = 0;
            await _context.SaveChangesAsync();
        } catch (Exception ex) {
            throw new BusinessException($"Không thể xóa do lỗi cơ sở dữ liệu: {ex.InnerException?.Message ?? ex.Message}");
        }

        await WriteSystemLog("DELETE_EXPENSE_CATEGORY", "ExpenseCategory", id,
            $"Ẩn nhóm chi phí: {categoryName} (đang có {totalVouchers} phiếu)");

        return true;
    }

    #endregion

    #region Private Helpers

    private async Task<string> GenerateVoucherCode(Guid branchId, DateTime currentTime)
    {
        var prefix = "PXT";

        for (int attempt = 1; attempt <= 5; attempt++)
        {
            var todayCount = await _context.ExpenseVouchers
                .Where(ev => ev.BranchId == branchId &&
                    ev.CreatedAt != null &&
                    ev.CreatedAt.Value.Date == currentTime.Date)
                .CountAsync();

            var nextNumber = todayCount + attempt;
            var code = $"{prefix}-{nextNumber:000000}";

            var exists = await _context.ExpenseVouchers.AnyAsync(ev => ev.VoucherCode == code);
            if (!exists) return code;
        }

        return $"{prefix}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    private IQueryable<ExpenseVoucher> ApplyVoucherSorting(IQueryable<ExpenseVoucher> query, string? sortBy, string? order)
    {
        bool isDesc = order?.ToLower() == "desc";
        string sort = sortBy?.ToLower() ?? "createdat";

        return sort switch
        {
            "code" => isDesc ? query.OrderByDescending(ev => ev.VoucherCode) : query.OrderBy(ev => ev.VoucherCode),
            "amount" => isDesc ? query.OrderByDescending(ev => ev.Amount) : query.OrderBy(ev => ev.Amount),
            "status" => isDesc ? query.OrderByDescending(ev => ev.Status) : query.OrderBy(ev => ev.Status),
            "createdat" => isDesc ? query.OrderByDescending(ev => ev.CreatedAt) : query.OrderBy(ev => ev.CreatedAt),
            "category" => isDesc ? query.OrderByDescending(ev => ev.Category.CategoryName) : query.OrderBy(ev => ev.Category.CategoryName),
            _ => query.OrderByDescending(ev => ev.CreatedAt)
        };
    }

    private ExpenseVoucherResponseDto MapToExpenseVoucherResponse(ExpenseVoucher ev)
    {
        var isPending = ev.Status == "PENDING";
        return new ExpenseVoucherResponseDto
        {
            VoucherId = ev.VoucherId,
            VoucherCode = ev.VoucherCode,
            BranchId = ev.BranchId,
            BranchName = ev.Branch?.BranchName,
            CategoryId = ev.CategoryId,
            CategoryName = ev.Category?.CategoryName,
            SupplierId = ev.SupplierId,
            SupplierName = ev.Supplier?.SupplierName,
            Amount = ev.Amount,
            Reason = ev.Reason,
            Note = ev.Note,
            Status = ev.Status,
            CreatedBy = ev.CreatedBy,
            CreatedByName = ev.CreatedByUser?.FullName ?? "Hệ thống",
            CreatedAt = ev.CreatedAt,
            ConfirmedBy = ev.ConfirmedBy,
            ConfirmedByName = ev.ConfirmedByUser?.FullName,
            CompletedAt = ev.CompletedAt,
            CancelledBy = ev.CancelledBy,
            CancelledByName = ev.CancelledByUser?.FullName,
            CancelledAt = ev.CancelledAt,
            CancelReason = ev.CancelReason,
            CanConfirm = isPending,
            CanCancel = isPending
        };
    }

    private async Task WriteSystemLog(string action, string entityType, Guid entityId, string description)
    {
        try
        {
            var log = new SystemLog
            {
                LogId = Guid.NewGuid(),
                Action = action,
                TableName = entityType.ToLower(),
                EntityType = entityType,
                EntityId = entityId,
                Description = description,
                Level = "INFO",
                Source = "ExpenseService",
                Timestamp = DateTime.UtcNow
            };

            _context.SystemLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch
        {
            // Silent error
        }
    }

    #endregion
}
