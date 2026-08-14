using System;
using System.Threading.Tasks;

namespace MEP_Management_System.Services;

public interface IOrderDiscountService
{
    /// <summary>
    /// Tính chiết khấu dựa trên tổng giá trị đơn hàng.
    /// </summary>
    /// <param name="subTotal">Tổng tiền trước chiết khấu.</param>
    Task<(decimal? DiscountPercent, decimal DiscountAmount, decimal FinalTotal)> CalculateDiscountAsync(decimal subTotal, Guid branchId);
}
