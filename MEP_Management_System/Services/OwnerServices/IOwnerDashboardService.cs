using MEP_Management_System.DTOs;

namespace MEP_Management_System.Services.OwnerServices;

public interface IOwnerDashboardService
{
    Task<DashboardOverviewDto> GetOverviewAsync(Guid ownerUserId, Guid branchId);
    Task<object> GetCustomerDebtListAsync(Guid branchId, bool? isOverdue, int page, int pageSize, string? search);
    Task<CustomerDebtOverviewDto> GetCustomerDebtOverviewAsync(Guid branchId);
    Task<object> GetTopProductsAsync(Guid branchId, DateTime? from, DateTime? to, int limit, string sortBy);
    Task<object> DebugPaymentsAsync(Guid branchId, int days);
}
