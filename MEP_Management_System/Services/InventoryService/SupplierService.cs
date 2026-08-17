using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.InventoryService;

public class SupplierService : ISupplierService
{
    private readonly MEPDatabaseContext _context;
    private readonly ISupplierDebtService _supplierDebtService;

    public SupplierService(MEPDatabaseContext context, ISupplierDebtService supplierDebtService)
    {
        _context = context;
        _supplierDebtService = supplierDebtService;
    }

    private static string ToCode(Guid id) => id.ToString()[..8].ToUpper();
    private static string StatusLabel(short? s) => s == 1 ? "active" : "inactive";
    private static short ParseStatus(string? s) => s == "inactive" ? (short)0 : (short)1;

    public async Task<(List<SupplierListItemDto> Items, int TotalCount, List<string> DistinctGroups)> GetSuppliersAsync(
        Guid branchId, SupplierQueryParameters p)
    {
        var query = _context.Suppliers.Where(s => s.BranchId == branchId && s.Status != -1).AsQueryable();

        if (!string.IsNullOrWhiteSpace(p.SearchTerm))
        {
            var term = p.SearchTerm.Trim().ToLower();
            query = query.Where(s =>
                s.SupplierName.ToLower().Contains(term) ||
                (s.ContactPhone != null && s.ContactPhone.Contains(term)) ||
                (s.ContactPerson != null && s.ContactPerson.ToLower().Contains(term)) ||
                (s.TaxCode != null && s.TaxCode.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(p.Status) && p.Status != "all")
        {
            var st = ParseStatus(p.Status);
            query = query.Where(s => s.Status == st);
        }

        if (!string.IsNullOrWhiteSpace(p.GroupName) && p.GroupName != "all")
            query = query.Where(s => s.GroupName == p.GroupName);

        var totalCount = await query.CountAsync();

        var suppliers = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((p.PageNumber - 1) * p.PageSize)
            .Take(p.PageSize)
            .ToListAsync();

        var distinctGroups = await _context.Suppliers
            .Where(s => s.BranchId == branchId && s.GroupName != null)
            .Select(s => s.GroupName!)
            .Distinct()
            .OrderBy(g => g)
            .ToListAsync();

        var supplierIds = suppliers.Select(s => s.SupplierId).ToList();
        var debtMap = await _supplierDebtService.CalculateCurrentDebtBatchAsync(supplierIds);

        var items = suppliers.Select(s =>
        {
            var debt = debtMap.TryGetValue(s.SupplierId, out var d) ? d : new SupplierDebtSummary();
            return new SupplierListItemDto
            {
                Id = s.SupplierId,
                Code = ToCode(s.SupplierId),
                Name = s.SupplierName,
                ContactPerson = s.ContactPerson,
                ContactPhone = s.ContactPhone,
                ContactEmail = s.ContactEmail,
                Address = s.Address,
                GroupName = s.GroupName,
                TaxCode = s.TaxCode,
                Status = StatusLabel(s.Status),
                CreatedAt = s.CreatedAt,
                CurrentDebt = debt.CurrentDebt,
                TotalPurchased = debt.TotalPurchased,
                OverpaidAmount = debt.OverpaidAmount,
                DebtStatus = debt.Status
            };
        }).ToList();

        return (items, totalCount, distinctGroups);
    }

    public async Task<SupplierDetailDto?> GetSupplierAsync(Guid id, Guid branchId)
    {
        var s = await _context.Suppliers
            .FirstOrDefaultAsync(x => x.SupplierId == id && x.BranchId == branchId && x.Status != -1);

        if (s == null) return null;

        return new SupplierDetailDto
        {
            Id = s.SupplierId,
            Code = ToCode(s.SupplierId),
            Name = s.SupplierName,
            ContactPerson = s.ContactPerson,
            ContactPhone = s.ContactPhone,
            ContactEmail = s.ContactEmail,
            TaxCode = s.TaxCode,
            GroupName = s.GroupName,
            BankAccount = s.BankAccount,
            BankName = s.BankName,
            Website = s.Website,
            Address = s.Address,
            Notes = s.Notes,
            Status = StatusLabel(s.Status),
            CreatedAt = s.CreatedAt
        };
    }

    public async Task<SupplierDetailDto> CreateSupplierAsync(SupplierUpsertRequest req, Guid branchId, Guid? userId)
    {
        var inputName = req.SupplierName.Trim();
        var exists = await _context.Suppliers
            .AnyAsync(x => x.SupplierName.Trim().ToLower() == inputName.ToLower() && x.BranchId == branchId && x.Status != -1);

        if (exists)
            throw new BusinessException($"Nhà cung cấp '{req.SupplierName}' đã tồn tại.");

        var s = new Models.Supplier
        {
            SupplierId = Guid.NewGuid(),
            SupplierName = inputName,
            ContactPhone = req.ContactPhone?.Trim(),
            ContactEmail = req.ContactEmail?.Trim(),
            ContactPerson = req.ContactPerson?.Trim(),
            TaxCode = req.TaxCode?.Trim(),
            GroupName = req.GroupName?.Trim(),
            BankAccount = req.BankAccount?.Trim(),
            BankName = req.BankName?.Trim(),
            Website = req.Website?.Trim(),
            Address = req.Address?.Trim(),
            Notes = req.Notes?.Trim(),
            Status = req.Status,
            BranchId = branchId,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            CreatedBy = userId
        };

        _context.Suppliers.Add(s);
        await _context.SaveChangesAsync();

        return new SupplierDetailDto
        {
            Id = s.SupplierId,
            Code = ToCode(s.SupplierId),
            Name = s.SupplierName,
            ContactPerson = s.ContactPerson,
            ContactPhone = s.ContactPhone,
            ContactEmail = s.ContactEmail,
            TaxCode = s.TaxCode,
            GroupName = s.GroupName,
            BankAccount = s.BankAccount,
            BankName = s.BankName,
            Website = s.Website,
            Address = s.Address,
            Notes = s.Notes,
            Status = StatusLabel(s.Status),
            CreatedAt = s.CreatedAt
        };
    }

    public async Task<SupplierDetailDto?> UpdateSupplierAsync(Guid id, SupplierUpsertRequest req, Guid branchId, Guid? userId)
    {
        var s = await _context.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == id && x.BranchId == branchId && x.Status != -1);
        if (s == null) return null;

        var inputName = req.SupplierName.Trim();
        var nameExists = await _context.Suppliers
            .AnyAsync(x => x.SupplierId != id && x.BranchId == branchId && x.Status != -1
                        && x.SupplierName.Trim().ToLower() == inputName.ToLower());
        if (nameExists)
            throw new BusinessException($"Nhà cung cấp '{req.SupplierName}' đã tồn tại.");

        s.SupplierName = inputName;
        s.ContactPhone = req.ContactPhone?.Trim();
        s.ContactEmail = req.ContactEmail?.Trim();
        s.ContactPerson = req.ContactPerson?.Trim();
        s.TaxCode = req.TaxCode?.Trim();
        s.GroupName = req.GroupName?.Trim();
        s.BankAccount = req.BankAccount?.Trim();
        s.BankName = req.BankName?.Trim();
        s.Website = req.Website?.Trim();
        s.Address = req.Address?.Trim();
        s.Notes = req.Notes?.Trim();
        s.Status = req.Status;
        s.UpdatedBy = userId;
        s.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync();

        return new SupplierDetailDto
        {
            Id = s.SupplierId,
            Code = ToCode(s.SupplierId),
            Name = s.SupplierName,
            ContactPerson = s.ContactPerson,
            ContactPhone = s.ContactPhone,
            ContactEmail = s.ContactEmail,
            TaxCode = s.TaxCode,
            GroupName = s.GroupName,
            BankAccount = s.BankAccount,
            BankName = s.BankName,
            Website = s.Website,
            Address = s.Address,
            Notes = s.Notes,
            Status = StatusLabel(s.Status),
            CreatedAt = s.CreatedAt
        };
    }

    public async Task<(bool Success, string Message)> DeleteSupplierAsync(Guid id, Guid branchId)
    {
        var s = await _context.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == id && x.BranchId == branchId);
        if (s == null)
            throw new KeyNotFoundException($"No supplier with ID: {id}");

        var debt = await _supplierDebtService.CalculateCurrentDebtAsync(id);
        if (debt.CurrentDebt > 0)
            throw new BusinessException($"Nhà cung cấp '{s.SupplierName}' đang còn nợ {debt.CurrentDebt:N0}đ. Vui lòng thanh toán hết công nợ trước khi xóa.");

        var hasDependencies = await _context.Products.AnyAsync(p => p.SupplierId == id) ||
                              await _context.PurchaseOrders.AnyAsync(po => po.SupplierId == id) ||
                              await _context.SupplierPayments.AnyAsync(sp => sp.SupplierId == id) ||
                              await _context.StockTickets.AnyAsync(st => st.SupplierId == id);

        if (hasDependencies)
        {
            s.Status = -1;
            var shortId = s.SupplierId.ToString()[..6];
            var newName = $"d_{shortId}_{s.SupplierName}";
            s.SupplierName = newName.Length > 150 ? newName[..150] : newName;

            if (!string.IsNullOrEmpty(s.ContactPhone))
            {
                var newPhone = $"d_{shortId}_{s.ContactPhone}";
                s.ContactPhone = newPhone.Length > 20 ? newPhone[..20] : newPhone;
            }
            if (!string.IsNullOrEmpty(s.ContactEmail))
            {
                var newEmail = $"d_{shortId}_{s.ContactEmail}";
                s.ContactEmail = newEmail.Length > 150 ? newEmail[..150] : newEmail;
            }
            if (!string.IsNullOrEmpty(s.TaxCode))
            {
                var newTaxCode = $"d_{shortId}_{s.TaxCode}";
                s.TaxCode = newTaxCode.Length > 50 ? newTaxCode[..50] : newTaxCode;
            }
        }
        else
        {
            _context.Suppliers.Remove(s);
        }

        await _context.SaveChangesAsync();
        return (true, "Supplier deleted successfully");
    }

    public async Task<(bool Success, string Message, short? NewStatus)> ToggleStatusAsync(Guid id, string status, Guid branchId, Guid? userId)
    {
        var s = await _context.Suppliers.FirstOrDefaultAsync(x => x.SupplierId == id && x.BranchId == branchId && x.Status != -1);
        if (s == null)
            throw new KeyNotFoundException($"No supplier with ID: {id}");

        var newStatus = ParseStatus(status);
        s.Status = newStatus;
        s.UpdatedBy = userId;
        s.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync();

        var message = newStatus == 1 ? "Đã kích hoạt lại nhà cung cấp." : "Đã ngừng hợp tác với nhà cung cấp.";
        return (true, message, newStatus);
    }
}
