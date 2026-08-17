using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MEP_Management_System.Models;
using MEP_Management_System.DTOs.SaleDTOs;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.SaleServices;

/// <summary>
/// Service xử lý logic nghiệp vụ cho Customers.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly MEPDatabaseContext _context;
    private static readonly Regex PhoneRegex = new(@"^0[3|5|7|8|9][0-9]{8}$", RegexOptions.Compiled);

    public CustomerService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<(List<CustomerDto> Items, int Total)> GetCustomersAsync(Guid branchId, string? search, string? group, int page, int pageSize)
    {
        var query = _context.Customers
            .Where(c => c.BranchId == branchId && (c.Status == 1 || c.Status == null))
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(c =>
                (c.CustomerName != null && c.CustomerName.Contains(search)) ||
                (c.PhoneNumber != null && c.PhoneNumber.Contains(search)) ||
                (c.Email != null && c.Email.Contains(search)));

        if (!string.IsNullOrEmpty(group))
            query = query.Where(c => c.Group == group);

        var total = await query.CountAsync();
        var customers = await query
            .OrderBy(c => c.CustomerName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        var items = customers.Select(MapToDto).ToList();
        return (items, total);
    }

    public async Task<List<CustomerDto>> GetAllCustomersAsync(Guid branchId, string? search)
    {
        var query = _context.Customers
            .Where(c => c.BranchId == branchId && (c.Status == 1 || c.Status == null))
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(c =>
                (c.CustomerName != null && c.CustomerName.Contains(search)) ||
                (c.PhoneNumber != null && c.PhoneNumber.Contains(search)) ||
                (c.Email != null && c.Email.Contains(search)));

        var customers = await query.OrderBy(c => c.CustomerName).ToListAsync();
        return customers.Select(MapToDto).ToList();
    }

    public async Task<CustomerDto?> GetCustomerAsync(Guid customerId, Guid branchId)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.BranchId == branchId);

        return customer == null ? null : MapToDto(customer);
    }

    public async Task<(CustomerDto? Customer, string? ErrorCode, string? ErrorMessage)> CreateCustomerAsync(
        Guid branchId, Guid createdBy, CreateCustomerRequest request)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            return (null, "MSG-85", "Customer name is required");

        if (!string.IsNullOrEmpty(request.PhoneNumber))
        {
            if (!PhoneRegex.IsMatch(request.PhoneNumber))
                return (null, "MSG-67", "Invalid phone number format. Expected: 0x-xxxx-xxxx (x = digit)");

            var exists = await _context.Customers.AnyAsync(c =>
                c.PhoneNumber == request.PhoneNumber && c.BranchId == branchId && (c.Status == 1 || c.Status == null));

            if (exists)
                return (null, "MSG-68", "Phone number already registered in this store");
        }

        var customer = new Customer
        {
            CustomerId = Guid.NewGuid(),
            CustomerName = request.CustomerName,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            Address = request.Address,
            Group = request.Group,
            Notes = request.Notes,
            Status = 1,
            BranchId = branchId,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        // Write system log
        await WriteSystemLogAsync(createdBy, "CREATE_CUSTOMER", "customers", "Customer",
            customer.CustomerId, $"Tạo khách hàng {customer.CustomerName} - SĐT: {customer.PhoneNumber ?? "—"}");

        return (MapToDto(customer), null, null);
    }

    public async Task<(CustomerDto? Customer, string? ErrorCode, string? ErrorMessage)> UpdateCustomerAsync(
        Guid customerId, Guid branchId, UpdateCustomerRequest request)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.BranchId == branchId);

        if (customer == null)
            return (null, "MSG-01", "Customer not found");

        if (!string.IsNullOrEmpty(request.PhoneNumber) && request.PhoneNumber != customer.PhoneNumber)
        {
            if (!PhoneRegex.IsMatch(request.PhoneNumber))
                return (null, "MSG-67", "Invalid phone number format");

            var exists = await _context.Customers.AnyAsync(c =>
                c.PhoneNumber == request.PhoneNumber && c.CustomerId != customerId &&
                c.BranchId == branchId && (c.Status == 1 || c.Status == null));

            if (exists)
                return (null, "MSG-68", "Phone number already registered in this store");
        }

        customer.CustomerName = request.CustomerName ?? customer.CustomerName;
        customer.PhoneNumber = request.PhoneNumber;
        customer.Email = request.Email ?? customer.Email;
        customer.Address = request.Address ?? customer.Address;
        customer.Group = request.Group ?? customer.Group;
        customer.Notes = request.Notes ?? customer.Notes;
        customer.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var userId = customer.CreatedBy ?? Guid.Empty;
        await WriteSystemLogAsync(userId, "UPDATE_CUSTOMER", "customers", "Customer",
            customer.CustomerId, $"Cập nhật khách hàng {customer.CustomerName} - SĐT: {customer.PhoneNumber ?? "—"}");

        return (MapToDto(customer), null, null);
    }

    public async Task<(bool Success, string? ErrorCode, string? ErrorMessage, bool SoftDelete)> DeleteCustomerAsync(
        Guid customerId, Guid branchId)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.BranchId == branchId);

        if (customer == null)
            return (false, "MSG-01", "Customer not found", false);

        var hasOrders = await _context.Orders.AnyAsync(o => o.CustomerId == customerId);
        var softDelete = hasOrders;

        if (hasOrders)
        {
            customer.Status = 0;
            if (!string.IsNullOrEmpty(customer.PhoneNumber))
            {
                var shortId = customer.CustomerId.ToString().Substring(0, 6);
                var newPhone = $"d_{shortId}_{customer.PhoneNumber}";
                if (newPhone.Length > 20) newPhone = newPhone.Substring(0, 20);
                customer.PhoneNumber = newPhone;
            }
            customer.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.Customers.Remove(customer);
        }

        await _context.SaveChangesAsync();

        var action = hasOrders ? "SOFT_DELETE_CUSTOMER" : "HARD_DELETE_CUSTOMER";
        var userId = customer.CreatedBy ?? Guid.Empty;
        await WriteSystemLogAsync(userId, action, "customers", "Customer",
            customer.CustomerId, $"Xóa khách hàng {customer.CustomerName} - SĐT: {customer.PhoneNumber ?? "—"} ({(hasOrders ? "soft-delete" : "hard-delete")})");

        return (true, null, null, softDelete);
    }

    public async Task<(List<CustomerOrderDto> Items, int Total)> GetCustomerOrdersAsync(
        Guid customerId, Guid branchId, int page, int pageSize)
    {
        var customerExists = await _context.Customers
            .AnyAsync(c => c.CustomerId == customerId && c.BranchId == branchId);

        if (!customerExists)
            return (new List<CustomerOrderDto>(), 0);

        var query = _context.Invoices
            .Where(i => i.Order.CustomerId == customerId && i.Status == "Completed")
            .Include(i => i.InvoiceItems)
            .AsQueryable();

        var total = await query.CountAsync();
        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        var items = invoices.Select(i => new CustomerOrderDto
        {
            InvoiceId = i.InvoiceId,
            InvoiceCode = i.InvoiceCode,
            TotalAmount = i.TotalAmount,
            PaymentMethod = i.PaymentMethod,
            Status = i.Status,
            CreatedAt = i.CreatedAt ?? DateTime.MinValue,
            ItemCount = i.InvoiceItems.Count
        }).ToList();

        return (items, total);
    }

    private static CustomerDto MapToDto(Customer c) => new()
    {
        CustomerId = c.CustomerId,
        CustomerName = c.CustomerName,
        PhoneNumber = c.PhoneNumber,
        Email = c.Email,
        Address = c.Address,
        Group = c.Group,
        Notes = c.Notes,
        LoyaltyPoints = c.LoyaltyPoints ?? 0,
        DebtLimit = c.DebtLimit,
        CurrentDebt = c.CurrentDebt,
        TotalSpent = c.TotalSpent,
        OrderCount = c.OrderCount,
        LastVisit = c.LastVisit,
        MembershipTier = c.MembershipTier,
        Status = c.Status ?? 1,
        CreatedAt = c.CreatedAt ?? DateTime.UtcNow,
        UpdatedAt = c.UpdatedAt,
        BranchId = c.BranchId,
        CreatedBy = c.CreatedBy
    };

    private async Task WriteSystemLogAsync(Guid userId, string action, string tableName,
        string entityType, Guid entityId, string description)
    {
        try
        {
            var log = new SystemLog
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                Action = action,
                TableName = tableName,
                EntityType = entityType,
                EntityId = entityId,
                Description = description,
                Level = "INFO",
                Source = "CustomerService",
                Timestamp = DateTime.UtcNow
            };
            _context.SystemLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch { /* Silent */ }
    }
}
