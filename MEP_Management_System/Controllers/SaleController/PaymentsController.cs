using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.Models;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Services;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos")]
[Authorize(Roles = "SalesStaff,Owner")]
public class PaymentsController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;
    private readonly IMapper _mapper;
    private readonly MepNotificationService _notifications;

    public PaymentsController(Models.MEPDatabaseContext context, IMapper mapper, MepNotificationService notifications)
    {
        _context = context;
        _mapper = mapper;
        _notifications = notifications;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found"));
    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? throw new Exception("userId"));

    // POST /api/pos/invoices/{invoiceId}/payments
    [HttpPost("invoices/{invoiceId:guid}/payments")]
    public async Task<ActionResult<PaymentDto>> CreatePayment(Guid invoiceId, [FromBody] CreatePaymentRequest request)
    {
        var branchId = GetBranchId();
        var invoice = await _context.Invoices
            .Include(i => i.Order).ThenInclude(o => o.Customer)
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);

        if (invoice == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Invoice not found"));
        if (invoice.Status != "Draft") return BadRequest(ApiErrorResponse.Create("MSG-78", "Invoice already finalized"));

        var subtotal = invoice.InvoiceItems.Sum(ii => ii.Quantity * ii.SellPrice);
        var vatAmount = Math.Round(subtotal * 0.08m, 2);
        var totalAmount = subtotal + vatAmount - (invoice.Order.DiscountAmount ?? 0);

        var existingPayments = await _context.Payments
            .Where(p => p.InvoiceId == invoiceId && (p.Status == "Confirmed" || p.Status == "Pending"))
            .SumAsync(p => p.Amount);
        var totalPaid = existingPayments + request.Amount;
        if (totalPaid > totalAmount)
            return BadRequest(ApiErrorResponse.Create("MSG-76", $"Total payments ({totalPaid:N0}) exceed invoice total ({totalAmount:N0})"));

        if (request.Method == PaymentMethod.Cash)
        {
            var cashReceived = request.CashReceived ?? request.Amount;
            var changeAmount = cashReceived - request.Amount;
            if (cashReceived < request.Amount) return BadRequest(ApiErrorResponse.Create("MSG-76", "Cash received is less than amount"));

            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                Method = PaymentMethod.Cash,
                Amount = request.Amount,
                Status = "Confirmed",
                CashReceived = cashReceived,
                ChangeAmount = changeAmount > 0 ? changeAmount : 0,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                BranchId = branchId
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            return Created($"/api/pos/payments/{payment.PaymentId}", _mapper.Map<PaymentDto>(payment));
        }

        if (request.Method == PaymentMethod.Transfer)
        {
            var vietQR = GenerateVietQR(request.Amount);
            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                Method = PaymentMethod.Transfer,
                Amount = request.Amount,
                Status = "Pending",
                VietQRString = vietQR.qrString,
                QRExpiredAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
                BranchId = branchId
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            return Created($"/api/pos/payments/{payment.PaymentId}", _mapper.Map<PaymentDto>(payment));
        }

        if (request.Method == PaymentMethod.Combined)
        {
            var lines = request.PaymentLines ?? throw new Exception("Payment lines required for combined payment");
            foreach (var line in lines)
            {
                var p = new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    Method = line.Method,
                    Amount = line.Amount,
                    Status = line.Method == PaymentMethod.Cash ? "Confirmed" : "Pending",
                    CashReceived = line.CashReceived,
                    ChangeAmount = (line.CashReceived ?? 0) > line.Amount ? (line.CashReceived - line.Amount) : 0,
                    PaidAt = line.Method == PaymentMethod.Cash ? DateTime.UtcNow : (DateTime?)null,
                    CreatedAt = DateTime.UtcNow,
                    BranchId = branchId
                };
                _context.Payments.Add(p);
            }
            await _context.SaveChangesAsync();

            var allPayments = await _context.Payments.Where(p => p.InvoiceId == invoiceId).ToListAsync();
            return Created($"/api/pos/invoices/{invoiceId}/payments", allPayments.Select(_mapper.Map<PaymentDto>));
        }

        return BadRequest(ApiErrorResponse.Create("MSG-76", "Invalid payment method"));
    }

    // GET /api/pos/payments/{paymentId}/qr
    [HttpGet("payments/{paymentId:guid}/qr")]
    public async Task<ActionResult> GetQRCode(Guid paymentId)
    {
        var payment = await _context.Payments.FindAsync(paymentId);
        if (payment == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Payment not found"));
        if (payment.Method != PaymentMethod.Transfer) return BadRequest(ApiErrorResponse.Create("MSG-76", "Only transfer payments have QR"));
        if (payment.QRExpiredAt < DateTime.UtcNow) return BadRequest(ApiErrorResponse.Create("MSG-76", "QR code has expired"));

        return Ok(new { vietQRString = payment.VietQRString, expiresAt = payment.QRExpiredAt });
    }

    // POST /api/pos/payments/{paymentId}/confirm-transfer
    [HttpPost("payments/{paymentId:guid}/confirm-transfer")]
    public async Task<ActionResult<PaymentDto>> ConfirmTransfer(Guid paymentId)
    {
        var branchId = GetBranchId();
        var payment = await _context.Payments.FindAsync(paymentId);
        if (payment == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Payment not found"));
        if (payment.Method != PaymentMethod.Transfer) return BadRequest(ApiErrorResponse.Create("MSG-76", "Only transfer payments need confirmation"));
        if (payment.Status == "Confirmed") return BadRequest(ApiErrorResponse.Create("MSG-76", "Payment already confirmed"));

        payment.Status = "Confirmed";
        payment.PaidAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Real-time: notify branch staff that transfer was confirmed
        _ = _notifications.NotifyBranchPaymentConfirmed(branchId, paymentId);

        return Ok(_mapper.Map<PaymentDto>(payment));
    }

    // POST /api/pos/payments/debt
    [HttpPost("payments/debt")]
    public async Task<ActionResult<PaymentDto>> RecordDebt([FromBody] DebtPaymentRequest request)
    {
        var branchId = GetBranchId();
        var customer = await _context.Customers.FindAsync(request.CustomerId);
        if (customer == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Customer not found"));

        var newDebt = (customer.CurrentDebt ?? 0) + request.Amount;
        if (newDebt > (customer.DebtLimit ?? 0)) return UnprocessableEntity(ApiErrorResponse.Create("MSG-84", $"Debt exceeds limit. Current: {customer.CurrentDebt}, Limit: {customer.DebtLimit}, Requested: {request.Amount}"));

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            InvoiceId = null,
            Method = PaymentMethod.Debt,
            Amount = request.Amount,
            Status = "Confirmed",
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            BranchId = branchId
        };
        _context.Payments.Add(payment);

        customer.CurrentDebt = newDebt;
        await _context.SaveChangesAsync();

        return Created($"/api/pos/payments/{payment.PaymentId}", _mapper.Map<PaymentDto>(payment));
    }

    private static (string qrString, DateTime expiresAt) GenerateVietQR(decimal amount)
    {
        var random = new Random();
        var trace = random.Next(100000, 999999);
        var qr = $"MEP|VP|{DateTime.UtcNow:yyyyMMddHHmmss}|{trace}|{amount:N0}|0|0|0";
        return (qr, DateTime.UtcNow.AddMinutes(5));
    }
}

public class CreatePaymentRequest
{
    public string Method { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal? CashReceived { get; set; }
    public System.Collections.Generic.List<PaymentLine>? PaymentLines { get; set; }
}

public class PaymentLine
{
    public string Method { get; set; } = null!;
    public decimal Amount { get; set; }
    public decimal? CashReceived { get; set; }
}
