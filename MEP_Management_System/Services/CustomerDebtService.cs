using System.Linq;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

public class CustomerDebtService
{
    private static readonly string[] CompletedStatuses = { "COMPLETED", "Completed", "PAID", "Paid", "DONE", "Done" };

    private readonly MEPDatabaseContext _context;

    public CustomerDebtService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<CustomerDebtSummary> CalculateCurrentDebtAsync(Guid customerId)
    {
        var totalInvoiced = await _context.Invoices
            .Where(i => i.Order.CustomerId == customerId && CompletedStatuses.Contains(i.Status))
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

        var totalPaid = await _context.Payments
            .Where(p => p.Status == "Confirmed" && (p.Invoice != null && p.Invoice.Order.CustomerId == customerId || p.CustomerId == customerId))
            .SumAsync(p => (decimal?)(p.Amount - (p.ChangeAmount ?? 0))) ?? 0m;

        var totalRefunded = await _context.RefundRecords
            .Where(r => r.CustomerId == customerId && CompletedStatuses.Contains(r.Status))
            .SumAsync(r => (decimal?)r.RefundAmount) ?? 0m;

        var raw = totalInvoiced - totalPaid - totalRefunded;

        return new CustomerDebtSummary
        {
            CurrentDebt = raw < 0 ? 0m : raw,
            AdvanceBalance = raw < 0 ? -raw : 0m
        };
    }

    /// <summary>
    /// Tính dynamic current debt cho nhiều customer (1 trip DB, không N+1).
    /// Trả về dict[customerId, currentDebt].
    /// </summary>
    public async Task<Dictionary<Guid, decimal>> CalculateCurrentDebtBatchAsync(IEnumerable<Guid> customerIds)
    {
        var idList = customerIds.ToList();
        if (idList.Count == 0) return new Dictionary<Guid, decimal>();

        var invoiced = await _context.Invoices
            .Where(i => i.Order.CustomerId.HasValue && idList.Contains(i.Order.CustomerId.Value) && CompletedStatuses.Contains(i.Status))
            .GroupBy(i => i.Order.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(i => (decimal?)i.TotalAmount) ?? 0m })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        var paid = await _context.Payments
            .Where(p => p.Status == "Confirmed" && ((p.Invoice != null && p.Invoice.Order.CustomerId.HasValue
                        && idList.Contains(p.Invoice.Order.CustomerId.Value)) || (p.CustomerId.HasValue && idList.Contains(p.CustomerId.Value))))
            .GroupBy(p => p.CustomerId ?? p.Invoice!.Order.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(p => (decimal?)(p.Amount - (p.ChangeAmount ?? 0))) ?? 0m })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        var refunded = await _context.RefundRecords
            .Where(r => r.CustomerId.HasValue && idList.Contains(r.CustomerId.Value) && CompletedStatuses.Contains(r.Status))
            .GroupBy(r => r.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, Total = g.Sum(r => (decimal?)r.RefundAmount) ?? 0m })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Total);

        var result = new Dictionary<Guid, decimal>();
        foreach (var id in idList)
        {
            invoiced.TryGetValue(id, out var inv);
            paid.TryGetValue(id, out var p);
            refunded.TryGetValue(id, out var r);
            var raw = inv - p - r;
            result[id] = raw < 0 ? 0m : raw;
        }
        return result;
    }
}

public class CustomerDebtSummary
{
    public decimal CurrentDebt { get; set; }
    public decimal AdvanceBalance { get; set; }
}
