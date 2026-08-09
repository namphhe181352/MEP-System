using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

/// <summary>
/// Ghi nhật ký biến động kho (inventory_ledger) mỗi khi giao dịch COMPLETED.
/// PHẢI được gọi trong cùng 1 DbTransaction với thao tác cập nhật tồn kho.
/// </summary>
public class InventoryLedgerService
{
    private readonly MEPDatabaseContext _context;

    public InventoryLedgerService(MEPDatabaseContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Ghi 1 dòng vào inventory_ledger.
    /// Gọi sau khi đã cập nhật BranchProduct (stock, cost_price).
    /// </summary>
    public async Task WriteLedgerEntry(
        Guid branchId,
        Guid branchProductId,
        string docType,
        Guid docId,
        string? docCode,
        DateTime transactionTime,
        decimal qtyChange,
        decimal unitCostPrice,
        decimal stockAfter,
        decimal avgCostAfter)
    {
        var entry = new InventoryLedger
        {
            LedgerId = Guid.NewGuid(),
            BranchId = branchId,
            BranchProductId = branchProductId,
            DocType = docType,
            DocId = docId,
            DocCode = docCode,
            TransactionTime = transactionTime,
            QtyChange = qtyChange,
            UnitCostPrice = Math.Round(unitCostPrice, 4),
            TotalCostValue = Math.Round(qtyChange * unitCostPrice, 4),
            StockAfter = stockAfter,
            AvgCostAfter = Math.Round(avgCostAfter, 4),
            CreatedAt = DateTime.UtcNow
        };

        _context.InventoryLedgers.Add(entry);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Ghi nhiều dòng ledger trong 1 lần (batch insert).
    /// </summary>
    public async Task WriteLedgerEntries(IEnumerable<InventoryLedger> entries)
    {
        _context.InventoryLedgers.AddRange(entries);
        await _context.SaveChangesAsync();
    }
}
