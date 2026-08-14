using System;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

public class OrderDiscountService : IOrderDiscountService
{
    private readonly MEPDatabaseContext _context;

    public OrderDiscountService(MEPDatabaseContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Tính chiết khấu dựa trên tổng giá trị đơn hàng.
    /// Logic: Lấy bậc cao nhất mà subTotal đạt được.
    /// </summary>
    public async Task<(decimal? DiscountPercent, decimal DiscountAmount, decimal FinalTotal)> CalculateDiscountAsync(decimal subTotal, Guid branchId)
    {
        // T12: Validate discount percent in [0, 100] before applying
        if (subTotal <= 0)
        {
            return (null, 0, subTotal);
        }

        // Lấy các bậc chiết khấu đang active và có ngưỡng <= subTotal
        var tiers = await _context.OrderDiscountTiers
            .Where(t => t.IsActive && t.MinOrderValue <= subTotal && t.BranchId == branchId)
            .OrderByDescending(t => t.MinOrderValue)
            .ToListAsync();

        // Nếu không có bậc nào đạt -> không giảm giá
        if (!tiers.Any())
        {
            return (null, 0, subTotal);
        }

        // Lấy bậc cao nhất (ngưỡng lớn nhất mà subTotal đạt được)
        var applicableTier = tiers.First();

        // T12: Validate discount percent in [0, 100]
        if (applicableTier.DiscountPercent < 0 || applicableTier.DiscountPercent > 100)
        {
            return (null, 0, subTotal);
        }

        // Tính số tiền giảm = subTotal * discountPercent / 100
        // Làm tròn xuống hàng nghìn VNĐ
        var discountAmount = Math.Floor(subTotal * applicableTier.DiscountPercent / 100 / 1000) * 1000;

        // Tính tổng tiền sau chiết khấu
        var finalTotal = subTotal - discountAmount;

        return (applicableTier.DiscountPercent, discountAmount, finalTotal);
    }
}
