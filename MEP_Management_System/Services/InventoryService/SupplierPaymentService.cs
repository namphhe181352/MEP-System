using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.InventoryService;

public class SupplierPaymentService : ISupplierPaymentService
{
    private readonly MEPDatabaseContext _context;

    public SupplierPaymentService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<(List<SupplierPaymentResponseDto> Items, int TotalCount)> GetPaymentsAsync(
        Guid? branchId, Guid? supplierId, int pageNumber, int pageSize)
    {
        var query = _context.SupplierPayments
            .Include(p => p.Supplier)
            .Include(p => p.PurchaseOrder)
            .Include(p => p.RecordedByUser)
            .AsQueryable();

        if (branchId.HasValue)
        {
            query = query.Where(p => p.Supplier != null && p.Supplier.BranchId == branchId.Value);
        }

        if (supplierId.HasValue)
            query = query.Where(p => p.SupplierId == supplierId.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new SupplierPaymentResponseDto
            {
                PaymentId = p.PaymentId,
                SupplierId = p.SupplierId,
                SupplierName = p.Supplier != null ? p.Supplier.SupplierName : "",
                OrderCode = p.PurchaseOrder != null ? p.PurchaseOrder.OrderCode : null,
                Amount = p.Amount,
                PaymentDate = p.PaymentDate.ToString("yyyy-MM-dd HH:mm"),
                PaymentMethod = p.PaymentMethod,
                ReferenceCode = p.ReferenceCode,
                Note = p.Note,
                RecordedByName = p.RecordedByUser != null ? p.RecordedByUser.Username : "",
                Status = p.IsDeleted == 1 ? "CANCELLED" : "ACTIVE",
                CreatedBy = p.CreatedBy
            }).ToListAsync();

        return (items, totalCount);
    }

    public async Task<Guid> CreatePaymentAsync(CreateSupplierPaymentDto dto, Guid currentUserId)
    {
        var payment = new SupplierPayment
        {
            PaymentId = Guid.NewGuid(),
            SupplierId = dto.SupplierId,
            PurchaseOrderId = dto.PurchaseOrderId,
            Amount = dto.Amount,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = dto.PaymentMethod,
            ReferenceCode = dto.ReferenceCode,
            Note = dto.Note,
            RecordedBy = currentUserId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = 0,
            CreatedBy = null
        };

        _context.SupplierPayments.Add(payment);
        await _context.SaveChangesAsync();

        return payment.PaymentId;
    }

    public async Task<bool> UpdatePaymentAsync(Guid id, UpdateSupplierPaymentDto dto)
    {
        var payment = await _context.SupplierPayments.FindAsync(id);
        if (payment == null)
            throw new KeyNotFoundException($"No payment with ID: {id}");

        if (dto.Amount.HasValue && dto.Amount.Value > 0) payment.Amount = dto.Amount.Value;
        if (!string.IsNullOrWhiteSpace(dto.PaymentMethod)) payment.PaymentMethod = dto.PaymentMethod;
        if (dto.ReferenceCode != null) payment.ReferenceCode = dto.ReferenceCode;
        if (dto.Note != null) payment.Note = dto.Note;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeletePaymentAsync(Guid id)
    {
        var payment = await _context.SupplierPayments.FindAsync(id);
        if (payment == null)
            throw new KeyNotFoundException($"No payment with ID: {id}");

        payment.IsDeleted = 1;
        await _context.SaveChangesAsync();
        return true;
    }
}
