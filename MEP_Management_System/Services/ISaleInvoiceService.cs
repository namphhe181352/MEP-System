using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.SaleDTOs;
using Microsoft.AspNetCore.Mvc;
using CategoryPolicyDto = MEP_Management_System.DTOs.InventoryDTOs.CategoryPolicyDto;

namespace MEP_Management_System.Services;

public interface ISaleInvoiceService
{
    Task<ActionResult<PageResultDto<InvoiceDto>>> GetInvoicesAsync(
        Guid branchId, string? status, DateTime? dateFrom, DateTime? dateTo,
        Guid? customerId, Guid? staffId, int page, int pageSize);

    Task<ActionResult<InvoiceDto>> CreateInvoiceAsync(Guid branchId, Guid userId, CreateInvoiceRequest request);

    Task<ActionResult<InvoiceDto>> GetInvoiceAsync(ClaimsPrincipal user, Guid invoiceId);

    Task<ActionResult<InvoiceItemDto>> AddItemAsync(Guid branchId, Guid invoiceId, AddItemRequest request);

    Task<ActionResult<InvoiceItemDto>> ScanItemAsync(Guid branchId, Guid invoiceId, ScanItemRequest request);

    Task<ActionResult<List<ProductPriceDto>>> SearchProductAsync(Guid branchId, SearchProductRequest request);

    Task<ActionResult> CancelInvoiceAsync(Guid branchId, Guid userId, Guid invoiceId);

    Task<ActionResult<InvoiceDto>> FinalizeInvoiceAsync(Guid branchId, Guid invoiceId);

    Task<ActionResult<List<InvoiceItemDto>>> GetItemsAsync(Guid branchId, Guid invoiceId);

    Task<ActionResult<InvoiceDto>> HoldInvoiceAsync(Guid branchId, Guid invoiceId, HoldInvoiceRequest request);

    Task<ActionResult<InvoiceDto>> ResumeInvoiceAsync(Guid branchId, Guid invoiceId);

    Task<ActionResult<PageResultDto<InvoiceDto>>> GetOnHoldInvoicesAsync(Guid branchId, int page, int pageSize);

    Task<ActionResult<List<CategoryPolicyDto>>> GetReturnPoliciesAsync(Guid branchId);
}