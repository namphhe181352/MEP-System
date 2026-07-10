using System;
using System.Linq;
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
[Route("api/pos/returns")]
[HasPermission(PermissionConstants.SALE_VIEW)]
[Tags("Đổi trả hàng (Return Order)")]
public class ReturnsController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;
    private readonly IMapper _mapper;

    public ReturnsController(Models.MEPDatabaseContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found in token"));

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(userIdStr ?? throw new Exception("userId not found in token"));
    }

    private async Task<string> GenCodeAsync(string prefix)
    {
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var seq = await _context.ReturnOrders.CountAsync(r => r.CreatedAt >= today) + 1;
        return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{seq:D4}";
    }

    // GET /api/pos/returns
    [HttpGet]
    public async Task<ActionResult<PageResultDto<ReturnOrderDto>>> GetReturns(
        [FromQuery] Guid? invoiceId,
        [FromQuery] string? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var branchId = GetBranchId();
        var query = _context.ReturnOrders
            .Include(r => r.Order).ThenInclude(o => o!.Branch)
            .Include(r => r.Order).ThenInclude(o => o!.Invoices) // để map InvoiceCode
            .Include(r => r.User)
            .Include(r => r.ReturnItems).ThenInclude(ri => ri.BranchProduct).ThenInclude(bp => bp!.Product)
            .Where(r => r.Order!.BranchId == branchId)
            .AsQueryable();

        if (invoiceId.HasValue)
        {
            var relatedOrders = await _context.Invoices
                .Where(i => i.InvoiceId == invoiceId.Value)
                .Select(i => i.OrderId)
                .ToListAsync();
            query = query.Where(r => relatedOrders.Contains(r.OrderId));
        }
        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status.ToLower() == status.ToLower());
        if (dateFrom.HasValue)
            query = query.Where(r => r.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(r => r.CreatedAt <= dateTo.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PageResultDto<ReturnOrderDto>
        {
            Items = items.Select(r => _mapper.Map<ReturnOrderDto>(r)).ToList(),
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    // GET /api/pos/returns/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReturnOrderDto>> GetReturn(Guid id)
    {
        var branchId = GetBranchId();
        var r = await _context.ReturnOrders
            .Include(r => r.Order).ThenInclude(o => o!.Branch)
            .Include(r => r.Order).ThenInclude(o => o!.Invoices)
            .Include(r => r.User)
            .Include(r => r.ReturnItems).ThenInclude(ri => ri.BranchProduct).ThenInclude(bp => bp!.Product)
            .FirstOrDefaultAsync(r => r.ReturnOrderId == id && r.Order!.BranchId == branchId);

        if (r == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy phiếu đổi trả."));

        return Ok(_mapper.Map<ReturnOrderDto>(r));
    }

    // POST /api/pos/returns
    [HttpPost]
    public async Task<ActionResult<ReturnOrderDto>> CreateReturn([FromBody] CreateReturnOrderRequest req)
    {
        try
        {
            var branchId = GetBranchId();
            var userId = GetUserId();

            // BR-81: tìm hóa đơn gốc
            var invoice = await _context.Invoices
                .Include(i => i.Order).ThenInclude(o => o!.Branch)
                .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct)
                .FirstOrDefaultAsync(i => i.InvoiceId == req.InvoiceId && i.Order!.BranchId == branchId);

            if (invoice == null) 
            {
                Console.WriteLine("[Returns API 404]: Không tìm thấy hóa đơn gốc.");
                return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy hóa đơn gốc."));
            }
            if (invoice.Status != "Completed")
            {
                Console.WriteLine($"[Returns API 400]: Chỉ hóa đơn đã hoàn tất mới được đổi trả. Status hiện tại: {invoice.Status}");
                return BadRequest(ApiErrorResponse.Create("MSG-78", "Chỉ hóa đơn đã hoàn tất mới được đổi trả."));
            }

            // BR-80: kiểm tra cửa sổ 30 ngày
            var invoiceDate = invoice.CreatedAt ?? DateTime.UtcNow;
            if (invoiceDate.AddDays(30) < DateTime.UtcNow)
            {
                Console.WriteLine($"[Returns API 400]: Quá 30 ngày. InvoiceDate: {invoiceDate}, Now: {DateTime.UtcNow}");
                return BadRequest(ApiErrorResponse.Create("MSG-65", "Đã quá thời hạn 30 ngày kể từ ngày mua. Không thể đổi trả."));
            }

            // Kiểm tra chưa có return đang xử lý
            var existing = await _context.ReturnOrders
                .AnyAsync(r => r.OrderId == invoice.OrderId && r.Status == "Pending");
            if (existing)
            {
                Console.WriteLine($"[Returns API 400]: Hóa đơn này đang có phiếu đổi trả đang xử lý (Pending).");
                return BadRequest(ApiErrorResponse.Create("MSG-66", "Hóa đơn này đang có phiếu đổi trả đang xử lý."));
            }

            var entity = new ReturnOrder
            {
                ReturnOrderId = Guid.NewGuid(),
                ReturnCode = await GenCodeAsync("RET"),
                OrderId = invoice.OrderId,
                UserId = userId,
                RefundMethod = req.RefundMethod?.ToUpper() ?? "CASH",
                ReturnType = req.ReturnType?.ToUpper() ?? "REFUND",
                Status = "Pending",
                Note = req.Note,
                CreatedAt = DateTime.UtcNow
            };

            _context.ReturnOrders.Add(entity);
            await _context.SaveChangesAsync();

            var dto = _mapper.Map<ReturnOrderDto>(entity);
            return CreatedAtAction(nameof(GetReturn), new { id = entity.ReturnOrderId }, dto);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Returns API Error]: {ex}");
            return StatusCode(500, new { message = ex.Message, stackTrace = ex.StackTrace, innerException = ex.InnerException?.Message });
        }
    }

    // POST /api/pos/returns/{id}/items
    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<ReturnItemDto>> AddReturnItem(Guid id, [FromBody] AddReturnItemRequest req)
    {
        var branchId = GetBranchId();

        var ret = await _context.ReturnOrders
            .Include(r => r.Order)
            .FirstOrDefaultAsync(r => r.ReturnOrderId == id && r.Order!.BranchId == branchId);

        if (ret == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy phiếu đổi trả."));
        if (ret.Status != "Pending")
            return BadRequest(ApiErrorResponse.Create("MSG-78", "Chỉ phiếu đang chờ xử lý mới được thêm sản phẩm."));

        // Validate sản phẩm thuộc hóa đơn gốc
        var originalItems = await _context.InvoiceItems
            .Include(ii => ii.Invoice)
            .Include(ii => ii.BranchProduct)
            .Where(ii => ii.Invoice != null && ii.Invoice.OrderId == ret.OrderId && ii.BranchProduct!.ProductId == req.ProductId)
            .ToListAsync();

        if (!originalItems.Any())
            return BadRequest(ApiErrorResponse.Create("MSG-59", "Sản phẩm này không có trong hóa đơn gốc."));

        var branchProductId = originalItems.First().BranchProductId;

        var totalBought = originalItems.Sum(ii => ii.Quantity);
        var totalReturned = await _context.ReturnItems
            .Where(ri => ri.ReturnOrderId == id && ri.BranchProductId == branchProductId)
            .SumAsync(ri => ri.Quantity);

        if (totalReturned + req.Quantity > totalBought)
            return BadRequest(ApiErrorResponse.Create("MSG-66", $"Số lượng trả vượt quá số lượng đã mua. Tối đa: {totalBought - totalReturned}"));

        var avgPrice = originalItems.Sum(ii => ii.SellPrice * ii.Quantity) / totalBought;
        var grossRefundLine = req.Quantity * avgPrice;

        // Áp dụng chiết khấu theo tỷ lệ của hóa đơn gốc
        var originalInvoice = originalItems.First().Invoice;
        var invoiceSubtotal = originalItems.Sum(ii => ii.Quantity * ii.SellPrice);
        var discountRatio = invoiceSubtotal > 0 && originalInvoice != null
            ? Math.Min(1m, Math.Max(0m, originalInvoice.DiscountAmount / invoiceSubtotal))
            : 0m;
        var discountPortion = grossRefundLine * discountRatio;
        var refundLine = Math.Max(0, grossRefundLine - discountPortion);

        var returnItem = new ReturnItem
        {
            ReturnItemId = Guid.NewGuid(),
            ReturnOrderId = id,
            BranchProductId = branchProductId,
            Quantity = req.Quantity,
            RefundAmount = refundLine,
            Reason = req.Reason?.ToUpper() ?? "OTHER"
        };

        _context.ReturnItems.Add(returnItem);
        await _context.SaveChangesAsync();

        return Ok(_mapper.Map<ReturnItemDto>(returnItem));
    }

    // POST /api/pos/returns/{id}/finalize
    [HttpPost("{id:guid}/finalize")]
    public async Task<ActionResult<ReturnOrderDto>> FinalizeReturn(Guid id, [FromBody] FinalizeReturnRequest req)
    {
        var branchId = GetBranchId();

        var ret = await _context.ReturnOrders
            .Include(r => r.Order)
            .Include(r => r.ReturnItems)
            .FirstOrDefaultAsync(r => r.ReturnOrderId == id && r.Order!.BranchId == branchId);

        if (ret == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy phiếu đổi trả."));
        if (ret.Status != "Pending")
            return BadRequest(ApiErrorResponse.Create("MSG-78", "Phiếu đổi trả không ở trạng thái chờ xử lý."));
        if (!ret.ReturnItems.Any())
            return BadRequest(ApiErrorResponse.Create("MSG-61", "Phiếu đổi trả phải có ít nhất 1 sản phẩm."));

        // Tính tổng hoàn
        ret.RefundAmount = ret.ReturnItems.Sum(ri => ri.RefundAmount ?? 0);
        ret.Status = "Completed";
        if (req?.Note != null) ret.Note = req.Note;

        // Hoàn kho: tăng AvailableStock và ActualStock cho từng sản phẩm trả
        foreach (var ri in ret.ReturnItems)
        {
            var bp = await _context.BranchProducts.FindAsync(ri.BranchProductId);
            if (bp != null)
            {
                bp.AvailableStock += ri.Quantity;
                bp.ActualStock += ri.Quantity;
            }
        }

        // Nếu hoàn tiền mặt: tạo RefundRecord
        if (ret.RefundMethod == "CASH")
        {
            var customerId = ret.Order?.CustomerId;
            if (customerId != null && customerId != Guid.Empty)
            {
                var refund = new RefundRecord
                {
                    RefundId = Guid.NewGuid(),
                    RefundCode = $"RFD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
                    OrderId = ret.OrderId,
                    InvoiceId = (await _context.Invoices.FirstOrDefaultAsync(i => i.OrderId == ret.OrderId))?.InvoiceId,
                    CustomerId = customerId.Value,
                    UserId = ret.UserId,
                    RefundAmount = ret.RefundAmount,
                    RefundMethod = "CASH",
                    Status = "REFUNDED",
                    Reason = ret.Note,
                    ProcessedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                _context.RefundRecords.Add(refund);
            }
        }

        await _context.SaveChangesAsync();

        var dto = _mapper.Map<ReturnOrderDto>(ret);
        return Ok(dto);
    }

    // PATCH /api/pos/returns/{id}/cancel
    [HttpPatch("{id:guid}/cancel")]
    public async Task<ActionResult> CancelReturn(Guid id)
    {
        var branchId = GetBranchId();

        var ret = await _context.ReturnOrders
            .Include(r => r.Order)
            .FirstOrDefaultAsync(r => r.ReturnOrderId == id && r.Order!.BranchId == branchId);

        if (ret == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy phiếu đổi trả."));
        if (ret.Status != "Pending")
            return BadRequest(ApiErrorResponse.Create("MSG-78", "Chỉ phiếu đang chờ xử lý mới được hủy."));

        ret.Status = "Cancelled";
        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã hủy phiếu đổi trả." });
    }
}
