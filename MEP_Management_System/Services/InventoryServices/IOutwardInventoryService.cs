using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;

namespace MEP_Management_System.Services.InventoryServices;

public interface IOutwardInventoryService
{
    Task<ApiResponse<PaginatedResponse<OutwardInventoryResponseDto>>> GetOutwardInventoriesAsync(
        ClaimsPrincipal user, Guid? branchId, string? status, string? ticketType,
        DateTime? fromDate, DateTime? toDate, int pageNumber, int pageSize);

    Task<ApiResponse<OutwardInventoryResponseDto>> GetOutwardInventoryByIdAsync(ClaimsPrincipal user, Guid id, Guid? branchId);

    Task<ApiResponse<object>> CreateOutwardInventoryAsync(ClaimsPrincipal user, OutwardInventoryCreateDto dto, Guid? branchIdOverride);

    Task<ApiResponse<OutwardInventoryResponseDto>> UpdateOutwardInventoryAsync(ClaimsPrincipal user, Guid id, OutwardInventoryUpdateDto dto, Guid? branchId);

    Task<ApiResponse<object>> ConfirmOutwardInventoryAsync(ClaimsPrincipal user, Guid id, Guid? branchId);

    Task<ApiResponse<object>> CancelOutwardInventoryAsync(ClaimsPrincipal user, Guid id, CancelTicketDto dto, Guid? branchId);

    Task<string> GetPrintHtmlAsync(ClaimsPrincipal user, Guid id, Guid? branchId);
}
