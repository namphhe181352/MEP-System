using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Services.InventoryServices;

namespace MEP_Management_System.Services
{
    /// <summary>
    /// Phiếu nhập kho legacy — delegate toàn bộ logic sang IInwardInventoryService
    /// (tạo PENDING / cộng kho & ghi ledger khi confirm).
    /// </summary>
    public class StockImportService : IStockImportService
    {
        private readonly IInwardInventoryService _inwardInventoryService;

        public StockImportService(IInwardInventoryService inwardInventoryService)
        {
            _inwardInventoryService = inwardInventoryService;
        }

        public async Task<ApiResponse<PaginatedResponse<StockImportResponseDto>>> GetStockImportsAsync(
            ClaimsPrincipal user, Guid? branchId, string? status, string? ticketType,
            DateTime? fromDate, DateTime? toDate, int pageNumber, int pageSize)
        {
            var result = await _inwardInventoryService.GetInwardInventoriesAsync(
                user, branchId, status, ticketType, fromDate, toDate, pageNumber, pageSize);

            if (result.Data == null)
                return new ApiResponse<PaginatedResponse<StockImportResponseDto>>
                {
                    Success = result.Success,
                    Message = result.Message,
                    Data = new PaginatedResponse<StockImportResponseDto>(),
                    Errors = result.Errors
                };

            return new ApiResponse<PaginatedResponse<StockImportResponseDto>>
            {
                Success = result.Success,
                Message = result.Message,
                Data = new PaginatedResponse<StockImportResponseDto>
                {
                    Items = result.Data.Items.Select(MapResponse).ToList(),
                    TotalCount = result.Data.TotalCount,
                    PageNumber = result.Data.PageNumber,
                    PageSize = result.Data.PageSize,
                    TotalPages = result.Data.TotalPages,
                    HasNextPage = result.Data.HasNextPage,
                    HasPreviousPage = result.Data.HasPreviousPage
                },
                Errors = result.Errors
            };
        }

        public async Task<ApiResponse<StockImportResponseDto>> GetStockImportByIdAsync(ClaimsPrincipal user, Guid id, Guid? branchId)
        {
            var result = await _inwardInventoryService.GetInwardInventoryByIdAsync(user, id, branchId);
            return new ApiResponse<StockImportResponseDto>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data == null ? null : MapResponse(result.Data),
                Errors = result.Errors
            };
        }

        public async Task<ApiResponse<object>> CreateStockImportAsync(ClaimsPrincipal user, StockImportCreateDto dto, Guid? branchId)
        {
            var inwardDto = new InwardInventoryCreateDto
            {
                InwardType = dto.InwardType,
                SupplierId = dto.SupplierId,
                Reason = dto.Reason,
                Note = dto.Note,
                Items = dto.Items.Select(i => new InwardInventoryItemDto
                {
                    Id = i.Id,
                    ProductCode = i.ProductCode,
                    ProductName = i.ProductName,
                    Unit = i.Unit,
                    Quantity = i.Quantity,
                    CostPrice = i.CostPrice,
                    Note = i.Note
                }).ToList()
            };

            return await _inwardInventoryService.CreateInwardInventoryAsync(user, inwardDto, branchId);
        }

        private static StockImportResponseDto MapResponse(InwardInventoryResponseDto s)
        {
            return new StockImportResponseDto
            {
                StockTicketId = s.StockTicketId,
                TicketCode = s.TicketCode,
                TicketType = s.TicketType,
                Status = s.Status,
                BranchId = s.BranchId,
                UserId = s.UserId,
                UserName = s.UserName,
                Reason = s.Reason,
                Note = s.Note,
                CostPrice = s.CostPrice,
                SupplierId = s.SupplierId,
                CreatedAt = s.CreatedAt,
                CompletedAt = s.CompletedAt,
                ConfirmedBy = s.ConfirmedBy,
                CancelledAt = s.CancelledAt,
                CancelledBy = s.CancelledBy,
                CancelReason = s.CancelReason,
                Items = s.Items?.Select(i => new StockImportItemResponseDto
                {
                    TicketItemId = i.TicketItemId,
                    BranchProductId = i.BranchProductId,
                    ProductId = i.ProductId,
                    ProductCode = i.ProductCode,
                    ProductName = i.ProductName,
                    SystemQuantity = i.SystemQuantity,
                    Quantity = i.Quantity,
                    ActualQuantity = i.ActualQuantity,
                    UnitPrice = i.UnitPrice,
                    UnitName = i.UnitName
                }).ToList() ?? new List<StockImportItemResponseDto>()
            };
        }
    }
}