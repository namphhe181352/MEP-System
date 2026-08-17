using System.Security.Claims;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos")]
[HasPermission(PermissionConstants.PAYMENT_VIEW)]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found"));
    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? throw new Exception("userId"));

    [HttpPost("invoices/{invoiceId:guid}/payments")]
    public async Task<ActionResult<PaymentDto>> CreatePayment(Guid invoiceId, [FromBody] CreatePaymentRequest request)
    {
        var result = await _paymentService.CreatePaymentAsync(invoiceId, request, GetUserId(), GetBranchId());
        return Created($"/api/pos/payments/{result.PaymentId}", result);
    }

    [HttpGet("payments/{paymentId:guid}/qr")]
    public async Task<ActionResult> GetQRCode(Guid paymentId)
    {
        var result = await _paymentService.GetQRCodeAsync(paymentId);
        return Ok(new { vietQRString = result.VietQRString, expiresAt = result.ExpiresAt });
    }

    [HttpPost("payments/{paymentId:guid}/confirm-transfer")]
    public async Task<ActionResult<PaymentDto>> ConfirmTransfer(Guid paymentId)
    {
        var result = await _paymentService.ConfirmTransferAsync(paymentId, GetUserId(), GetBranchId());
        return Ok(result);
    }

    [HttpGet("payments/{paymentId:guid}/status")]
    public async Task<ActionResult> GetPaymentStatus(Guid paymentId)
    {
        var result = await _paymentService.GetPaymentStatusAsync(paymentId);
        return Ok(new { status = result.Status });
    }

    [HttpPost("payments/{paymentId:guid}/cancel")]
    public async Task<ActionResult> CancelPayment(Guid paymentId)
    {
        await _paymentService.CancelPaymentAsync(paymentId, GetUserId());
        return Ok(new { success = true, message = "Payment cancelled" });
    }
}
