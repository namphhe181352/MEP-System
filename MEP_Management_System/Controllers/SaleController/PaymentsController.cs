using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos")]
[HasPermission(PermissionConstants.PAYMENT_VIEW)]
public class PaymentsController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;
    private readonly IMapper _mapper;
    private readonly MepNotificationService _notifications;
    private readonly VietQrService _vietQrService;

    public PaymentsController(Models.MEPDatabaseContext context, IMapper mapper, MepNotificationService notifications, VietQrService vietQrService)
    {
        _context = context;
        _mapper = mapper;
        _notifications = notifications;
        _vietQrService = vietQrService;
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
        // VAT removed - POS retail does not include VAT
        var totalAmount = subtotal - (invoice.Order.DiscountAmount ?? 0);

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
            // Transaction Content e.g. MEP INV-1234
            var invoiceCode = (await _context.Invoices.FindAsync(invoiceId))?.InvoiceCode ?? invoiceId.ToString().Substring(0, 8);
            var content = $"MEP {invoiceCode}";
            
            var (qrBase64, transactionContent) = await _vietQrService.GenerateQrAsync(request.Amount, content);
            
            var payment = new Payment
            {
                PaymentId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                Method = PaymentMethod.Transfer,
                Amount = request.Amount,
                Status = "Pending",
                VietQRString = transactionContent, // Saving transaction content in VietQRString for matching
                QRExpiredAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow,
                BranchId = branchId
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            
            var dto = _mapper.Map<PaymentDto>(payment);
            dto.QRImageBase64 = qrBase64;
            dto.TransactionContent = transactionContent;
            
            return Created($"/api/pos/payments/{payment.PaymentId}", dto);
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

    // GET /api/pos/payments/{paymentId}/status
    [HttpGet("payments/{paymentId:guid}/status")]
    public async Task<ActionResult> GetPaymentStatus(Guid paymentId)
    {
        var payment = await _context.Payments.FindAsync(paymentId);
        if (payment == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Payment not found"));
        return Ok(new { status = payment.Status });
    }
}
