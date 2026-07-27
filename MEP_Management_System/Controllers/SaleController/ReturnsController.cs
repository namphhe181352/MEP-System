using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.InventoryDTOs;
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
            .Include(r => r.Order).ThenInclude(o => o!.Invoice) // để map InvoiceCode
            .Include(r => r.Order).ThenInclude(o => o!.Customer) // để map CustomerName
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
            .Include(r => r.Order).ThenInclude(o => o!.Invoice)
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
                .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp!.Product).ThenInclude(p => p!.CategoryTaxonomy)
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

            // BR-80: kiểm tra cửa sổ thời gian đổi/trả từ branch settings
            var branch = invoice.Order?.Branch;
            var allowedDays = branch?.ReturnDaysAllowed ?? (int?)30; // default 30 days if not set
            var invoiceDate = invoice.CreatedAt ?? DateTime.UtcNow;
            if (allowedDays.HasValue && invoiceDate.AddDays(allowedDays.Value) < DateTime.UtcNow)
            {
                Console.WriteLine($"[Returns API 400]: Quá {allowedDays} ngày. InvoiceDate: {invoiceDate}, Now: {DateTime.UtcNow}");
                return BadRequest(ApiErrorResponse.Create("MSG-65", $"Đã quá thời hạn {allowedDays} ngày kể từ ngày mua. Không thể đổi trả."));
            }

            // Kiểm tra chưa có return đang xử lý
            var existing = await _context.ReturnOrders
                .AnyAsync(r => r.OrderId == invoice.OrderId && r.Status == "Pending");
            if (existing)
            {
                Console.WriteLine($"[Returns API 400]: Hóa đơn này đang có phiếu đổi trả đang xử lý (Pending).");
                return BadRequest(ApiErrorResponse.Create("MSG-66", "Hóa đơn này đang có phiếu đổi trả đang xử lý."));
            }

            // Kiểm tra có ít nhất một item trong hóa đơn được phép đổi/trả theo ReturnType
            var config = await _context.SystemConfigs
                .FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "CATEGORY_RETURN_POLICIES");

            List<CategoryPolicyDto>? categoryPolicies = null;
            if (config != null && !string.IsNullOrWhiteSpace(config.ConfigValue))
            {
                try
                {
                    categoryPolicies = JsonSerializer.Deserialize<List<CategoryPolicyDto>>(config.ConfigValue,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch { }
            }

            if (categoryPolicies == null || categoryPolicies.Count == 0)
            {
                Console.WriteLine("[Returns API 400]: Chưa có chính sách đổi/trả theo nhóm hàng.");
                return BadRequest(ApiErrorResponse.Create("MSG-80",
                    "Chưa có chính sách đổi/trả theo nhóm hàng. Vui lòng thiết lập trong phần Cài đặt trước khi thực hiện đổi/trả."));
            }

            var returnType = req.ReturnType?.ToUpper() ?? "REFUND";
            var hasEligibleItem = false;
            var ineligibleReasons = new List<string>();

            foreach (var invoiceItem in invoice.InvoiceItems)
            {
                var product = invoiceItem.BranchProduct?.Product;
                var categoryId = product?.CategoryTaxonomyId;

                // Tìm policy — ưu tiên CategoryId, fallback CategoryName
                CategoryPolicyDto? itemPolicy = null;
                if (categoryId.HasValue)
                {
                    itemPolicy = categoryPolicies.FirstOrDefault(c =>
                        c.CategoryId.HasValue && c.CategoryId.Value == categoryId.Value);
                }
                if (itemPolicy == null)
                {
                    var categoryName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName;
                    if (!string.IsNullOrEmpty(categoryName))
                    {
                        itemPolicy = categoryPolicies.FirstOrDefault(c =>
                            c.CategoryName != null && c.CategoryName.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
                    }
                }

                if (itemPolicy == null)
                {
                    var displayName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName ?? "(không xác định)";
                    ineligibleReasons.Add($"\"{product?.ProductName}\" (nhóm \"{displayName}\"): chưa có chính sách");
                    continue;
                }

                var policyAllowedDays = returnType == "EXCHANGE"
                    ? itemPolicy.ExchangeDays
                    : itemPolicy.ReturnDays;

                if (!policyAllowedDays.HasValue || policyAllowedDays.Value <= 0)
                {
                    var displayName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName ?? "(không xác định)";
                    ineligibleReasons.Add($"\"{product?.ProductName}\" (nhóm \"{displayName}\"): không được phép {(returnType == "EXCHANGE" ? "đổi" : "trả")}");
                    continue;
                }

                var invoiceDate2 = invoice.CreatedAt ?? DateTime.UtcNow;
                if (invoiceDate2.AddDays(policyAllowedDays.Value) >= DateTime.UtcNow)
                {
                    hasEligibleItem = true;
                    break;
                }
                else
                {
                    var displayName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName ?? "(không xác định)";
                    ineligibleReasons.Add($"\"{product?.ProductName}\" (nhóm \"{displayName}\"): đã quá hạn {policyAllowedDays} ngày");
                }
            }

            if (!hasEligibleItem)
            {
                Console.WriteLine($"[Returns API 400]: Không có sản phẩm nào được phép {returnType}.");
                return BadRequest(ApiErrorResponse.Create("MSG-80",
                    $"Hóa đơn này không có sản phẩm nào được phép {(returnType == "EXCHANGE" ? "đổi" : "trả")}. " +
                    string.Join("; ", ineligibleReasons.Take(3)) +
                    (ineligibleReasons.Count > 3 ? $" (và {ineligibleReasons.Count - 3} sản phẩm khác)" : "")));
            }

            var entity = new ReturnOrder
            {
                ReturnOrderId = Guid.NewGuid(),
                ReturnCode = await GenCodeAsync("RET"),
                OrderId = invoice.OrderId,
                UserId = userId,
                RefundMethod = req.RefundMethod?.ToUpper() ?? "CASH",
                ReturnType = returnType,
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
            .Include(ii => ii.BranchProduct).ThenInclude(bp => bp.Product).ThenInclude(p => p.CategoryTaxonomy)
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

        // Category-level policy check — mỗi item được đánh giá độc lập theo categoryId
        var product = originalItems.First().BranchProduct?.Product;
        var config = await _context.SystemConfigs
            .FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "CATEGORY_RETURN_POLICIES");

        if (config == null || string.IsNullOrWhiteSpace(config.ConfigValue))
        {
            // Không có chính sách nào được cấu hình → không nhóm hàng nào được phép đổi/trả
            return BadRequest(ApiErrorResponse.Create("MSG-80",
                "Chưa có chính sách đổi/trả theo nhóm hàng. Vui lòng thiết lập trong phần Cài đặt trước khi thực hiện đổi/trả."));
        }

        List<CategoryPolicyDto>? categoryPolicies = null;
        try
        {
            categoryPolicies = JsonSerializer.Deserialize<List<CategoryPolicyDto>>(config.ConfigValue,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { }

        if (categoryPolicies == null || categoryPolicies.Count == 0)
        {
            return BadRequest(ApiErrorResponse.Create("MSG-80",
                "Chưa có chính sách đổi/trả theo nhóm hàng. Vui lòng thiết lập trong phần Cài đặt trước khi thực hiện đổi/trả."));
        }

        // Bước 1: Match theo CategoryTaxonomyId (ưu tiên)
        CategoryPolicyDto? categoryPolicy = null;
        var categoryId = product?.CategoryTaxonomyId;

        if (categoryId.HasValue)
        {
            categoryPolicy = categoryPolicies.FirstOrDefault(c =>
                c.CategoryId.HasValue && c.CategoryId.Value == categoryId.Value);
        }

        // Bước 2: Fallback sang CategoryName nếu không match được bằng ID (dữ liệu legacy)
        if (categoryPolicy == null)
        {
            var categoryName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName;
            if (!string.IsNullOrEmpty(categoryName))
            {
                categoryPolicy = categoryPolicies.FirstOrDefault(c =>
                    c.CategoryName != null && c.CategoryName.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (categoryPolicy == null)
        {
            var displayName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName ?? "(không xác định)";
            return BadRequest(ApiErrorResponse.Create("MSG-80",
                $"Sản phẩm \"{product?.ProductName}\" thuộc nhóm \"{displayName}\" không được phép {(ret.ReturnType == "EXCHANGE" ? "đổi" : "trả")}."));
        }

        var allowedDays = ret.ReturnType == "EXCHANGE"
            ? categoryPolicy.ExchangeDays
            : categoryPolicy.ReturnDays;

        if (!allowedDays.HasValue || allowedDays.Value <= 0)
        {
            var displayName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName ?? "(không xác định)";
            return BadRequest(ApiErrorResponse.Create("MSG-80",
                $"Sản phẩm \"{product?.ProductName}\" thuộc nhóm \"{displayName}\" không được phép {(ret.ReturnType == "EXCHANGE" ? "đổi" : "trả")}."));
        }

        var invoiceDate = originalItems.First().Invoice?.CreatedAt ?? DateTime.UtcNow;
        if (invoiceDate.AddDays(allowedDays.Value) < DateTime.UtcNow)
        {
            var displayName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName ?? "(không xác định)";
            return BadRequest(ApiErrorResponse.Create("MSG-79",
                $"Sản phẩm \"{product?.ProductName}\" thuộc nhóm \"{displayName}\" đã quá thời hạn {allowedDays} ngày. Không thể đổi trả."));
        }

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

    // GET /api/pos/returns/invoice/{invoiceId}/eligible-items
    /// <summary>
    /// Trả về danh sách sản phẩm trong hóa đơn kèm trạng thái được phép đổi/trả.
    /// Mỗi item được đánh giá độc lập theo policy của category riêng.
    /// </summary>
    [HttpGet("invoice/{invoiceId:guid}/eligible-items")]
    public async Task<ActionResult<InvoiceEligibilityResponse>> GetEligibleItems(Guid invoiceId)
    {
        var branchId = GetBranchId();

        var invoice = await _context.Invoices
            .Include(i => i.Order)
            .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp!.Product).ThenInclude(p => p!.CategoryTaxonomy)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order!.BranchId == branchId);

        if (invoice == null)
            return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy hóa đơn."));

        // Đọc category policies
        List<CategoryPolicyDto>? categoryPolicies = null;
        var config = await _context.SystemConfigs
            .FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "CATEGORY_RETURN_POLICIES");

        if (config != null && !string.IsNullOrWhiteSpace(config.ConfigValue))
        {
            try
            {
                categoryPolicies = JsonSerializer.Deserialize<List<CategoryPolicyDto>>(config.ConfigValue,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { }
        }

        var invoiceDate = invoice.CreatedAt ?? DateTime.UtcNow;
        var items = new List<InvoiceItemEligibilityDto>();

        foreach (var invoiceItem in invoice.InvoiceItems)
        {
            var product = invoiceItem.BranchProduct?.Product;
            var categoryId = product?.CategoryTaxonomyId;
            var categoryName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName;

            // Tìm policy — ưu tiên CategoryId, fallback CategoryName
            CategoryPolicyDto? itemPolicy = null;
            if (categoryPolicies != null)
            {
                if (categoryId.HasValue)
                {
                    itemPolicy = categoryPolicies.FirstOrDefault(c =>
                        c.CategoryId.HasValue && c.CategoryId.Value == categoryId.Value);
                }
                if (itemPolicy == null && !string.IsNullOrEmpty(categoryName))
                {
                    itemPolicy = categoryPolicies.FirstOrDefault(c =>
                        c.CategoryName != null && c.CategoryName.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
                }
            }

            var eligibility = new InvoiceItemEligibilityDto
            {
                InvoiceItemId = invoiceItem.InvoiceItemId,
                ProductId = product?.ProductId ?? Guid.Empty,
                ProductName = product?.ProductName,
                ProductCode = product?.ProductCode,
                Barcode = product?.Barcode,
                Unit = product?.BaseUnit,
                Quantity = invoiceItem.Quantity,
                UnitPrice = invoiceItem.SellPrice,
                LineTotal = invoiceItem.Quantity * invoiceItem.SellPrice,
                CategoryId = categoryId,
                CategoryName = categoryName,
            };

            // Nếu không có policy → không được phép
            if (itemPolicy == null)
            {
                eligibility.RefundAllowed = false;
                eligibility.ExchangeAllowed = false;
                eligibility.RestrictionReason = string.IsNullOrEmpty(categoryName)
                    ? "Sản phẩm không thuộc nhóm hàng nào."
                    : $"Nhóm hàng \"{categoryName}\" chưa được cấu hình chính sách đổi/trả.";
            }
            else
            {
                // Refund
                if (itemPolicy.ReturnDays.HasValue && itemPolicy.ReturnDays.Value > 0)
                {
                    var refundDeadline = invoiceDate.AddDays(itemPolicy.ReturnDays.Value);
                    if (refundDeadline >= DateTime.UtcNow)
                    {
                        eligibility.RefundAllowed = true;
                        eligibility.RemainingRefundDays = (int)(refundDeadline - DateTime.UtcNow).TotalDays;
                    }
                }

                // Exchange
                if (itemPolicy.ExchangeDays.HasValue && itemPolicy.ExchangeDays.Value > 0)
                {
                    var exchangeDeadline = invoiceDate.AddDays(itemPolicy.ExchangeDays.Value);
                    if (exchangeDeadline >= DateTime.UtcNow)
                    {
                        eligibility.ExchangeAllowed = true;
                        eligibility.RemainingExchangeDays = (int)(exchangeDeadline - DateTime.UtcNow).TotalDays;
                    }
                }

                if (!eligibility.RefundAllowed && !eligibility.ExchangeAllowed)
                {
                    eligibility.RestrictionReason = $"Nhóm hàng \"{categoryName}\" không được phép đổi/trả hoặc đã quá hạn.";
                }
            }

            items.Add(eligibility);
        }

        return Ok(new InvoiceEligibilityResponse
        {
            InvoiceId = invoice.InvoiceId,
            InvoiceCode = invoice.InvoiceCode,
            InvoiceDate = invoice.CreatedAt,
            Items = items
        });
    }
}
