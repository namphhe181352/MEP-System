using System;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/customers")]
[HasPermission(PermissionConstants.CUSTOMER_VIEW)]
[Tags("Quản lý bán hàng (Sale)")]
public class CustomersController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;
    private readonly IMapper _mapper;

    public CustomersController(Models.MEPDatabaseContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    private static readonly Regex PhoneRegex = new(@"^0[3|5|7|8|9][0-9]{8}$", RegexOptions.Compiled);

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found in token"));

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? throw new Exception("userId not found"));

    // GET /api/pos/customers
    [HttpGet]
    public async Task<ActionResult<PageResultDto<CustomerDto>>> GetCustomers(
        [FromQuery] string? search, [FromQuery] string? group,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var branchId = GetBranchId();
        Console.WriteLine($"[DEBUG] GetCustomers - BranchId: {branchId}, search: '{search}', group: '{group}', totalInDB: {await _context.Customers.CountAsync()}");

        var query = _context.Customers.Where(c => c.BranchId == branchId).AsQueryable();

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

        return Ok(new PageResultDto<CustomerDto>
        {
            Items = customers.Select(_mapper.Map<CustomerDto>).ToList(),
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    // POST /api/pos/customers
    [HttpPost]
    public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
            return BadRequest(ApiErrorResponse.Create("MSG-85", "Customer name is required"));
        if (!string.IsNullOrEmpty(request.PhoneNumber))
        {
            if (!PhoneRegex.IsMatch(request.PhoneNumber))
                return BadRequest(ApiErrorResponse.Create("MSG-67", "Invalid phone number format. Expected: 0x-xxxx-xxxx (x = digit)"));
            var branchId = GetBranchId();
            var exists = await _context.Customers.AnyAsync(c => c.PhoneNumber == request.PhoneNumber && c.BranchId == branchId);
            if (exists) return Conflict(ApiErrorResponse.Create("MSG-68", "Phone number already registered in this store"));
        }

        var customer = _mapper.Map<Customer>(request);
        customer.CustomerId = Guid.NewGuid();
        customer.CreatedBy = GetUserId();
        customer.BranchId = GetBranchId();
        Console.WriteLine($"[DEBUG] CreateCustomer - BranchId: {customer.BranchId}, CreatedBy: {customer.CreatedBy}");

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, _mapper.Map<CustomerDto>(customer));
    }

    // PUT /api/pos/customers/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> UpdateCustomer(Guid id, [FromBody] UpdateCustomerRequest request)
    {
        var branchId = GetBranchId();
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == id && c.BranchId == branchId);
        if (customer == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Customer not found"));

        if (!string.IsNullOrEmpty(request.PhoneNumber) && request.PhoneNumber != customer.PhoneNumber)
        {
            if (!PhoneRegex.IsMatch(request.PhoneNumber))
                return BadRequest(ApiErrorResponse.Create("MSG-67", "Invalid phone number format"));
            var exists = await _context.Customers.AnyAsync(c => c.PhoneNumber == request.PhoneNumber && c.CustomerId != id && c.BranchId == branchId);
            if (exists) return Conflict(ApiErrorResponse.Create("MSG-68", "Phone number already registered in this store"));
        }

        _mapper.Map(request, customer);
        await _context.SaveChangesAsync();

        return Ok(_mapper.Map<CustomerDto>(customer));
    }

    // GET /api/pos/customers/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> GetCustomer(Guid id)
    {
        var branchId = GetBranchId();
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == id && c.BranchId == branchId);
        if (customer == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Customer not found"));
        return Ok(_mapper.Map<CustomerDto>(customer));
    }

    // GET /api/pos/customers/{id}/orders
    [HttpGet("{id:guid}/orders")]
    public async Task<ActionResult<PageResultDto<CustomerOrderDto>>> GetCustomerOrders(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var branchId = GetBranchId();
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == id && c.BranchId == branchId);
        if (customer == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Customer not found"));

        var query = _context.Invoices
            .Where(i => i.Order.CustomerId == id && i.Status == "Completed")
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

        return Ok(new PageResultDto<CustomerOrderDto>
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }
}
