using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;

namespace MEP_Management_System.Services;

/// <summary>
/// Interface for report generation services.
/// </summary>
public interface IReportService
{
    Task<DailyEndReportDto> GetDailyEndReportAsync(DailyEndReportRequestDto request, ClaimsPrincipal currentUser);

    Task<StockMovementReportDto> GetStockMovementFromLedgerAsync(StockMovementReportRequestDto request, ClaimsPrincipal currentUser);

    Task<RevenueByTimeReportDto> GetRevenueByTimeReportAsync(RevenueByTimeRequestDto request, ClaimsPrincipal currentUser);

    Task<LowStockReportDto> GetLowStockReportAsync(LowStockReportRequestDto request, ClaimsPrincipal currentUser);

    Task<ProductProfitReportDto> GetProductProfitReportAsync(ProductProfitReportRequestDto request, ClaimsPrincipal currentUser);

    Task<SupplierDetailReportDto> GetSupplierDetailReportAsync(SupplierDetailRequestDto request, ClaimsPrincipal currentUser);

    Task<StockLedgerDto> GetStockLedgerAsync(Guid branchProductId, StockLedgerRequestDto request, ClaimsPrincipal currentUser);

    Task<object> GetStockLedgerDetailAsync(Guid branchProductId, DateTime? fromDate, DateTime? toDate, ClaimsPrincipal currentUser);

    Task<object> CreateDailySnapshotAsync(Guid? branchIdOverride, ClaimsPrincipal currentUser);
}
