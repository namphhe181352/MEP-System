using System.Linq;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;

namespace MEP_Management_System.Services;

/// <summary>
/// Xử lý duyệt cửa hàng mới (store approvals). Khi Admin duyệt một hồ sơ:
///   - Đánh dấu User (Owner) IsVerified = true
///   - Tự động tạo Branch mới với tên = StoreName và gán owner làm ManagerUser
///   - Tạo StaffAssignment gán owner → branch
///   - Gửi notification tới owner qua SignalR
/// </summary>
public class StoreApprovalService : IStoreApprovalService
{
    private readonly MEPDatabaseContext _context;
    private readonly MepNotificationService _notificationService;

    public StoreApprovalService(MEPDatabaseContext context, MepNotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<PageResultDto<StoreApprovalDto>> GetApprovalsAsync(string? status, string? search, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        pageSize = pageSize > 100 ? 100 : pageSize;

        var query = BuildFilteredQuery(status, search);

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var owners = await LoadOwnersAsync(rows.Select(a => a.UserId).Distinct().ToList());
        var reviewerNames = await LoadReviewerNamesAsync(rows);

        var items = rows.Select(a => BuildDto(a, owners, reviewerNames)).ToList();

        return new PageResultDto<StoreApprovalDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<StoreApprovalDto> GetApprovalByIdAsync(Guid id)
    {
        var approval = await _context.StoreApprovals.FirstOrDefaultAsync(a => a.ApprovalId == id);
        if (approval == null)
            throw new KeyNotFoundException("Ho so phe duyet khong ton tai.");

        var owner = await _context.Users.FirstOrDefaultAsync(u => u.UserId == approval.UserId);
        var owners = owner == null ? new Dictionary<Guid, User>() : new Dictionary<Guid, User> { [owner.UserId] = owner };
        var reviewerNames = await LoadReviewerNamesAsync(new List<StoreApproval> { approval });
        return BuildDto(approval, owners, reviewerNames);
    }

    public async Task<StoreApprovalDto> ApproveAsync(Guid id, ApproveStoreRequest dto, Guid adminId)
    {
        var approval = await _context.StoreApprovals.FirstOrDefaultAsync(a => a.ApprovalId == id);
        if (approval == null)
            throw new KeyNotFoundException("Ho so phe duyet khong ton tai.");

        if (approval.Status != "PENDING")
            throw new BusinessException("Ho so nay da duoc xu ly truoc do.");

        var owner = await _context.Users.FirstOrDefaultAsync(u => u.UserId == approval.UserId);
        if (owner == null)
            throw new BusinessException("Khong tim thay tai khoan Owner tuong ung.");

        // Một owner chỉ được gán cho một cửa hàng.
        var existingBranch = await _context.Branches
            .FirstOrDefaultAsync(b => b.ManagerUserId == approval.UserId);
        if (existingBranch != null)
            throw new BusinessException("Chu cua hang nay da duoc gan cho mot cua hang khac.");

        var now = DateTime.UtcNow;

        await using var transaction = await _context.Database.BeginTransactionAsync();

        approval.Status = "APPROVED";
        approval.Notes = dto.Notes?.Trim();
        approval.ReviewedBy = adminId;
        approval.ReviewedAt = now;

        owner.IsVerified = true;

        var branch = new Branch
        {
            BranchId = Guid.NewGuid(),
            BranchName = approval.StoreName.Trim(),
            BranchCode = null,
            Type = "Owner",
            ManagerUserId = owner.UserId,
            IsActive = 1,
            Address = approval.StoreAddress,
            CreatedAt = now,
            UpdatedAt = now
        };
        _context.Branches.Add(branch);

        _context.StaffAssignments.Add(new StaffAssignment
        {
            AssignmentId = Guid.NewGuid(),
            UserId = owner.UserId,
            BranchId = branch.BranchId,
            AssignedAt = now
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        await _notificationService.NotifyOwnerApprovalDecision(owner.UserId, "APPROVED",
            $"Cua hang '{approval.StoreName}' cua ban da duoc phe duyet thanh cong.");

        return BuildDto(approval,
            new Dictionary<Guid, User> { [owner.UserId] = owner },
            await LoadReviewerNamesAsync(new List<StoreApproval> { approval }));
    }

    public async Task<StoreApprovalDto> RejectAsync(Guid id, RejectStoreRequest dto, Guid adminId)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new BusinessException("Vui long nhap ly do tu choi.");

        var approval = await _context.StoreApprovals.FirstOrDefaultAsync(a => a.ApprovalId == id);
        if (approval == null)
            throw new KeyNotFoundException("Ho so phe duyet khong ton tai.");

        if (approval.Status != "PENDING")
            throw new BusinessException("Ho so nay da duoc xu ly truoc do.");

        var owner = await _context.Users.FirstOrDefaultAsync(u => u.UserId == approval.UserId);
        if (owner == null)
            throw new BusinessException("Khong tim thay tai khoan Owner tuong ung.");

        approval.Status = "REJECTED";
        approval.RejectReason = dto.Reason.Trim();
        approval.ReviewedBy = adminId;
        approval.ReviewedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _notificationService.NotifyOwnerApprovalDecision(owner.UserId, "REJECTED",
            $"Ho so cua hang '{approval.StoreName}' cua ban da bi tu choi. Ly do: {approval.RejectReason}");

        return BuildDto(approval,
            new Dictionary<Guid, User> { [owner.UserId] = owner },
            await LoadReviewerNamesAsync(new List<StoreApproval> { approval }));
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private IQueryable<StoreApproval> BuildFilteredQuery(string? status, string? search)
    {
        var query = _context.StoreApprovals.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status.Trim());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(a => a.StoreName.ToLower().Contains(s)
                || (a.TaxCode != null && a.TaxCode.ToLower().Contains(s))
                || (a.BusinessLicense != null && a.BusinessLicense.ToLower().Contains(s))
                || _context.Users.Any(u => u.UserId == a.UserId
                    && (u.Email.ToLower().Contains(s) || u.FullName.ToLower().Contains(s))));
        }

        return query;
    }

    private async Task<Dictionary<Guid, User>> LoadOwnersAsync(List<Guid> userIds)
    {
        var result = new Dictionary<Guid, User>();
        if (userIds.Count == 0)
            return result;

        var owners = await _context.Users
            .Where(u => userIds.Contains(u.UserId))
            .ToListAsync();

        foreach (var u in owners)
            if (!result.ContainsKey(u.UserId))
                result[u.UserId] = u;

        return result;
    }

    private async Task<Dictionary<Guid, string>> LoadReviewerNamesAsync(List<StoreApproval> approvals)
    {
        var result = new Dictionary<Guid, string>();
        var reviewerIds = approvals
            .Where(a => a.ReviewedBy.HasValue && a.ReviewedBy.Value != Guid.Empty)
            .Select(a => a.ReviewedBy!.Value)
            .Distinct()
            .ToList();

        if (reviewerIds.Count == 0)
            return result;

        var reviewers = await _context.Users
            .Where(u => reviewerIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FullName })
            .ToListAsync();

        foreach (var r in reviewers)
            if (!result.ContainsKey(r.UserId))
                result[r.UserId] = r.FullName;

        return result;
    }

    private StoreApprovalDto BuildDto(StoreApproval a, Dictionary<Guid, User> owners, Dictionary<Guid, string> reviewerNames)
    {
        owners.TryGetValue(a.UserId, out var owner);
        var reviewerName = a.ReviewedBy.HasValue && reviewerNames.TryGetValue(a.ReviewedBy.Value, out var rn)
            ? rn
            : null;

        return new StoreApprovalDto
        {
            ApprovalId = a.ApprovalId,
            UserId = a.UserId,
            StoreName = a.StoreName,
            BusinessLicense = a.BusinessLicense,
            TaxCode = a.TaxCode,
            StoreAddress = a.StoreAddress,
            DocumentUrls = a.DocumentUrls,
            Status = a.Status,
            RejectReason = a.RejectReason,
            ReviewedBy = a.ReviewedBy,
            ReviewedAt = a.ReviewedAt,
            CreatedAt = a.CreatedAt,
            OwnerEmail = owner?.Email ?? "",
            OwnerName = owner?.FullName ?? "",
            ReviewerName = reviewerName
        };
    }
}