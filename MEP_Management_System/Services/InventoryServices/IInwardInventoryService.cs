using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;

namespace MEP_Management_System.Services.InventoryServices;

public interface IInwardInventoryService
{
    Task<ApiResponse<PaginatedResponse<InwardInventoryResponseDto>>> GetInwardInventoriesAsync(
        ClaimsPrincipal user, Guid? branchId, string? status, string? ticketType,
        DateTime? fromDate, DateTime? toDate, int pageNumber, int pageSize);

    Task<ApiResponse<InwardInventoryResponseDto>> GetInwardInventoryByIdAsync(ClaimsPrincipal user, Guid id, Guid? branchId);

    Task<ApiResponse<object>> CreateInwardInventoryAsync(ClaimsPrincipal user, InwardInventoryCreateDto dto, Guid? branchId);

    Task<ApiResponse<InwardInventoryResponseDto>> UpdateInwardInventoryAsync(ClaimsPrincipal user, Guid id, InwardInventoryUpdateDto dto, Guid? branchId);

    Task<ApiResponse<object>> ConfirmInwardInventoryAsync(ClaimsPrincipal user, Guid id, Guid? branchId);

    Task<ApiResponse<object>> CancelInwardInventoryAsync(ClaimsPrincipal user, Guid id, CancelTicketDto dto, Guid? branchId);

    Task<string> GetPrintHtmlAsync(ClaimsPrincipal user, Guid id, Guid? branchId);

    Task<ApiResponse<List<InwardTicketForReturnDto>>> GetBySupplierAsync(ClaimsPrincipal user, Guid supplierId, Guid? branchId);

    Task<ApiResponse<List<ReturnableItemDto>>> GetReturnableItemsAsync(ClaimsPrincipal user, Guid id);
}
