using System.Security.Claims;
using MEP_Management_System.Services;
using MEP_Management_System.Services.SaleServices;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/customers")]
public class CustomerPaymentsController : ControllerBase
{
    private readonly ICustomerPaymentService _customerPaymentService;

    public CustomerPaymentsController(ICustomerPaymentService customerPaymentService)
    {
        _customerPaymentService = customerPaymentService;
    }

    private Guid GetBranchId()
    {
        var val = User.FindFirst("branchId")?.Value;
        return Guid.TryParse(val, out var id) ? id : throw new System.Exception("branchId not found");
    }

    private Guid GetUserId()
    {
        var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(val, out var id) ? id : throw new System.Exception("userId not found");
    }

    [HttpPost("{customerId:guid}/payments")]
    public async Task<IActionResult> RecordPayment(Guid customerId, [FromBody] RecordCustomerPaymentRequest request)
    {
        var (paymentId, currentDebt, advanceBalance) = await _customerPaymentService.RecordPaymentAsync(
            customerId, request, GetBranchId(), GetUserId());

        return Ok(new
        {
            success = true,
            message = request.IsAdvance ? "Advance deposit recorded." : "Payment recorded.",
            paymentId,
            currentDebt,
            advanceBalance
        });
    }
}
