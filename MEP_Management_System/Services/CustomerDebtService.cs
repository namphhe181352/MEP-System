using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

public class CustomerDebtService
{
    private readonly MEPDatabaseContext _context;

    public CustomerDebtService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<CustomerDebtSummary> CalculateCurrentDebtAsync(Guid customerId)
    {
        var totalInvoiced = await _context.Invoices
            .Where(i => i.Order.CustomerId == customerId && i.Status == "COMPLETED")
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

        var totalPaid = await _context.Payments
            .Where(p => p.Invoice != null && p.Invoice.Order.CustomerId == customerId && p.Status == "Confirmed")
            .SumAsync(p => (decimal?)(p.Amount - (p.ChangeAmount ?? 0))) ?? 0m;

        var totalRefunded = await _context.RefundRecords
            .Where(r => r.CustomerId == customerId && r.Status == "COMPLETED")
            .SumAsync(r => (decimal?)r.RefundAmount) ?? 0m;

        var raw = totalInvoiced - totalPaid - totalRefunded;

        return new CustomerDebtSummary
        {
            CurrentDebt = raw < 0 ? 0m : raw,
            AdvanceBalance = raw < 0 ? -raw : 0m
        };
    }
}

public class CustomerDebtSummary
{
    public decimal CurrentDebt { get; set; }
    public decimal AdvanceBalance { get; set; }
}
