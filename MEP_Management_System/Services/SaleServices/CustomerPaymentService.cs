using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.SaleServices;

public class CustomerPaymentService : ICustomerPaymentService
{
    private readonly MEPDatabaseContext _context;
    private readonly CustomerDebtService _customerDebtService;

    public CustomerPaymentService(MEPDatabaseContext context, CustomerDebtService customerDebtService)
    {
        _context = context;
        _customerDebtService = customerDebtService;
    }

    public async Task<(Guid PaymentId, decimal CurrentDebt, decimal AdvanceBalance)> RecordPaymentAsync(
        Guid customerId, RecordCustomerPaymentRequest request, Guid branchId, Guid userId)
    {
        if (request.Amount <= 0)
            throw new BusinessException("Amount must be greater than 0.");

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Status == 1);
        if (customer == null)
            throw new KeyNotFoundException("Customer not found.");

        var debt = await _customerDebtService.CalculateCurrentDebtAsync(customerId);
        var outstandingDebt = debt.CurrentDebt;

        if (outstandingDebt <= 0 && !request.IsAdvance)
            throw new BusinessException("Customer has no outstanding debt and is not making an advance deposit.");

        if (request.Amount > outstandingDebt && !request.IsAdvance)
            throw new BusinessException("Amount exceeds outstanding debt.");

        var payment = new Payment
        {
            PaymentId = Guid.NewGuid(),
            InvoiceId = null,
            CustomerId = customerId,
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

        await TryWriteSystemLog(userId, customer.CustomerName, payment.Amount, payment.Method);

        return (payment.PaymentId, updatedDebt.CurrentDebt, updatedDebt.AdvanceBalance);
    }

    private async Task TryWriteSystemLog(Guid userId, string customerName, decimal amount, string method)
    {
        try
        {
            var log = new SystemLog
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                Action = "RECORD_CUSTOMER_PAYMENT",
                TableName = "payments",
                EntityType = "CustomerPayment",
                EntityId = Guid.NewGuid(),
                Description = $"Ghi nhận thanh toán KH {customerName} - Số tiền: {amount:N0}đ - PTTT: {method}",
                Level = "INFO",
                Source = "CustomerPaymentService",
                Timestamp = DateTime.UtcNow
            };
            _context.SystemLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch { /* Silent */ }
    }
}
