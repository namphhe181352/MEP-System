using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.SaleServices;

public class OrderDiscountTierService : IOrderDiscountTierService
{
    private readonly MEPDatabaseContext _context;

    public OrderDiscountTierService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<List<OrderDiscountTierDto>> GetAllAsync(Guid? branchId)
    {
        var query = _context.OrderDiscountTiers.AsQueryable();

        if (branchId != null && branchId != Guid.Empty)
        {
            query = query.Where(t => t.BranchId == branchId);
        }
        else
        {
            query = query.Where(t => false);
        }

        var tiers = await query.OrderBy(t => t.MinOrderValue).ToListAsync();

        var branchIds = tiers.Where(t => t.BranchId.HasValue).Select(t => t.BranchId!.Value).Distinct().ToList();
        var branches = await _context.Branches
            .Where(b => branchIds.Contains(b.BranchId))
            .GroupBy(b => b.BranchId)
            .Select(g => g.First())
            .ToDictionaryAsync(b => b.BranchId, b => b.BranchName);

        return tiers.Select(t => new OrderDiscountTierDto
        {
            OrderDiscountTierId = t.OrderDiscountTierId,
            BranchId = t.BranchId,
            BranchName = t.BranchId.HasValue && branches.ContainsKey(t.BranchId.Value) ? branches[t.BranchId.Value] : null,
            MinOrderValue = t.MinOrderValue,
            DiscountPercent = t.DiscountPercent,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();
    }

    public async Task<OrderDiscountTierDto?> GetByIdAsync(Guid id)
    {
        var tier = await _context.OrderDiscountTiers.Include(t => t.Branch).FirstOrDefaultAsync(t => t.OrderDiscountTierId == id);
        if (tier == null) return null;

        return new OrderDiscountTierDto
        {
            OrderDiscountTierId = tier.OrderDiscountTierId,
            BranchId = tier.BranchId,
            BranchName = tier.Branch != null ? tier.Branch.BranchName : null,
            MinOrderValue = tier.MinOrderValue,
            DiscountPercent = tier.DiscountPercent,
            IsActive = tier.IsActive,
            CreatedAt = tier.CreatedAt,
            UpdatedAt = tier.UpdatedAt
        };
    }

    public async Task<OrderDiscountTierDto> CreateAsync(CreateOrderDiscountTierRequest req, Guid? branchId)
    {
        if (req.MinOrderValue < 0)
            throw new BusinessException("MinOrderValue phải lớn hơn hoặc bằng 0");

        if (req.DiscountPercent < 0 || req.DiscountPercent > 100)
            throw new BusinessException("DiscountPercent phải nằm trong khoảng 0 - 100");

        if (branchId != null && branchId != Guid.Empty)
        {
            var branchExists = await _context.Branches.AnyAsync(b => b.BranchId == branchId);
            if (!branchExists)
                throw new BusinessException("Không tìm thấy chi nhánh");
        }

        var exists = await _context.OrderDiscountTiers
            .AnyAsync(t => t.BranchId == branchId && t.MinOrderValue == req.MinOrderValue);
        if (exists)
            throw new BusinessException("MinOrderValue đã tồn tại trong chi nhánh này");

        var rogueGlobalTier = await _context.OrderDiscountTiers
            .FirstOrDefaultAsync(t => t.MinOrderValue == req.MinOrderValue && t.BranchId == null);

        if (rogueGlobalTier != null)
        {
            rogueGlobalTier.BranchId = branchId;
            rogueGlobalTier.DiscountPercent = req.DiscountPercent;
            rogueGlobalTier.IsActive = req.IsActive;
            rogueGlobalTier.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new OrderDiscountTierDto
            {
                OrderDiscountTierId = rogueGlobalTier.OrderDiscountTierId,
                BranchId = rogueGlobalTier.BranchId,
                MinOrderValue = rogueGlobalTier.MinOrderValue,
                DiscountPercent = rogueGlobalTier.DiscountPercent,
                IsActive = rogueGlobalTier.IsActive,
                CreatedAt = rogueGlobalTier.CreatedAt,
                UpdatedAt = rogueGlobalTier.UpdatedAt
            };
        }

        var tier = new OrderDiscountTier
        {
            OrderDiscountTierId = Guid.NewGuid(),
            BranchId = branchId,
            MinOrderValue = req.MinOrderValue,
            DiscountPercent = req.DiscountPercent,
            IsActive = req.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.OrderDiscountTiers.Add(tier);
        await _context.SaveChangesAsync();

        return new OrderDiscountTierDto
        {
            OrderDiscountTierId = tier.OrderDiscountTierId,
            BranchId = tier.BranchId,
            MinOrderValue = tier.MinOrderValue,
            DiscountPercent = tier.DiscountPercent,
            IsActive = tier.IsActive,
            CreatedAt = tier.CreatedAt,
            UpdatedAt = tier.UpdatedAt
        };
    }

    public async Task<OrderDiscountTierDto?> UpdateAsync(Guid id, CreateOrderDiscountTierRequest req, Guid? branchId)
    {
        var tier = await _context.OrderDiscountTiers.FindAsync(id);
        if (tier == null) return null;

        if (req.MinOrderValue < 0)
            throw new BusinessException("MinOrderValue phải lớn hơn hoặc bằng 0");

        if (req.DiscountPercent < 0 || req.DiscountPercent > 100)
            throw new BusinessException("DiscountPercent phải nằm trong khoảng 0 - 100");

        if (branchId != null && branchId != Guid.Empty)
        {
            var branchExists = await _context.Branches.AnyAsync(b => b.BranchId == branchId);
            if (!branchExists)
                throw new BusinessException("Không tìm thấy chi nhánh");
        }

        var duplicate = await _context.OrderDiscountTiers
            .AnyAsync(t => t.OrderDiscountTierId != id && t.BranchId == branchId && t.MinOrderValue == req.MinOrderValue);
        if (duplicate)
            throw new BusinessException("MinOrderValue đã tồn tại trong chi nhánh này");

        tier.BranchId = branchId;
        tier.MinOrderValue = req.MinOrderValue;
        tier.DiscountPercent = req.DiscountPercent;
        tier.IsActive = req.IsActive;
        tier.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new OrderDiscountTierDto
        {
            OrderDiscountTierId = tier.OrderDiscountTierId,
            BranchId = tier.BranchId,
            MinOrderValue = tier.MinOrderValue,
            DiscountPercent = tier.DiscountPercent,
            IsActive = tier.IsActive,
            CreatedAt = tier.CreatedAt,
            UpdatedAt = tier.UpdatedAt
        };
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var tier = await _context.OrderDiscountTiers.FindAsync(id);
        if (tier == null)
            throw new KeyNotFoundException("Không tìm thấy bậc chiết khấu");

        _context.OrderDiscountTiers.Remove(tier);
        await _context.SaveChangesAsync();
        return true;
    }
}
