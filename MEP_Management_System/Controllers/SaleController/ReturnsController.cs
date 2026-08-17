using System;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/returns")]
[Authorize]
[Tags("Đổi trả hàng (Return Order)")]
public class ReturnsController : ControllerBase
{
    private readonly IReturnService _service;

    private static readonly string BankAccountNumber = "0975849675";
    private static readonly string BankName = "MB Bank";

    public ReturnsController(IReturnService service)
    {
        _service = service;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found in token"));

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(userIdStr ?? throw new Exception("userId not found in token"));
    }

    private ActionResult ToBusinessError(BusinessException b) =>
        BadRequest(ApiErrorResponse.Create(b.Message, b.Errors?.FirstOrDefault() ?? b.Message));

    private ActionResult ToNotFound(KeyNotFoundException nf) =>
        NotFound(ApiErrorResponse.Create("MSG-01", nf.Message));

    // GET /api/pos/returns
    [HttpGet]
    public async Task<ActionResult<PageResultDto<ReturnOrderDto>>> GetReturns(
        [FromQuery] Guid? invoiceId,
        [FromQuery] string? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var branchId = GetBranchId();
        var result = await _service.GetReturnsAsync(branchId, invoiceId, status, dateFrom, dateTo, page, pageSize);
        return Ok(result);
    }

    // GET /api/pos/returns/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReturnOrderDto>> GetReturn(Guid id)
    {
        var branchId = GetBranchId();
        var dto = await _service.GetReturnAsync(id, branchId);
        if (dto == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy phiếu đổi trả."));
        return Ok(dto);
    }

    // POST /api/pos/returns
    [HttpPost]
    public async Task<ActionResult<ReturnOrderDto>> CreateReturn([FromBody] CreateReturnOrderRequest req)
    {
        try
        {
            var dto = await _service.CreateReturnAsync(req, GetUserId(), GetBranchId());
            return CreatedAtAction(nameof(GetReturn), new { id = dto.ReturnOrderId }, dto);
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo phiếu trả hàng" });
        }
    }

    // POST /api/pos/returns/{id}/items
    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<ReturnItemDto>> AddReturnItem(Guid id, [FromBody] AddReturnItemRequest req)
    {
        try
        {
            var dto = await _service.AddReturnItemAsync(id, req, GetBranchId());
            return Ok(dto);
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
    }

    // POST /api/pos/returns/{id}/finalize
    [HttpPost("{id:guid}/finalize")]
    public async Task<ActionResult<ReturnOrderDto>> FinalizeReturn(Guid id, [FromBody] FinalizeReturnRequest req)
    {
        try
        {
            var dto = await _service.FinalizeReturnAsync(id, req, GetUserId(), GetBranchId());
            return Ok(dto);
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
    }

    // PATCH /api/pos/returns/{id}/cancel
    [HttpPatch("{id:guid}/cancel")]
    public async Task<ActionResult> CancelReturn(Guid id)
    {
        try
        {
            await _service.CancelReturnAsync(id, GetUserId(), GetBranchId());
            return Ok(new { message = "Đã hủy phiếu đổi trả." });
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
    }

    // POST /api/pos/returns/{id}/exchange/pay
    /// <summary>
    /// Thanh toán chênh lệch đổi hàng (delta&gt;0).
    /// CASH → tạo Payment Confirmed (cashReceived/changeAmount) + move kho + RO Completed.
    /// TRANSFER → sinh QR (VietQR) + Payment Pending, RO vẫn Pending chờ confirm-transfer.
    /// </summary>
    [HttpPost("{id:guid}/exchange/pay")]
    public async Task<ActionResult> PayExchangeDiff(Guid id, [FromBody] PayExchangeRequest req)
    {
        try
        {
            var res = await _service.PayExchangeDiffAsync(id, req, GetUserId(), GetBranchId());

            if (string.Equals(res.Method, "CASH", StringComparison.OrdinalIgnoreCase))
            {
                var cashReceived = req?.CashReceived ?? 0m;
                return Ok(new
                {
                    returnOrder = res.ReturnOrder,
                    method = "CASH",
                    paidAmount = res.Amount,
                    cashReceived = cashReceived,
                    changeAmount = Math.Max(0m, cashReceived - res.Amount)
                });
            }

            // TRANSFER
            return Ok(new
            {
                paymentId = res.PaymentId,
                qrImageBase64 = res.QRImageBase64,
                transactionContent = res.TransactionContent,
                amount = res.Amount,
                bankAccountNumber = BankAccountNumber,
                bankName = BankName,
                returnOrderId = res.ReturnOrderId,
                method = "TRANSFER"
            });
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
    }

    // POST /api/pos/returns/{id}/exchange/confirm-transfer
    /// <summary>
    /// Xác nhận khách đã chuyển khoản → Payment Confirmed + move kho + RO Completed.
    /// </summary>
    [HttpPost("{id:guid}/exchange/confirm-transfer")]
    public async Task<ActionResult<ReturnOrderDto>> ConfirmExchangeTransfer(Guid id, [FromBody] ConfirmTransferRequest? req)
    {
        try
        {
            var dto = await _service.ConfirmExchangeTransferAsync(id, req, GetUserId(), GetBranchId());
            return Ok(dto);
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
    }

    // POST /api/pos/returns/{id}/exchange/cancel-payment
    /// <summary>
    /// Huỷ Payment chuyển khoản đang chờ (khi bỏ QR / đổi sang method khác). RO vẫn Pending.
    /// </summary>
    [HttpPost("{id:guid}/exchange/cancel-payment")]
    public async Task<ActionResult> CancelExchangePayment(Guid id)
    {
        try
        {
            await _service.CancelExchangePaymentAsync(id, GetBranchId());
            return Ok(new { message = "Đã huỷ thanh toán chênh lệch đang chờ." });
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
    }

    // GET /api/pos/returns/return-policies
    /// <summary>
    /// Trả về danh sách chính sách đổi/trả theo nhóm hàng của chi nhánh hiện tại.
    /// </summary>
    [HttpGet("return-policies")]
    public async Task<ActionResult<List<CategoryPolicyDto>>> GetReturnPolicies()
    {
        return Ok(await _service.GetReturnPoliciesAsync(GetBranchId()));
    }

    // GET /api/pos/returns/category-policies
    [HttpGet("category-policies")]
    public async Task<ActionResult<List<CategoryPolicyDto>>> GetCategoryPolicies()
    {
        return Ok(await _service.GetCategoryPoliciesAsync(GetBranchId()));
    }

    // GET /api/pos/returns/discount-percent
    [HttpGet("discount-percent")]
    public async Task<ActionResult<object>> GetReturnDiscount()
    {
        return Ok(await _service.GetReturnDiscountAsync(GetBranchId()));
    }

    // GET /api/pos/returns/invoice/{invoiceId:guid}/eligible-items
    /// <summary>
    /// Kiểm tra sản phẩm trong hóa đơn có đủ điều kiện đổi/trả không.
    /// </summary>
    [HttpGet("invoice/{invoiceId:guid}/eligible-items")]
    public async Task<ActionResult<InvoiceEligibilityResponse>> GetEligibleItems(Guid invoiceId)
    {
        try
        {
            return Ok(await _service.GetEligibleItemsAsync(invoiceId, GetBranchId()));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy hóa đơn."));
        }
    }

    // GET /api/pos/returns/defective-items
    [HttpGet("defective-items")]
    [HasPermission(PermissionConstants.SALE_VIEW)]
    public async Task<ActionResult<PageResultDto<DefectiveItemDto>>> GetDefectiveItems(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var result = await _service.GetDefectiveItemsAsync(GetBranchId(), page, pageSize, search, fromDate, toDate);
        return Ok(result);
    }

    // POST /api/pos/returns/exchange/quote
    /// <summary>Dry-run: tính Delta + check tồn SP B, KHÔNG ghi DB. UI gọi mỗi khi đổi qty.</summary>
    [HttpPost("exchange/quote")]
    public async Task<ActionResult<ExchangeQuoteResponse>> QuoteExchange([FromBody] ExchangeQuoteRequest req)
    {
        return Ok(await _service.QuoteExchangeAsync(req, GetBranchId()));
    }

    // POST /api/pos/returns/exchange
    /// <summary>Thực thi đổi hàng chênh lệch đầy đủ (validate + tạo ReturnOrder(EXCHANGE) + ExchangeItems + stock moves + ReturnedQuantity) trong 1 transaction.</summary>
    [HttpPost("exchange")]
    public async Task<ActionResult<ReturnOrderDto>> CreateExchange([FromBody] CreateExchangeRequest req)
    {
        try
        {
            var dto = await _service.CreateExchangeAsync(req, GetUserId(), GetBranchId());
            return CreatedAtAction(nameof(GetReturn), new { id = dto.ReturnOrderId }, dto);
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi xử lý đổi hàng: " + ex.Message });
        }
    }
}