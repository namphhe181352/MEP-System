using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using CategoryPolicyDto = MEP_Management_System.DTOs.InventoryDTOs.CategoryPolicyDto;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/invoices")]
[Authorize]
[Tags("Quản lý bán hàng (Sale)")]
public class InvoicesController : ControllerBase
{
    private readonly ISaleInvoiceService _saleInvoiceService;

    public InvoicesController(ISaleInvoiceService saleInvoiceService)
    {
        _saleInvoiceService = saleInvoiceService;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found in token"));

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? throw new Exception("userId not found"));

    // GET /api/pos/invoices
    [HttpGet]
    public async Task<ActionResult<PageResultDto<InvoiceDto>>> GetInvoices(
        [FromQuery] string? status, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? customerId, [FromQuery] Guid? staffId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return await _saleInvoiceService.GetInvoicesAsync(GetBranchId(), status, dateFrom, dateTo, customerId, staffId, page, pageSize);
    }

    // POST /api/pos/invoices
    [HttpPost]
    [HasPermission(PermissionConstants.SALE_CREATE)]
    public async Task<ActionResult<InvoiceDto>> CreateInvoice([FromBody] CreateInvoiceRequest request)
    {
        return await _saleInvoiceService.CreateInvoiceAsync(GetBranchId(), GetUserId(), request);
    }

    // GET /api/pos/invoices/{invoiceId}
    [HttpGet("{invoiceId:guid}")]
    public async Task<ActionResult<InvoiceDto>> GetInvoice(Guid invoiceId)
    {
        return await _saleInvoiceService.GetInvoiceAsync(User, invoiceId);
    }

    // POST /api/pos/invoices/{invoiceId}/items
    [HttpPost("{invoiceId:guid}/items")]
    [HasPermission(PermissionConstants.SALE_CREATE)]
    public async Task<ActionResult<InvoiceItemDto>> AddItem(Guid invoiceId, [FromBody] AddItemRequest request)
    {
        return await _saleInvoiceService.AddItemAsync(GetBranchId(), invoiceId, request);
    }

    // POST /api/pos/invoices/{invoiceId}/items/scan
    [HttpPost("{invoiceId:guid}/items/scan")]
    [HasPermission(PermissionConstants.SALE_CREATE)]
    public async Task<ActionResult<InvoiceItemDto>> ScanItem(Guid invoiceId, [FromBody] ScanItemRequest request)
    {
        return await _saleInvoiceService.ScanItemAsync(GetBranchId(), invoiceId, request);
    }

    // POST /api/pos/invoices/{invoiceId}/items/search
    [HttpPost("{invoiceId:guid}/items/search")]
    public async Task<ActionResult<List<ProductPriceDto>>> SearchProduct([FromBody] SearchProductRequest request)
    {
        return await _saleInvoiceService.SearchProductAsync(GetBranchId(), request);
    }

    // PATCH /api/pos/invoices/{invoiceId}/cancel
    [HttpPatch("{invoiceId:guid}/cancel")]
    [HasPermission(PermissionConstants.SALE_DELETE)]
    public async Task<ActionResult> CancelInvoice(Guid invoiceId)
    {
        return await _saleInvoiceService.CancelInvoiceAsync(GetBranchId(), GetUserId(), invoiceId);
    }

    // POST /api/pos/invoices/{invoiceId}/finalize
    [HttpPost("{invoiceId:guid}/finalize")]
    [HasPermission(PermissionConstants.SALE_CREATE)]
    public async Task<ActionResult<InvoiceDto>> FinalizeInvoice(Guid invoiceId)
    {
        return await _saleInvoiceService.FinalizeInvoiceAsync(GetBranchId(), invoiceId);
    }

    // GET /api/pos/invoices/{invoiceId}/items
    [HttpGet("{invoiceId:guid}/items")]
    public async Task<ActionResult<List<InvoiceItemDto>>> GetItems(Guid invoiceId)
    {
        return await _saleInvoiceService.GetItemsAsync(GetBranchId(), invoiceId);
    }

    // POST /api/pos/invoices/{invoiceId}/hold
    /// <summary>Treo hóa đơn (Hold Invoice) - lưu trạng thái giỏ hàng hiện tại.</summary>
    [HttpPost("{invoiceId:guid}/hold")]
    [HasPermission(PermissionConstants.SALE_CREATE)]
    public async Task<ActionResult<InvoiceDto>> HoldInvoice(Guid invoiceId, [FromBody] HoldInvoiceRequest request)
    {
        return await _saleInvoiceService.HoldInvoiceAsync(GetBranchId(), invoiceId, request);
    }

    // POST /api/pos/invoices/{invoiceId}/resume
    /// <summary>Khôi phục hóa đơn đang treo (Resume Invoice) - chuyển về Draft.</summary>
    [HttpPost("{invoiceId:guid}/resume")]
    [HasPermission(PermissionConstants.SALE_CREATE)]
    public async Task<ActionResult<InvoiceDto>> ResumeInvoice(Guid invoiceId)
    {
        return await _saleInvoiceService.ResumeInvoiceAsync(GetBranchId(), invoiceId);
    }

    // GET /api/pos/invoices/on-hold
    /// <summary>Lấy danh sách hóa đơn đang treo (chưa hết hạn 7 ngày).</summary>
    [HttpGet("on-hold")]
    public async Task<ActionResult<PageResultDto<InvoiceDto>>> GetOnHoldInvoices(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return await _saleInvoiceService.GetOnHoldInvoicesAsync(GetBranchId(), page, pageSize);
    }

    // GET /api/pos/invoices/return-policies
    /// <summary>
    /// Trả về danh sách chính sách đổi/trả theo nhóm hàng của chi nhánh hiện tại.
    /// Endpoint dự phòng khi /api/pos/returns/return-policies không khả dụng.
    /// </summary>
    [HttpGet("return-policies")]
    public async Task<ActionResult<List<CategoryPolicyDto>>> GetReturnPolicies()
    {
        return await _saleInvoiceService.GetReturnPoliciesAsync(GetBranchId());
    }
}