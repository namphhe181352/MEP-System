using System.Security.Claims;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/customers")]
public class CustomerPaymentsController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly CustomerDebtService _customerDebtService;

    public CustomerPaymentsController(MEPDatabaseContext context, CustomerDebtService customerDebtService)
    {
        _context = context;
        _customerDebtService = customerDebtService;
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

    // GetUserId is available for future use when Payment.UserId is added

    /// <summary>
    /// POST /api/pos/customers/{customerId}/payments
    /// Record a payment from a customer (debt repayment or advance deposit).
    /// </summary>
    [HttpPost("{customerId:guid}/payments")]
    public async Task<IActionResult> RecordPayment(Guid customerId, [FromBody] RecordCustomerPaymentRequest request)
    {
        if (request.Amount <= 0)
            return BadRequest(new { success = false, message = "Amount must be greater than 0." });

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Status == 1);
        if (customer == null)
            return NotFound(new { success = false, message = "Customer not found." });

        var debt = await _customerDebtService.CalculateCurrentDebtAsync(customerId);
        var outstandingDebt = debt.CurrentDebt;

        if (outstandingDebt <= 0 && !request.IsAdvance)
            return BadRequest(new { success = false, message = "Customer has no outstanding debt and is not making an advance deposit." });

        if (request.Amount > outstandingDebt && !request.IsAdvance)
            return BadRequest(new { success = false, message = "Amount exceeds outstanding debt." });

        var branchId = GetBranchId();

        // Create the payment record (no InvoiceId — this is a standalone debt repayment)
        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            InvoiceId = null,
            Method = request.Method ?? "Cash",
            Amount = request.Amount,
            Status = "Confirmed",
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            BranchId = branchId
        };
        _context.Payments.Add(payment);

        if (request.IsAdvance)
        {
            // Advance: amount may exceed current debt, excess goes to AdvanceBalance
            var excess = request.Amount - outstandingDebt;
            customer.CurrentDebt = 0m;
            customer.AdvanceBalance = (customer.AdvanceBalance ?? 0m) + excess;
        }
        else
        {
            customer.CurrentDebt = Math.Max(0m, outstandingDebt - request.Amount);
        }

        await _context.SaveChangesAsync();

        var updatedDebt = await _customerDebtService.CalculateCurrentDebtAsync(customerId);

        return Ok(new
        {
            success = true,
            message = request.IsAdvance ? "Advance deposit recorded." : "Payment recorded.",
            paymentId = payment.PaymentId,
            currentDebt = updatedDebt.CurrentDebt,
            advanceBalance = updatedDebt.AdvanceBalance
        });
    }
}

public class RecordCustomerPaymentRequest
{
    public decimal Amount { get; set; }
    public string? Method { get; set; }
    public string? Note { get; set; }
    public bool IsAdvance { get; set; }
}
