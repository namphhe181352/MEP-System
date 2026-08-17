using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;

namespace MEP_Management_System.Services.AdminDashboardServices;

/// <summary>
/// Service interface for Admin dashboard operations.
/// </summary>
public interface IAdminDashboardService
{
    Task<DashboardOverviewDto> GetOverviewAsync();
    Task<List<RevenueChartPointDto>> GetRevenueChartAsync(int year);
    Task<List<SystemEventDto>> GetRecentEventsAsync(int limit);
    Task<(byte[] Content, string FileName, string ContentType)> ExportDataAsync(string format);
}
