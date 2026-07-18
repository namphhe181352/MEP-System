using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/invoices")]
[HasPermission(PermissionConstants.SALE_VIEW)]
[Tags("Quản lý bán hàng (Sale)")]
public class InvoicesController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;
    private readonly IMapper _mapper;
    private readonly MepNotificationService _notifications;

    private readonly IOrderDiscountService _orderDiscountService;
    private readonly ILogger<InvoicesController> _logger;
    public InvoicesController(
       Models.MEPDatabaseContext context,
       IMapper mapper,
       MepNotificationService notifications,
       IOrderDiscountService orderDiscountService,
       ILogger<InvoicesController> logger)
    {
        _context = context;
        _mapper = mapper;
        _notifications = notifications;
        _orderDiscountService = orderDiscountService;
        _logger = logger;
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

        // Count query first (lightweight)
        var countQuery = _context.Orders.Where(o => o.BranchId == branchId);
        if (!string.IsNullOrEmpty(status))
            countQuery = countQuery.Where(o => o.Invoices.Any(i => i.Status == status));
        if (dateFrom.HasValue)
            countQuery = countQuery.Where(o => o.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            countQuery = countQuery.Where(o => o.CreatedAt <= dateTo.Value);
        if (customerId.HasValue)
            countQuery = countQuery.Where(o => o.CustomerId == customerId.Value);
        if (staffId.HasValue)
            countQuery = countQuery.Where(o => o.UserId == staffId.Value);
        var total = await countQuery.CountAsync();

        // Projection query - only fetch needed fields (no entity tracking, no over-fetching)
        var orderIds = await _context.Orders
            .AsNoTracking()
            .Where(o => o.BranchId == branchId)
            .Where(o => !string.IsNullOrEmpty(status) ? o.Invoices.Any(i => i.Status == status) : true)
            .Where(o => dateFrom.HasValue ? o.CreatedAt >= dateFrom.Value : true)
            .Where(o => dateTo.HasValue ? o.CreatedAt <= dateTo.Value : true)
            .Where(o => customerId.HasValue ? o.CustomerId == customerId.Value : true)
            .Where(o => staffId.HasValue ? o.UserId == staffId.Value : true)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => o.OrderId)
            .ToListAsync();

        // Fetch invoices + related data in single optimized query
        var invoiceData = await _context.Invoices
            .AsNoTracking()
            .Where(i => orderIds.Contains(i.OrderId))
            .Select(i => new
            {
                Invoice = i,
                i.OrderId,
                i.Order.UserId,
                UserName = i.Order.User != null ? i.Order.User.FullName : null,
                i.Order.CustomerId,
                CustomerName = i.Order.Customer != null ? i.Order.Customer.CustomerName : null,
                DiscountAmount = i.DiscountAmount,
                DiscountPercent = i.DiscountPercent,
                i.InvoiceItems,
                i.Payments
            })
            .ToListAsync();

        var items = invoiceData.Select(data =>
        {
            var dto = _mapper.Map<InvoiceDto>(data.Invoice);
            dto.OrderId = data.OrderId;
            dto.UserId = data.UserId;
            dto.UserName = data.UserName;
            dto.CustomerId = data.CustomerId;
            dto.CustomerName = data.CustomerName;
            // AutoMapper tính TotalAmount từ Invoice.InvoiceItems nhưng khi dùng Select() trong EF,
            // navigation property không được load → InvoiceItems là null → TotalAmount = 0.
            // Nên phải tính thủ công từ data.InvoiceItems (đã được EF populate riêng).
            if (data.InvoiceItems != null && data.InvoiceItems.Any())
            {
                var subtotal = data.InvoiceItems.Sum(ii => ii.Quantity * ii.SellPrice);
                dto.Subtotal = subtotal;
                dto.DiscountAmount = data.DiscountAmount;
                dto.DiscountPercent = data.DiscountPercent;
                dto.TotalAmount = subtotal - data.DiscountAmount;
                dto.Items = _mapper.Map<List<DTOs.SaleDTOs.InvoiceItemDto>>(data.InvoiceItems);
            }
            if (data.Payments != null && data.Payments.Any())
            {
                dto.PaymentMethod = data.Payments.Count > 1
                    ? "Combined"
                    : data.Payments.First().Method.ToString();
            }
            return dto;
        }).ToList();

        return Ok(new PageResultDto<InvoiceDto> { Items = items, PageNumber = page, PageSize = pageSize, TotalCount = total });
    }

    // POST /api/pos/invoices
    [HttpPost]
    public async Task<ActionResult<InvoiceDto>> CreateInvoice([FromBody] CreateInvoiceRequest request)
    {
        var branchId = GetBranchId();
        var userId = GetUserId();

        // Validate: user must have an open shift before creating invoice
        var openShift = await _context.Shifts
            .FirstOrDefaultAsync(s => s.UserId == userId && s.BranchId == branchId && s.Status == "OPEN");
        if (openShift == null)
            return BadRequest(ApiErrorResponse.Create("MSG-76", "Vui lòng mở ca bán trước khi tạo đơn hàng"));

        if (request.CustomerId.HasValue)
        {
            var customerExists = await _context.Customers.AnyAsync(c => c.CustomerId == request.CustomerId.Value);
            if (!customerExists)
            {
                return BadRequest(ApiErrorResponse.Create("MSG-01", "Không tìm thấy khách hàng"));
            }
        }

        // Remove draft limit as it breaks checkout flow when Draft invoices leak.
        var now = DateTime.UtcNow;
        var todayPrefixInv = $"INV-{now.Year}-";
        var todayPrefixOrder = $"POS-{now:yyyyMMdd}-";

        var existingInvCodes = await _context.Invoices
            .Where(i => i.InvoiceCode != null && i.InvoiceCode.StartsWith(todayPrefixInv))
            .Select(i => i.InvoiceCode)
            .ToListAsync();
        var existingOrderCodes = await _context.Orders
            .Where(o => o.OrderCode != null && o.OrderCode.StartsWith(todayPrefixOrder))
            .Select(o => o.OrderCode)
            .ToListAsync();

        int GetSeq(string prefix, string code) =>
            int.TryParse(code.Substring(prefix.Length), out var n) ? n : 0;

        var maxInvSeq = existingInvCodes.Count == 0 ? 0 : existingInvCodes.Max(c => GetSeq(todayPrefixInv, c));
        var maxOrderSeq = existingOrderCodes.Count == 0 ? 0 : existingOrderCodes.Max(c => GetSeq(todayPrefixOrder, c));
        var nextSeq = Math.Max(maxInvSeq, maxOrderSeq) + 1;

        var invCode = CodeGenerator.GenerateInvoiceCode(nextSeq, now);
        var orderCode = CodeGenerator.GenerateOrderCode(nextSeq, now);

        var order = new Models.Order
        {
            OrderId = Guid.NewGuid(),
            OrderCode = orderCode,
            BranchId = branchId,
            CustomerId = request.CustomerId,
            UserId = userId,
            Status = "Draft",
            CreatedAt = now,
            Note = request.Note
        };

        var invoice = new Models.Invoice
        {
            InvoiceId = Guid.NewGuid(),
            InvoiceCode = invCode,
            OrderId = order.OrderId,
            UserId = userId,
            Status = "Draft",
            CreatedAt = now,
            SubTotalAmount = 0,
            DiscountAmount = 0,
            TotalAmount = 0
        };

        _context.Orders.Add(order);
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();

        var dto = _mapper.Map<InvoiceDto>(invoice);
        dto.OrderId = order.OrderId;
        dto.UserId = userId;

        // Real-time: notify branch staff that a new invoice was created
        _ = _notifications.NotifyBranchInvoiceCreated(branchId, new
        {
            invoice.InvoiceId,
            invoice.InvoiceCode,
            invoice.Status,
            invoice.TotalAmount,
            invoice.CreatedAt,
            UserId = userId
        });

        return CreatedAtAction(nameof(GetInvoice), new { invoiceId = invoice.InvoiceId }, dto);
    }

    // GET /api/pos/invoices/{invoiceId}
    [HttpGet("{invoiceId:guid}")]
    public async Task<ActionResult<InvoiceDto>> GetInvoice(Guid invoiceId)
    {
        var dto = await InvoiceService.GetInvoiceByIdAsync(_context, _mapper, _orderDiscountService, invoiceId, User);

        if (dto == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Invoice not found"));

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
            .Include(b => b.Product).ThenInclude(p => p.ProductConversionUnits)
            .FirstOrDefaultAsync(b => b.BranchId == branchId && b.ProductId == request.ProductId && b.IsActive == 1);

        if (bp == null) return NotFound(ApiErrorResponse.Create("MSG-59", "Product not found or inactive"));

        // UOM: Xác định đơn vị và hệ số quy đổi
        string selectedUnit = request.SelectedUnit ?? bp.Product.BaseUnit;
        decimal convertValue = 1m;
        decimal unitPrice = request.UnitPrice ?? bp.SellPrice;

        // Nếu chọn đơn vị quy đổi
        if (!string.IsNullOrEmpty(request.SelectedUnit) && request.SelectedUnit != bp.Product.BaseUnit)
        {
            var convUnit = bp.Product.ProductConversionUnits
                .FirstOrDefault(u => u.UnitName == request.SelectedUnit);

            if (convUnit == null)
                return BadRequest(ApiErrorResponse.Create("MSG-59", $"Đơn vị '{request.SelectedUnit}' không hợp lệ cho sản phẩm này"));

            convertValue = convUnit.ConvertValue;
            unitPrice = convUnit.Price; // Giá của đơn vị quy đổi
        }

        // Tính actual quantity (đã quy đổi về BaseUnit)
        var actualQuantity = Math.Round(request.Quantity * convertValue, 2);

        // P1 Fix: Sử dụng transaction để tránh race condition
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Re-fetch stock trong transaction để tránh race condition
            var currentStock = await _context.BranchProducts
                .Where(b => b.BranchProductId == bp.BranchProductId)
                .Select(b => b.AvailableStock)
                .FirstAsync();

            if (currentStock < actualQuantity)
                return BadRequest(ApiErrorResponse.Create("MSG-59", $"Insufficient stock. Available: {currentStock} {bp.Product.BaseUnit}"));

            // Validate floor price with proper unit conversion scaling
            // effectiveCost = costPrice * convertValue / baseConvertValue
            var baseConvUnit = bp.Product.ProductConversionUnits.FirstOrDefault(u => u.UnitName == bp.Product.BaseUnit);
            var baseConvertValue = baseConvUnit?.ConvertValue ?? 1m;
            var effectiveCost = bp.CostPrice * convertValue / baseConvertValue;

            if (unitPrice < effectiveCost) return BadRequest(ApiErrorResponse.Create("MSG-63", "Price override below floor price"));

            var existingItem = invoice.InvoiceItems
                .FirstOrDefault(ii => ii.BranchProductId == bp.BranchProductId && ii.SellPrice == unitPrice && ii.SelectedUnit == selectedUnit);
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
                    SellPrice = unitPrice,
                    SelectedUnit = selectedUnit,
                    ConvertValue = convertValue
                };
                _context.InvoiceItems.Add(item);
                invoice.InvoiceItems.Add(item);
            }

            await _context.SaveChangesAsync();
            invoice.TotalAmount = invoice.InvoiceItems.Sum(ii => ii.Quantity * ii.SellPrice);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error adding item to invoice {InvoiceId}", invoiceId);
            return StatusCode(500, ApiErrorResponse.Create("MSG-99", "Đã xảy ra lỗi khi thêm sản phẩm"));
        }

        var dto = _mapper.Map<InvoiceItemDto>(invoice.InvoiceItems.First(ii => ii.BranchProductId == bp.BranchProductId && ii.SellPrice == unitPrice && ii.SelectedUnit == selectedUnit));
        return CreatedAtAction(null, dto);
    }

    // POST /api/pos/invoices/{invoiceId}/items/scan
    [HttpPost("{invoiceId:guid}/items/scan")]
    public async Task<ActionResult<InvoiceItemDto>> ScanItem(Guid invoiceId, [FromBody] ScanItemRequest request)
    {
        var branchId = GetBranchId();
        var bp = await _context.BranchProducts
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.BranchId == branchId && b.Product.Barcode == request.Barcode && b.IsActive == 1);
        if (bp == null) return NotFound(ApiErrorResponse.Create("MSG-59", "Product not found by barcode"));

        var addReq = new AddItemRequest { ProductId = bp.ProductId, Quantity = request.Quantity > 0 ? request.Quantity : 1 };
        return await AddItem(invoiceId, addReq);
    }

    // POST /api/pos/invoices/{invoiceId}/items/search
    [HttpPost("{invoiceId:guid}/items/search")]
    public async Task<ActionResult<List<ProductPriceDto>>> SearchProduct([FromBody] SearchProductRequest request)
    {
        var branchId = GetBranchId();
        var products = await _context.BranchProducts
            .Include(b => b.Product).ThenInclude(p => p.ProductConversionUnits)
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
            Unit = b.Product.BaseUnit,
            AvailableStock = b.AvailableStock,
            ConversionUnits = b.Product.ProductConversionUnits
                .Where(c => c.DirectSale == 1) // Chỉ lấy đơn vị bán trực tiếp
                .Select(c => new ConversionUnitDto
                {
                    ConversionId = c.ConversionId,
                    UnitName = c.UnitName,
                    ConvertValue = c.ConvertValue,
                    Price = c.Price,
                    DirectSale = c.DirectSale == 1
                }).ToList()
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
        if (invoice.Status != "Draft" && invoice.Status != "OnHold")
            return BadRequest(ApiErrorResponse.Create("MSG-78", "Only draft or on-hold invoices can be cancelled"));

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
            .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp.Product)
            .Include(i => i.Payments)
            .Include(i => i.Order).ThenInclude(o => o.Customer)
            .Include(i => i.Order).ThenInclude(o => o.User)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);

        if (invoice == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Invoice not found"));
        if (invoice.Status != "Draft") return BadRequest(ApiErrorResponse.Create("MSG-78", "Invoice already finalized"));
        if (!invoice.InvoiceItems.Any()) return BadRequest(ApiErrorResponse.Create("MSG-61", "Invoice must have at least 1 item"));

        // Tính tier discount TRƯỚC validation
        var subTotal = invoice.InvoiceItems.Sum(ii => ii.Quantity * ii.SellPrice);
        var (discountPercent, discountAmount, totalAmount) = await _orderDiscountService.CalculateDiscountAsync(subTotal);

        var hasPending = invoice.Payments.Any(p => p.Status == "Pending");
        if (hasPending)
        {
            return BadRequest(ApiErrorResponse.Create("payment_not_confirmed", "Thanh toán chuyển khoản chưa được xác nhận. Vui lòng kiểm tra lại."));
        }

        var totalPaid = invoice.Payments
            .Where(p => p.Status == "Confirmed")
            .Sum(p => p.Amount);

        if (totalPaid < totalAmount)
        {
            return BadRequest(ApiErrorResponse.Create("MSG-82", $"Thanh toán chưa đủ. Cần thanh toán {totalAmount:N0} VNĐ, nhưng mới nhận {totalPaid:N0} VNĐ."));
        }

        // Validate stock
        foreach (var item in invoice.InvoiceItems)
        {
            var actualQty = item.Quantity * (item.ConvertValue ?? 1m);
            if (item.BranchProduct.AvailableStock < actualQty)
                return BadRequest(ApiErrorResponse.Create("MSG-59", $"Insufficient stock for product. Available: {item.BranchProduct.AvailableStock}"));
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Deduct stock - POS bán lẻ: trừ cả ActualStock và AvailableStock
            // (AvailableStock là stored field, không phải computed property)
            // Fix concurrent checkout with atomic update
            foreach (var item in invoice.InvoiceItems)
            {
                var actualQty = item.Quantity * (item.ConvertValue ?? 1m);
                var rowsAffected = await _context.BranchProducts
                    .Where(bp => bp.BranchProductId == item.BranchProductId && bp.AvailableStock >= actualQty && bp.ActualStock >= actualQty)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(b => b.ActualStock, b => b.ActualStock - actualQty)
                        .SetProperty(b => b.AvailableStock, b => b.AvailableStock - actualQty));

                if (rowsAffected == 0)
                {
                    await transaction.RollbackAsync();
                    return BadRequest(ApiErrorResponse.Create("MSG-59", $"Thanh toán thất bại. Sản phẩm trong giỏ hàng đã hết hàng hoặc không đủ số lượng. Available: {item.BranchProduct.AvailableStock}"));
                }
            }

            invoice.Status = "Completed";
            invoice.Order.Status = "Completed";

            // Lưu các giá trị đã tính ở trên vào invoice
            invoice.SubTotalAmount = subTotal;
            invoice.DiscountPercent = discountPercent;
            invoice.DiscountAmount = discountAmount;
            invoice.TotalAmount = totalAmount;

            // Update customer stats
            if (invoice.Order.Customer != null)
            {
                invoice.Order.Customer.TotalSpent = (invoice.Order.Customer.TotalSpent ?? 0) + invoice.TotalAmount;
                invoice.Order.Customer.OrderCount = (invoice.Order.Customer.OrderCount ?? 0) + 1;
                invoice.Order.Customer.LastVisit = DateTime.UtcNow;
                invoice.Order.Customer.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }

        var dto = _mapper.Map<InvoiceDto>(invoice);
        dto.OrderId = invoice.OrderId;
        dto.UserId = invoice.UserId;
        dto.UserName = invoice.Order?.User?.FullName;

        if (invoice.Payments != null && invoice.Payments.Any())
        {
            dto.PaymentMethod = invoice.Payments.Count > 1 
                ? "Combined" 
                : invoice.Payments.First().Method.ToString();
        }

        // Real-time: query items fresh (not from change-tracked entity) for notification payload
        var stockUpdates = await _context.InvoiceItems
            .Include(ii => ii.BranchProduct).ThenInclude(bp => bp.Product)
            .Where(ii => ii.InvoiceId == invoiceId)
            .Select(ii => new
            {
                ii.BranchProductId,
                ProductId = ii.BranchProduct.ProductId,
                ProductName = ii.BranchProduct.Product.ProductName,
                NewAvailableStock = ii.BranchProduct.AvailableStock
            })
            .ToListAsync();
        _ = _notifications.NotifyBranchInvoiceFinalized(branchId, invoice.InvoiceId, stockUpdates.Cast<object>().ToList());

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

    // POST /api/pos/invoices/{invoiceId}/hold
    /// <summary>Treo hóa đơn (Hold Invoice) - lưu trạng thái giỏ hàng hiện tại.</summary>
    [HttpPost("{invoiceId:guid}/hold")]
    public async Task<ActionResult<InvoiceDto>> HoldInvoice(Guid invoiceId, [FromBody] HoldInvoiceRequest request)
    {
        var branchId = GetBranchId();
        var invoice = await _context.Invoices
            .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp.Product)
            .Include(i => i.Order).ThenInclude(o => o.Customer)
            .Include(i => i.Order).ThenInclude(o => o.User)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);

        if (invoice == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Invoice not found"));
        if (invoice.Status != "Draft") return BadRequest(ApiErrorResponse.Create("MSG-78", "Only draft invoices can be held"));

        // BR-87: Max 20 held invoices per branch
        var heldCount = await _context.Invoices
            .Where(i => i.Order.BranchId == branchId && i.Status == "OnHold")
            .CountAsync();

        if (heldCount >= 20)
            return BadRequest(ApiErrorResponse.Create("MSG-71", "Đã đạt tối đa 20 đơn treo. Không thể treo thêm."));
        // BR-86: Save current cart state, change status to "OnHold"
        var now = DateTime.UtcNow;
        invoice.Status = "OnHold";
        invoice.HoldNote = request?.HoldNote;
        invoice.HeldAt = now;
        invoice.Order.Status = "OnHold";

        await _context.SaveChangesAsync();

        var dto = _mapper.Map<InvoiceDto>(invoice);
        dto.OrderId = invoice.OrderId;
        return Ok(dto);
    }

    // POST /api/pos/invoices/{invoiceId}/resume
    /// <summary>Khôi phục hóa đơn đang treo (Resume Invoice) - chuyển về Draft.</summary>
    [HttpPost("{invoiceId:guid}/resume")]
    public async Task<ActionResult<InvoiceDto>> ResumeInvoice(Guid invoiceId)
    {
        var branchId = GetBranchId();
        var invoice = await _context.Invoices
            .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp.Product)
            .Include(i => i.Order).ThenInclude(o => o.Customer)
            .Include(i => i.Order).ThenInclude(o => o.User)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);

        if (invoice == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Invoice not found"));
        if (invoice.Status != "OnHold") return BadRequest(ApiErrorResponse.Create("MSG-78", "Only on-hold invoices can be resumed"));

        // BR-88: Check not expired (24h)
        if (invoice.HeldAt.HasValue && invoice.HeldAt.Value.AddHours(24) < DateTime.UtcNow)
            return BadRequest(ApiErrorResponse.Create("MSG-78", "Hóa đơn treo đã quá 24 giờ và tự động hủy."));

        // BR-89: Validate real-time stock for all items when resuming
        foreach (var item in invoice.InvoiceItems)
        {
            if (item.BranchProduct.AvailableStock < item.Quantity)
                return BadRequest(ApiErrorResponse.Create("MSG-59",
                    $"Sản phẩm '{item.BranchProduct.Product?.ProductName}' không đủ tồn kho khi khôi phục. Hiện có: {item.BranchProduct.AvailableStock}"));
        }

        invoice.Status = "Draft";
        invoice.Order.Status = "Draft";
        // Clear hold metadata
        invoice.HoldNote = null;
        invoice.HeldAt = null;

        await _context.SaveChangesAsync();

        var dto = _mapper.Map<InvoiceDto>(invoice);
        dto.OrderId = invoice.OrderId;
        return Ok(dto);
    }

    // GET /api/pos/invoices/on-hold
    /// <summary>Lấy danh sách hóa đơn đang treo (chưa hết hạn 7 ngày).</summary>
    [HttpGet("on-hold")]
    public async Task<ActionResult<PageResultDto<InvoiceDto>>> GetOnHoldInvoices(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var branchId = GetBranchId();
        var cutoff = DateTime.UtcNow.AddDays(-7); // 7 days expiry

        // Count first
        var total = await _context.Invoices
            .Where(i => i.Order.BranchId == branchId && i.Status == "OnHold" && i.HeldAt != null && i.HeldAt > cutoff)
            .CountAsync();

        // Projection with AsNoTracking - only fetch needed fields
        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.Order.BranchId == branchId && i.Status == "OnHold" && i.HeldAt != null && i.HeldAt > cutoff)
            .OrderByDescending(i => i.HeldAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(i => new
            {
                Invoice = i,
                i.OrderId,
                i.UserId,
                UserName = i.Order.User != null ? i.Order.User.FullName : null,
                CustomerName = i.Order.Customer != null ? i.Order.Customer.CustomerName : null,
                i.InvoiceItems,
                i.Payments
            })
            .ToListAsync();

        var dtos = invoices.Select(data =>
        {
            var dto = _mapper.Map<InvoiceDto>(data.Invoice);
            dto.OrderId = data.OrderId;
            dto.UserId = data.UserId;
            dto.UserName = data.UserName;
            return dto;
        }).ToList();

        return Ok(new PageResultDto<InvoiceDto>
        {
            Items = dtos,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }
}

public class SearchProductRequest
{
    public string? Keyword { get; set; }
}
