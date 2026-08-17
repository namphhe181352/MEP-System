using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.OwnerServices;

public class OwnerOrderDiscountTierService : IOwnerOrderDiscountTierService
{
    private readonly MEPDatabaseContext _context;

    public OwnerOrderDiscountTierService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<OrderDiscountTierDto> CreateAsync(Guid? branchId, CreateOrderDiscountTierDto request)
    {
        var resolvedBranchId = branchId;

        if (request.MinOrderValue <= 0)
            throw new BusinessException("MSG-02", "Ngưỡng giá trị phải lớn hơn 0");

        if (request.DiscountPercent < 0.01m || request.DiscountPercent > 100)
            throw new BusinessException("MSG-02", "Phần trăm chiết khấu phải trong khoảng 0.01 - 100");

        var existingTier = await _context.OrderDiscountTiers
            .FirstOrDefaultAsync(t => t.MinOrderValue == request.MinOrderValue
                && t.BranchId == resolvedBranchId);

        if (existingTier != null)
            throw new BusinessException("MSG-03", $"Ngưỡng giá trị {request.MinOrderValue:N0} đã tồn tại");

        var rogueGlobalTier = await _context.OrderDiscountTiers
            .FirstOrDefaultAsync(t => t.MinOrderValue == request.MinOrderValue && t.BranchId == null);

        if (rogueGlobalTier != null)
        {
            rogueGlobalTier.BranchId = resolvedBranchId;
            rogueGlobalTier.DiscountPercent = request.DiscountPercent;
            rogueGlobalTier.IsActive = true;
            rogueGlobalTier.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _context.Entry(rogueGlobalTier).Reference(rt => rt.Branch).LoadAsync();

            return new OrderDiscountTierDto
            {
                OrderDiscountTierId = rogueGlobalTier.OrderDiscountTierId,
                BranchId = rogueGlobalTier.BranchId,
                BranchName = rogueGlobalTier.Branch?.BranchName,
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
            BranchId = resolvedBranchId,
            MinOrderValue = request.MinOrderValue,
            DiscountPercent = request.DiscountPercent,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.OrderDiscountTiers.Add(tier);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("23505") == true
            || ex.InnerException?.Message?.Contains("duplicate key") == true
            || ex.InnerException?.Message?.Contains("unique constraint") == true)
        {
            throw new BusinessException("MSG-03", $"Ngưỡng giá trị {request.MinOrderValue:N0} đã tồn tại");
        }

        await _context.Entry(tier).Reference(t => t.Branch).LoadAsync();

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

    public async Task<OrderDiscountTierDto> UpdateAsync(Guid id, Guid? branchId, UpdateOrderDiscountTierDto request)
    {
        var resolvedBranchId = branchId;

        var tier = await _context.OrderDiscountTiers
            .Include(t => t.Branch)
            .FirstOrDefaultAsync(t => t.OrderDiscountTierId == id);

        if (tier == null)
            throw new KeyNotFoundException("Không tìm thấy bậc chiết khấu");

        if (request.MinOrderValue <= 0)
            throw new BusinessException("MSG-02", "Ngưỡng giá trị phải lớn hơn 0");

        if (request.DiscountPercent < 0.01m || request.DiscountPercent > 100)
            throw new BusinessException("MSG-02", "Phần trăm chiết khấu phải trong khoảng 0.01 - 100");

        var duplicateTier = await _context.OrderDiscountTiers
            .AnyAsync(t => t.MinOrderValue == request.MinOrderValue
                && t.BranchId == resolvedBranchId
                && t.OrderDiscountTierId != id);

        if (duplicateTier)
            throw new BusinessException("MSG-03", $"Ngưỡng giá trị {request.MinOrderValue:N0} đã tồn tại");

        tier.BranchId = resolvedBranchId;
        tier.MinOrderValue = request.MinOrderValue;
        tier.DiscountPercent = request.DiscountPercent;
        tier.IsActive = request.IsActive;
        tier.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("23505") == true
            || ex.InnerException?.Message?.Contains("duplicate key") == true
            || ex.InnerException?.Message?.Contains("unique constraint") == true)
        {
            throw new BusinessException("MSG-03", $"Ngưỡng giá trị {request.MinOrderValue:N0} đã tồn tại");
        }

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

    public async Task DeleteAsync(Guid id)
    {
        var tier = await _context.OrderDiscountTiers.FindAsync(id);

        if (tier == null)
            throw new KeyNotFoundException("Không tìm thấy bậc chiết khấu");

        _context.OrderDiscountTiers.Remove(tier);
        await _context.SaveChangesAsync();
    }
}
