using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

public interface IBranchResolutionService
{
    Task<Guid> ResolveBranchIdAsync(Guid? requestedBranchId, Guid userId, bool isOwner, Guid? currentBranchId);
}

public class BranchResolutionService : IBranchResolutionService
{
    private readonly MEPDatabaseContext _context;

    public BranchResolutionService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<Guid> ResolveBranchIdAsync(Guid? requestedBranchId, Guid userId, bool isOwner, Guid? currentBranchId)
    {
        // Ưu tiên 1: requestedBranchId từ request (chỉ Owner mới truyền được)
        if (requestedBranchId.HasValue && requestedBranchId != Guid.Empty)
        {
            if (!isOwner)
            {
                // Staff không được phép truyền branchId khác
                if (currentBranchId.HasValue)
                {
                    return currentBranchId.Value;
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
        if (currentBranchId.HasValue)
        {
            return currentBranchId.Value;
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
