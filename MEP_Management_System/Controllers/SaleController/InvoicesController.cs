using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.Models;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Services;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/invoices")]
[Authorize(Roles = "SalesStaff,Owner")]
public class InvoicesController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;
    private readonly IMapper _mapper;

    public InvoicesController(Models.MEPDatabaseContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found in token"));

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? throw new Exception("userId not found"));

    // GET /api/pos/invoices
    [HttpGet]
    public async Task<ActionResult<PageResultDto<InvoiceDto>>> GetInvoices(
        [FromQuery] string? status, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] Guid? customerId, [FromQuery] Guid? staffId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var branchId = GetBranchId();
        var query = _context.Orders
            .Where(o => o.BranchId == branchId)
            .Include(o => o.Invoices).ThenInclude(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp.Product)
            .Include(o => o.Customer)
            .Include(o => o.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(o => o.Invoices.Any(i => i.Status == status));
        if (dateFrom.HasValue)
            query = query.Where(o => o.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(o => o.CreatedAt <= dateTo.Value);
        if (customerId.HasValue)
            query = query.Where(o => o.CustomerId == customerId.Value);
        if (staffId.HasValue)
            query = query.Where(o => o.UserId == staffId.Value);

        var total = await query.CountAsync();
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        var items = new List<InvoiceDto>();
        foreach (var order in orders)
        {
            foreach (var invoice in order.Invoices)
            {
                var dto = _mapper.Map<InvoiceDto>(invoice);
                dto.OrderId = order.OrderId;
                dto.UserId = order.UserId;
                items.Add(dto);
            }
        }

        return Ok(new PageResultDto<InvoiceDto> { Items = items, PageNumber = page, PageSize = pageSize, TotalCount = total });
    }

    // POST /api/pos/invoices
    [HttpPost]
    public async Task<ActionResult<InvoiceDto>> CreateInvoice([FromBody] CreateInvoiceRequest request)
    {
        var branchId = GetBranchId();
        var userId = GetUserId();

        var todayCount = await _context.Orders
            .Where(o => o.BranchId == branchId && o.CreatedAt.HasValue && o.CreatedAt.Value.Date == DateTime.UtcNow.Date)
            .CountAsync();

        var order = new Models.Order
        {
            OrderId = Guid.NewGuid(),
            OrderCode = CodeGenerator.GenerateOrderCode(todayCount + 1),
            BranchId = branchId,
            CustomerId = request.CustomerId,
            UserId = userId,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
            Note = request.Note
        };

        var invoice = new Models.Invoice
        {
            InvoiceId = Guid.NewGuid(),
            InvoiceCode = CodeGenerator.GenerateInvoiceCode(todayCount + 1),
            OrderId = order.OrderId,
            UserId = userId,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
            TotalAmount = 0
        };

        _context.Orders.Add(order);
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var dto = _mapper.Map<InvoiceDto>(invoice);
        dto.OrderId = order.OrderId;
        dto.UserId = userId;
        return CreatedAtAction(nameof(GetInvoice), new { invoiceId = invoice.InvoiceId }, dto);
    }

    // GET /api/pos/invoices/{invoiceId}
    [HttpGet("{invoiceId:guid}")]
    public async Task<ActionResult<InvoiceDto>> GetInvoice(Guid invoiceId)
    {
        var branchId = GetBranchId();
        var invoice = await _context.Invoices
            .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp.Product)
            .Include(i => i.Order).ThenInclude(o => o.Customer)
            .Include(i => i.Order).ThenInclude(o => o.User)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);

        if (invoice == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Invoice not found"));

        var dto = _mapper.Map<InvoiceDto>(invoice);
        dto.OrderId = invoice.OrderId;
        dto.UserId = invoice.UserId;
        return Ok(dto);
    }

    // POST /api/pos/invoices/{invoiceId}/items
    [HttpPost("{invoiceId:guid}/items")]
    public async Task<ActionResult<InvoiceItemDto>> AddItem(Guid invoiceId, [FromBody] AddItemRequest request)
    {
        var branchId = GetBranchId();
        var invoice = await _context.Invoices
            .Include(i => i.InvoiceItems)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);

        if (invoice == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Invoice not found"));
        if (invoice.Status != "Draft") return BadRequest(ApiErrorResponse.Create("MSG-60", "Cannot add items to a non-draft invoice"));

        var bp = await _context.BranchProducts
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.BranchId == branchId && b.ProductId == request.ProductId && b.IsActive == 1);

        if (bp == null) return NotFound(ApiErrorResponse.Create("MSG-59", "Product not found or inactive"));
        if (bp.AvailableStock < request.Quantity) return BadRequest(ApiErrorResponse.Create("MSG-59", $"Insufficient stock. Available: {bp.AvailableStock}"));

        var unitPrice = request.UnitPrice ?? bp.SellPrice;
        if (unitPrice < bp.CostPrice) return BadRequest(ApiErrorResponse.Create("MSG-63", "Price override below floor price"));

        var existingItem = invoice.InvoiceItems.FirstOrDefault(ii => ii.BranchProductId == bp.BranchProductId && ii.SellPrice == unitPrice);
        if (existingItem != null)
        {
            existingItem.Quantity += request.Quantity;
        }
        else
        {
            var item = new InvoiceItem
            {
                InvoiceItemId = Guid.NewGuid(),
                InvoiceId = invoiceId,
                BranchProductId = bp.BranchProductId,
                Quantity = request.Quantity,
                SellPrice = unitPrice
            };
            _context.InvoiceItems.Add(item);
        }

        invoice.TotalAmount = invoice.InvoiceItems.Sum(ii => ii.Quantity * ii.SellPrice);
        await _context.SaveChangesAsync();

        var dto = _mapper.Map<InvoiceItemDto>(existingItem ?? await _context.InvoiceItems.Include(ii => ii.BranchProduct).ThenInclude(b => b.Product).FirstAsync(ii => ii.InvoiceId == invoiceId && ii.BranchProductId == bp.BranchProductId));
        return CreatedAtAction(null, dto);
    }

    // POST /api/pos/invoices/{invoiceId}/items/scan
    [HttpPost("{invoiceId:guid}/items/scan")]
    public async Task<ActionResult<InvoiceItemDto>> ScanItem(Guid invoiceId, [FromBody] ScanItemRequest request)
    {
        var branchId = GetBranchId();
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Barcode == request.Barcode && p.Status == 1);
        if (product == null) return NotFound(ApiErrorResponse.Create("MSG-59", "Product not found by barcode"));

        var addReq = new AddItemRequest { ProductId = product.ProductId, Quantity = request.Quantity };
        return await AddItem(invoiceId, addReq);
    }

    // POST /api/pos/invoices/{invoiceId}/items/search
    [HttpPost("{invoiceId:guid}/items/search")]
    public async Task<ActionResult<List<ProductPriceDto>>> SearchProduct([FromBody] SearchProductRequest request)
    {
        var branchId = GetBranchId();
        var products = await _context.BranchProducts
            .Include(b => b.Product)
            .Where(b => b.BranchId == branchId && b.IsActive == 1 &&
                (request.Keyword == null || b.Product.ProductName.Contains(request.Keyword) || (b.Product.ProductCode != null && b.Product.ProductCode.Contains(request.Keyword))))
            .Take(20)
            .ToListAsync();

        var result = products.Select(b => new ProductPriceDto
        {
            ProductId = b.ProductId,
            ProductName = b.Product.ProductName,
            ProductCode = b.Product.ProductCode,
            Barcode = b.Product.Barcode,
            RetailPrice = b.SellPrice,
            FloorPrice = b.CostPrice,
            Unit = b.Product.BaseUnit
        }).ToList();

        return Ok(result);
    }

    // PATCH /api/pos/invoices/{invoiceId}/cancel
    [HttpPatch("{invoiceId:guid}/cancel")]
    public async Task<ActionResult> CancelInvoice(Guid invoiceId)
    {
        var branchId = GetBranchId();
        var invoice = await _context.Invoices
            .Include(i => i.Order)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);

        if (invoice == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Invoice not found"));
        if (invoice.Status != "Draft") return BadRequest(ApiErrorResponse.Create("MSG-78", "Only draft invoices can be cancelled"));

        invoice.Status = "Cancelled";
        invoice.Order.Status = "Cancelled";
        await _context.SaveChangesAsync();
        return Ok(new { message = "Invoice cancelled" });
    }

    // POST /api/pos/invoices/{invoiceId}/finalize
    [HttpPost("{invoiceId:guid}/finalize")]
    public async Task<ActionResult<InvoiceDto>> FinalizeInvoice(Guid invoiceId)
    {
        var branchId = GetBranchId();
        var invoice = await _context.Invoices
            .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct)
            .Include(i => i.Order).ThenInclude(o => o.Customer)
            .Include(i => i.Order).ThenInclude(o => o.User)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);

        if (invoice == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Invoice not found"));
        if (invoice.Status != "Draft") return BadRequest(ApiErrorResponse.Create("MSG-78", "Invoice already finalized"));
        if (!invoice.InvoiceItems.Any()) return BadRequest(ApiErrorResponse.Create("MSG-61", "Invoice must have at least 1 item"));

        // Validate stock
        foreach (var item in invoice.InvoiceItems)
        {
            if (item.BranchProduct.AvailableStock < item.Quantity)
                return BadRequest(ApiErrorResponse.Create("MSG-59", $"Insufficient stock for product. Available: {item.BranchProduct.AvailableStock}"));
        }

        // Deduct stock
        foreach (var item in invoice.InvoiceItems)
        {
            item.BranchProduct.AvailableStock -= item.Quantity;
            item.BranchProduct.ActualStock -= item.Quantity;
        }

        invoice.Status = "Completed";
        invoice.Order.Status = "Completed";
        invoice.TotalAmount = invoice.InvoiceItems.Sum(ii => ii.Quantity * ii.SellPrice);

        // Update customer stats
        if (invoice.Order.Customer != null)
        {
            invoice.Order.Customer.TotalSpent = (invoice.Order.Customer.TotalSpent ?? 0) + invoice.TotalAmount;
            invoice.Order.Customer.OrderCount = (invoice.Order.Customer.OrderCount ?? 0) + 1;
            invoice.Order.Customer.LastVisit = DateTime.UtcNow;
            invoice.Order.Customer.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        var dto = _mapper.Map<InvoiceDto>(invoice);
        dto.OrderId = invoice.OrderId;
        return Ok(dto);
    }

    // GET /api/pos/invoices/{invoiceId}/items
    [HttpGet("{invoiceId:guid}/items")]
    public async Task<ActionResult<List<InvoiceItemDto>>> GetItems(Guid invoiceId)
    {
        var branchId = GetBranchId();
        var invoice = await _context.Invoices
            .Include(i => i.Order)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);
        if (invoice == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Invoice not found"));

        var items = await _context.InvoiceItems
            .Include(ii => ii.BranchProduct).ThenInclude(b => b.Product)
            .Where(ii => ii.InvoiceId == invoiceId)
            .ToListAsync();

        return Ok(_mapper.Map<List<InvoiceItemDto>>(items));
    }
}

public class SearchProductRequest
{
    public string? Keyword { get; set; }
}
