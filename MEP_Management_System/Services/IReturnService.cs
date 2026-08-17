using System.Collections.Generic;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;

namespace MEP_Management_System.Services;

public interface IReturnService
{
    Task<PageResultDto<ReturnOrderDto>> GetReturnsAsync(Guid branchId, Guid? invoiceId, string? status, DateTime? dateFrom, DateTime? dateTo, int page, int pageSize);
    Task<ReturnOrderDto?> GetReturnAsync(Guid id, Guid branchId);
    Task<ReturnOrderDto> CreateReturnAsync(CreateReturnOrderRequest req, Guid userId, Guid branchId);
    Task<ReturnItemDto> AddReturnItemAsync(Guid id, AddReturnItemRequest req, Guid branchId);
    Task<ReturnOrderDto> FinalizeReturnAsync(Guid id, FinalizeReturnRequest? req, Guid userId, Guid branchId);
    Task<bool> CancelReturnAsync(Guid id, Guid userId, Guid branchId);
    Task<ExchangeQuoteResponse> QuoteExchangeAsync(ExchangeQuoteRequest req, Guid branchId);
    Task<ReturnOrderDto> CreateExchangeAsync(CreateExchangeRequest req, Guid userId, Guid branchId);
    Task<PayExchangeResultDto> PayExchangeDiffAsync(Guid id, PayExchangeRequest? req, Guid userId, Guid branchId);
    Task<ReturnOrderDto> ConfirmExchangeTransferAsync(Guid id, ConfirmTransferRequest? req, Guid userId, Guid branchId);
    Task<bool> CancelExchangePaymentAsync(Guid id, Guid branchId);
    Task<List<CategoryPolicyDto>> GetReturnPoliciesAsync(Guid branchId);
    Task<List<CategoryPolicyDto>> GetCategoryPoliciesAsync(Guid branchId);
    Task<object> GetReturnDiscountAsync(Guid branchId);
    Task<InvoiceEligibilityResponse> GetEligibleItemsAsync(Guid invoiceId, Guid branchId);
    Task<PageResultDto<DefectiveItemDto>> GetDefectiveItemsAsync(Guid branchId, int page, int pageSize, string? search, DateTime? fromDate, DateTime? toDate);
}
