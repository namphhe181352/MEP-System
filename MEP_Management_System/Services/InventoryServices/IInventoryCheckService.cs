using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using CancelTicketDto = MEP_Management_System.DTOs.InventoryDTOs.CancelInventoryCheckDto;

namespace MEP_Management_System.Services.InventoryServices;

public interface IInventoryCheckService
{
    Task<ApiResponse<PaginatedResponse<InventoryCheckResponseDto>>> GetInventoryChecksAsync(
        ClaimsPrincipal user, Guid? branchId, string? ticketCode, string? status, DateTime? fromDate, DateTime? toDate,
        int pageNumber, int pageSize);

    Task<ApiResponse<InventoryCheckResponseDto>> GetInventoryCheckByIdAsync(ClaimsPrincipal user, Guid id, Guid? branchId);

    Task<ApiResponse<object>> CreateInventoryCheckAsync(ClaimsPrincipal user, InventoryCheckCreateDto dto, Guid? branchIdOverride);

    Task<ApiResponse<InventoryCheckResponseDto>> UpdateInventoryCheckAsync(ClaimsPrincipal user, Guid id, InventoryCheckUpdateDto dto, Guid? branchId);

    Task<ApiResponse<InventoryCheckResponseDto>> FillActualQuantitiesAsync(ClaimsPrincipal user, Guid id, FillActualQuantitiesDto dto, Guid? branchId);

    Task<ApiResponse<object>> ApproveInventoryCheckAsync(ClaimsPrincipal user, Guid id, Guid? branchId);

    Task<ApiResponse<object>> RejectOrRequestRecountAsync(ClaimsPrincipal user, Guid id, RejectOrRecountDto dto, Guid? branchId);

    Task<ApiResponse<object>> CancelInventoryCheckAsync(ClaimsPrincipal user, Guid id, CancelInventoryCheckDto dto, Guid? branchId);

    Task<ApiResponse<InventoryCheckResponseDto>> UpdateDiscrepancyReasonsAsync(ClaimsPrincipal user, Guid id, UpdateDiscrepancyReasonsDto dto, Guid? branchId);

    Task<string> GetPrintHtmlAsync(ClaimsPrincipal user, Guid id, Guid? branchId);

    Task<ApiResponse<object>> GetCountersAsync(ClaimsPrincipal user, Guid? branchId);

    Task<ApiResponse<object>> GetAssigneesAsync(ClaimsPrincipal user, Guid? branchId);

    Task<ApiResponse<object>> GetNotificationsAsync(ClaimsPrincipal user, Guid? branchId);

    Task<ApiResponse<object>> MarkNotificationsAsReadAsync(ClaimsPrincipal user, List<Guid>? notificationIds, Guid? branchId);
}
