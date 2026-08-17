using AutoMapper;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

public class PaymentService : IPaymentService
{
    private readonly MEPDatabaseContext _context;
    private readonly IMapper _mapper;
    private readonly VietQrService _vietQrService;
    private readonly MepNotificationService _notifications;

    public PaymentService(MEPDatabaseContext context, IMapper mapper, VietQrService vietQrService, MepNotificationService notifications)
    {
        _context = context;
        _mapper = mapper;
        _vietQrService = vietQrService;
        _notifications = notifications;
    }

    private void LogAction(Guid targetEntityId, Guid userId, string action, string tableName, string entityType, string description)
    {
        _context.SystemLogs.Add(new SystemLog
        {
            LogId = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            TableName = tableName,
            EntityType = entityType,
            EntityId = targetEntityId,
            Description = description,
            Level = "INFO",
            Source = "PaymentService",
            Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
    }

    public async Task<PaymentDto> CreatePaymentAsync(Guid invoiceId, CreatePaymentRequest request, Guid userId, Guid branchId)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Order).ThenInclude(o => o.Customer)
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId)
            ?? throw new BusinessException("Invoice not found");

        if (invoice.Status != "Draft")
            throw new BusinessException("Invoice already finalized");

        if (!invoice.InvoiceItems.Any())
            throw new BusinessException("Hoá đơn chưa có sản phẩm nào");

        if (request.Amount <= 0)
            throw new BusinessException("Số tiền thanh toán phải lớn hơn 0");

        var subtotal = invoice.InvoiceItems.Sum(ii => ii.Quantity * ii.SellPrice);
        var totalAmount = subtotal - (invoice.Order.DiscountAmount ?? 0);

        var existingPayments = await _context.Payments
            .Where(p => p.InvoiceId == invoiceId && (p.Status == "Confirmed" || p.Status == "Pending"))
            .SumAsync(p => p.Amount);
        var totalPaid = existingPayments + request.Amount;
        if (totalPaid > totalAmount)
            throw new BusinessException($"Total payments ({totalPaid:N0}) exceed invoice total ({totalAmount:N0})");

        return request.Method switch
        {
            PaymentMethod.Cash => await CreateCashPaymentAsync(invoiceId, invoice, request, userId, branchId),
            PaymentMethod.Transfer => await CreateTransferPaymentAsync(invoiceId, invoice, request, userId, branchId),
            PaymentMethod.Combined => await CreateCombinedPaymentAsync(invoiceId, invoice, request, userId, branchId),
            _ => throw new BusinessException("Invalid payment method")
        };
    }

    private async Task<PaymentDto> CreateCashPaymentAsync(Guid invoiceId, Invoice invoice, CreatePaymentRequest request, Guid userId, Guid branchId)
    {
        if (request.CashReceived == null)
            throw new BusinessException("Tiền mặt nhận được là bắt buộc đối với thanh toán tiền mặt.");

        var cashReceived = request.CashReceived.Value;
        var changeAmount = cashReceived - request.Amount;
        if (cashReceived < request.Amount)
            throw new BusinessException("Cash received is less than amount");

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
        LogAction(payment.PaymentId, userId, "RECORD_PAYMENT", "payments", "Payment",
            $"Thanh toán tiền mặt {payment.Amount:N0} VND cho hóa đơn {invoice.InvoiceCode}");
        await _context.SaveChangesAsync();
        return _mapper.Map<PaymentDto>(payment);
    }

    private async Task<PaymentDto> CreateTransferPaymentAsync(Guid invoiceId, Invoice invoice, CreatePaymentRequest request, Guid userId, Guid branchId)
    {
        var invoiceCode = (await _context.Invoices.FindAsync(invoiceId))?.InvoiceCode ?? invoiceId.ToString()[..8];
        var content = $"MEP {invoiceCode}";
        var (qrBase64, transactionContent) = await _vietQrService.GenerateQrAsync(request.Amount, content);

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            InvoiceId = invoiceId,
            Method = PaymentMethod.Transfer,
            Amount = request.Amount,
            Status = "Pending",
            VietQRString = transactionContent,
            QRExpiredAt = DateTime.UtcNow.AddMinutes(5),
            CreatedAt = DateTime.UtcNow,
            BranchId = branchId
        };
        _context.Payments.Add(payment);
        LogAction(payment.PaymentId, userId, "RECORD_PAYMENT", "payments", "Payment",
            $"Tạo QR chuyển khoản {payment.Amount:N0} VND cho hóa đơn {invoice.InvoiceCode}");
        await _context.SaveChangesAsync();

        var dto = _mapper.Map<PaymentDto>(payment);
        dto.QRImageBase64 = qrBase64;
        dto.TransactionContent = transactionContent;
        return dto;
    }

    private async Task<PaymentDto> CreateCombinedPaymentAsync(Guid invoiceId, Invoice invoice, CreatePaymentRequest request, Guid userId, Guid branchId)
    {
        var lines = request.PaymentLines;
        if (lines == null || !lines.Any())
            throw new BusinessException("Danh sách thanh toán kết hợp không được để trống.");

        var linesSum = lines.Sum(l => l.Amount);
        if (linesSum != request.Amount)
            throw new BusinessException($"Tổng số tiền các hình thức thanh toán ({linesSum:N0}) không khớp với tổng tiền yêu cầu ({request.Amount:N0}).");

        var qrImages = new Dictionary<Guid, string>();
        var transContents = new Dictionary<Guid, string>();
        var invoiceCode = invoice.InvoiceCode ?? invoiceId.ToString()[..8];

        foreach (var line in lines)
        {
            if (line.Method == PaymentMethod.Cash)
            {
                if (line.CashReceived == null)
                    throw new BusinessException("Tiền mặt nhận được là bắt buộc đối với thanh toán tiền mặt.");
                if (line.CashReceived.Value < line.Amount)
                    throw new BusinessException("Cash received is less than amount");
            }

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

            if (line.Method == PaymentMethod.Transfer)
            {
                var content = $"MEP {invoiceCode}";
                var (qrBase64, transactionContent) = await _vietQrService.GenerateQrAsync(line.Amount, content);
                p.VietQRString = transactionContent;
                p.QRExpiredAt = DateTime.UtcNow.AddMinutes(5);
                qrImages[p.PaymentId] = qrBase64;
                transContents[p.PaymentId] = transactionContent;
            }

            _context.Payments.Add(p);
        }

        LogAction(invoiceId, userId, "RECORD_PAYMENT", "payments", "Payment",
            $"Thanh toán kết hợp cho hóa đơn {invoice.InvoiceCode}, tổng {request.Amount:N0} VND");
        await _context.SaveChangesAsync();

        var allPayments = await _context.Payments.Where(p => p.InvoiceId == invoiceId).ToListAsync();
        var paymentDtos = allPayments.Select(p =>
        {
            var dto = _mapper.Map<PaymentDto>(p);
            if (qrImages.ContainsKey(p.PaymentId))
            {
                dto.QRImageBase64 = qrImages[p.PaymentId];
                dto.TransactionContent = transContents[p.PaymentId];
            }
            return dto;
        }).ToList();

        var transferPayment = paymentDtos.FirstOrDefault(p => p.Method == PaymentMethod.Transfer);
        return transferPayment ?? paymentDtos.First();
    }

    public async Task<QrCodeResultDto> GetQRCodeAsync(Guid paymentId)
    {
        var payment = await _context.Payments.FindAsync(paymentId)
            ?? throw new KeyNotFoundException("Payment not found");

        if (payment.Method != PaymentMethod.Transfer)
            throw new BusinessException("Only transfer payments have QR");

        if (payment.QRExpiredAt < DateTime.UtcNow)
            throw new BusinessException("QR code has expired");

        return new QrCodeResultDto
        {
            VietQRString = payment.VietQRString ?? "",
            ExpiresAt = payment.QRExpiredAt
        };
    }

    public async Task<PaymentDto> ConfirmTransferAsync(Guid paymentId, Guid userId, Guid branchId)
    {
        var payment = await _context.Payments.FindAsync(paymentId)
            ?? throw new KeyNotFoundException("Payment not found");

        if (payment.Method != PaymentMethod.Transfer)
            throw new BusinessException("Only transfer payments need confirmation");

        if (payment.Status == "Confirmed")
            throw new BusinessException("Payment already confirmed");

        if (payment.QRExpiredAt.HasValue && payment.QRExpiredAt.Value < DateTime.UtcNow)
            throw new BusinessException("Mã QR đã hết hạn. Vui lòng hủy và tạo mã mới.");

        payment.Status = "Confirmed";
        payment.PaidAt = DateTime.UtcNow;
        LogAction(payment.PaymentId, userId, "CONFIRM_TRANSFER", "payments", "Payment",
            $"Xác nhận chuyển khoản {payment.Amount:N0} VND cho hóa đơn {payment.InvoiceId}");
        await _context.SaveChangesAsync();

        _ = _notifications.NotifyBranchPaymentConfirmed(branchId, paymentId);

        return _mapper.Map<PaymentDto>(payment);
    }

    public async Task<PaymentStatusDto> GetPaymentStatusAsync(Guid paymentId)
    {
        var payment = await _context.Payments.FindAsync(paymentId)
            ?? throw new KeyNotFoundException("Payment not found");

        return new PaymentStatusDto { Status = payment.Status };
    }

    public async Task<bool> CancelPaymentAsync(Guid paymentId, Guid userId)
    {
        var payment = await _context.Payments.FindAsync(paymentId)
            ?? throw new KeyNotFoundException("Payment not found");

        if (payment.Status != "Pending")
            throw new BusinessException("Only pending payments can be cancelled");

        payment.Status = "Cancelled";
        LogAction(payment.PaymentId, userId, "CANCEL_PAYMENT", "payments", "Payment",
            $"Hủy thanh toán {payment.Amount:N0} VND");
        await _context.SaveChangesAsync();
        return true;
    }
}
