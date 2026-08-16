using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Hubs;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/returns")]
[Authorize]
[Tags("Đổi trả hàng (Return Order)")]
public class ReturnsController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;
    private readonly IMapper _mapper;
    private readonly IHubContext<PosHub> _posHub;
    private readonly VietQrService _vietQr;

    public ReturnsController(Models.MEPDatabaseContext context, IMapper mapper, IHubContext<PosHub> posHub, VietQrService vietQr)
    {
        _context = context;
        _mapper = mapper;
        _posHub = posHub;
        _vietQr = vietQr;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found in token"));

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(userIdStr ?? throw new Exception("userId not found in token"));
    }

    private async Task TryWriteSystemLog(Guid userId, string action, string tableName,
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
                Source = "ReturnsController",
                Timestamp = DateTime.UtcNow
            };
            _context.SystemLogs.Add(log);
            await _context.SaveChangesAsync();
        }
        catch { /* Silent */ }
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
            .Include(r => r.ReturnItems).ThenInclude(ri => ri.BranchProduct).ThenInclude(bp => bp!.Product).ThenInclude(p => p!.ProductConversionUnits)
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
            .Include(r => r.Order).ThenInclude(o => o!.Customer)
            .Include(r => r.User)
            .Include(r => r.ReturnItems).ThenInclude(ri => ri.BranchProduct).ThenInclude(bp => bp!.Product).ThenInclude(p => p!.ProductConversionUnits)
            .Include(r => r.ExchangeItems).ThenInclude(ei => ei.BranchProduct).ThenInclude(bp => bp!.Product)
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
                .Include(i => i.Order).ThenInclude(o => o!.Customer)
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
                // REFUND chỉ trả bằng TIỀN MẶT (bỏ qua method khác do client gửi).
                RefundMethod = returnType == "REFUND" ? "CASH" : (req.RefundMethod?.ToUpper() ?? "CASH"),
                ReturnType = returnType,
                Status = "Pending",
                Note = req.Note,
                CreatedAt = DateTime.UtcNow
            };

            _context.ReturnOrders.Add(entity);
            await _context.SaveChangesAsync();

            await TryWriteSystemLog(
                GetUserId(),
                "CREATE_RETURN_ORDER",
                "return_orders",
                "ReturnOrder",
                entity.ReturnOrderId,
                $"Tạo phiếu đổi trả {entity.ReturnCode} - {entity.RefundMethod ?? "—"}");

            var dto = _mapper.Map<ReturnOrderDto>(entity);
            return CreatedAtAction(nameof(GetReturn), new { id = entity.ReturnOrderId }, dto);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Returns API Error]: {ex}");
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo phiếu trả hàng" });
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

        // Áp dụng chiết khấu trả hàng branch-level (thay cho prorate chiết khấu của hóa đơn gốc)
        var returnDiscountPercent = await GetReturnDiscountPercent(GetBranchId());
        var discountRatio = returnDiscountPercent > 0m ? returnDiscountPercent / 100m : 0m;
        var discountPortion = grossRefundLine * discountRatio;
        var refundLine = Math.Max(0, grossRefundLine - discountPortion);

        // === FIX: Tự tra hệ số quy đổi (ConvertValue) từ dòng hóa đơn gốc ===
        // Trước đây tin tưởng req.ConversionRate (FE hay gửi thiếu -> mặc định 1 ->
        // BaseQuantity = Quantity, không quy về ĐVT cơ bản -> SL bảo hành lệch).
        // Giây tính từ InvoiceItem gốc:
        //  1) Pin đúng dòng nếu FE gửi InvoiceItemId (cùng SP có nhiều đơn vị trong 1 HĐ).
        //  2) Fallback req.ConversionRate.
        //  3) Fallback ConvertValue lớn nhất trong các dòng gốc của SP.
        var pinnedItem = req.InvoiceItemId.HasValue
            ? originalItems.FirstOrDefault(ii => ii.InvoiceItemId == req.InvoiceItemId.Value)
            : null;

        decimal convertValue = 1m;
        if (pinnedItem != null && pinnedItem.ConvertValue.HasValue && pinnedItem.ConvertValue.Value > 0)
            convertValue = pinnedItem.ConvertValue.Value;
        else if (req.ConversionRate > 0)
            convertValue = req.ConversionRate;
        else
        {
            var maxCv = originalItems.Select(ii => ii.ConvertValue ?? 0m).DefaultIfEmpty(0m).Max();
            convertValue = maxCv > 0 ? maxCv : 1m;
        }

        var returnItem = new ReturnItem
        {
            ReturnItemId = Guid.NewGuid(),
            ReturnOrderId = id,
            BranchProductId = branchProductId,
            Quantity = req.Quantity,
            ConversionRate = convertValue,
            BaseQuantity = req.Quantity * convertValue,
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
            .Include(r => r.Order).ThenInclude(o => o!.Branch)
            .Include(r => r.Order).ThenInclude(o => o!.Customer)
            .Include(r => r.Order).ThenInclude(o => o!.Invoice)
            .Include(r => r.User)
            .Include(r => r.ReturnItems).ThenInclude(ri => ri.BranchProduct).ThenInclude(bp => bp!.Product).ThenInclude(p => p!.ProductConversionUnits)
            .Include(r => r.ExchangeItems).ThenInclude(ei => ei.BranchProduct).ThenInclude(bp => bp!.Product)
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

        // Hoàn kho: EXCHANGE (doi hang ngang gia) → ton kho giu nguyen
        // REFUND (tra hang hoan tien) → tang ton kho
        if (ret.ReturnType == "REFUND")
        {
            var stockTicketReturn = new StockTicket
            {
                StockTicketId = Guid.NewGuid(),
                TicketCode = ret.ReturnCode,
                BranchId = ret.Order!.BranchId,
                UserId = GetUserId(),
                TicketType = "CUSTOMER_RETURN",
                Status = "COMPLETED",
                Notes = "Khách trả hàng " + ret.ReturnCode,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };
            _context.StockTickets.Add(stockTicketReturn);

            foreach (var ri in ret.ReturnItems)
            {
                var bp = await _context.BranchProducts.Include(b => b.Product).FirstOrDefaultAsync(b => b.BranchProductId == ri.BranchProductId);
                if (bp != null)
                {
                    var baseQty = ri.BaseQuantity > 0 ? ri.BaseQuantity : ri.Quantity;
                    var isDefective = string.Equals(ri.Reason, "DEFECTIVE", StringComparison.OrdinalIgnoreCase);

                    var ticketItem = new StockTicketItem
                    {
                        TicketItemId = Guid.NewGuid(),
                        StockTicketId = stockTicketReturn.StockTicketId,
                        BranchProductId = ri.BranchProductId,
                        SystemQuantity = bp.ActualStock,
                        ActualQuantity = bp.ActualStock + baseQty,
                        Discrepancy = baseQty,
                        UnitPrice = bp.CostPrice,
                        UnitName = bp.Product?.BaseUnit ?? ""
                    };
                    _context.StockTicketItems.Add(ticketItem);

                    bp.ActualStock += baseQty;
                    bp.AvailableStock += baseQty;

                    if (isDefective)
                    {
                        // Vẫn tạo WarrantyTicket để theo dõi bảo hành, nhưng không trừ tồn kho nữa (theo yêu cầu của user)
                        // bp.WarrantyQuantity += baseQty;

                        _context.WarrantyTickets.Add(new WarrantyTicket
                        {
                            WarrantyTicketId = Guid.NewGuid(),
                            ReturnItemId = ri.ReturnItemId,
                            BranchProductId = ri.BranchProductId,
                            ProductId = bp.ProductId,
                            Quantity = ri.Quantity,
                            ConversionRate = ri.ConversionRate > 0 ? ri.ConversionRate : 1,
                            BaseQuantity = baseQty,
                            Status = "PENDING_ASSIGN",
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = GetUserId()
                        });

                        _context.InventoryLedgers.Add(new InventoryLedger
                        {
                            LedgerId = Guid.NewGuid(),
                            BranchId = bp.BranchId,
                            BranchProductId = bp.BranchProductId,
                            DocType = "CUSTOMER_RETURN_DEFECTIVE",
                            DocId = ret.ReturnOrderId,
                            DocCode = ret.ReturnCode,
                            TransactionTime = DateTime.UtcNow,
                            QtyChange = baseQty, // Tồn vật lý TĂNG (hàng trả lại về kho)
                            UnitCostPrice = Math.Round(bp.CostPrice, 4),
                            TotalCostValue = Math.Round(baseQty * bp.CostPrice, 4),
                            StockAfter = bp.ActualStock,
                            AvgCostAfter = Math.Round(bp.CostPrice, 4),
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        _context.InventoryLedgers.Add(new InventoryLedger
                        {
                            LedgerId = Guid.NewGuid(),
                            BranchId = bp.BranchId,
                            BranchProductId = bp.BranchProductId,
                            DocType = "CUSTOMER_RETURN",
                            DocId = ret.ReturnOrderId,
                            DocCode = ret.ReturnCode,
                            TransactionTime = DateTime.UtcNow,
                            QtyChange = baseQty,
                            UnitCostPrice = Math.Round(bp.CostPrice, 4),
                            TotalCostValue = Math.Round(baseQty * bp.CostPrice, 4),
                            StockAfter = bp.ActualStock,
                            AvgCostAfter = Math.Round(bp.CostPrice, 4),
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }
        }
        else if (ret.ReturnType == "EXCHANGE" && ret.ExchangeItems.Any())
        {
            // === Đổi chênh lệch ===
            // delta > 0 (lệch giá, khách trả thêm) → phải thanh toán qua popup (exchange/pay) → Completed.
            //   Không được xác nhận trực tiếp ở đây (tránh sinh Payment không qua QR/cash-input).
            // delta == 0 (đổi ngang giá) → chỉ move kho, KHÔNG tiền.
            // ii.ReturnedQuantity đã reserve lúc CreateExchange — KHÔNG đụng.
            var exDelta = ret.DeltaAmount ?? 0m;
            if (exDelta > 0)
                return BadRequest(ApiErrorResponse.Create("MSG-78",
                    "Đơn đổi chênh lệch (thu thêm) phải thanh toán qua popup, không xác nhận trực tiếp."));

            var stockErr = await ApplyExchangeDiffStockAsync(ret);
            if (stockErr != null)
                return BadRequest(ApiErrorResponse.Create("MSG-81", stockErr));
        }
        else if (ret.ReturnType == "EXCHANGE")
        {
            // FIX L3: trước đây gom nhóm theo ProductId -> 1 WarrantyTicket link firstItem,
            // các ReturnItem còn lại bị mồ côi (unique 1-1 trên ReturnItemId) -> đứt liên kết,
            // DefectiveItems không hiện trạng thái BH. Đổi sang 1 WarrantyTicket / ReturnItem.

            // Bước 1: pre-check đủ sellable theo từng Product (trước khi giữ bất kỳ hold nào)
            var needByProduct = ret.ReturnItems
                .GroupBy(ri => ri.BranchProduct!.ProductId)
                .Select(g => new
                {
                    BranchProductId = g.First().BranchProductId,
                    TotalBase = g.Sum(ri => ri.BaseQuantity > 0 ? ri.BaseQuantity : ri.Quantity)
                })
                .ToList();

            foreach (var pg in needByProduct)
            {
                var bp = await _context.BranchProducts.FindAsync(pg.BranchProductId);
                if (bp == null) continue;
                var sellable = bp.AvailableStock - bp.WarrantyQuantity;
                if (sellable < pg.TotalBase)
                    return BadRequest(ApiErrorResponse.Create("MSG-81",
                        $"Sản phẩm xuất đổi hiện không đủ tồn kho khả dụng để thực hiện giao dịch (Tồn khả dụng bán: {sellable}, cần: {pg.TotalBase})"));
            }

            // Bước 2: 1 ticket / ReturnItem + hold theo từng dòng
            foreach (var ri in ret.ReturnItems)
            {
                var bp = await _context.BranchProducts.FindAsync(ri.BranchProductId);
                if (bp == null) continue;

                var baseQty = ri.BaseQuantity > 0 ? ri.BaseQuantity : ri.Quantity;
                bp.WarrantyQuantity += baseQty;
                bp.ActualStock += baseQty;
                bp.AvailableStock += baseQty;

                _context.WarrantyTickets.Add(new WarrantyTicket
                {
                    WarrantyTicketId = Guid.NewGuid(),
                    ReturnItemId = ri.ReturnItemId,              // FIX L3: mỗi ReturnItem có ticket riêng
                    BranchProductId = ri.BranchProductId,
                    ProductId = bp.ProductId,
                    Quantity = ri.Quantity,
                    ConversionRate = ri.ConversionRate > 0 ? ri.ConversionRate : 1,  // FIX L11
                    BaseQuantity = baseQty,
                    Status = "PENDING_ASSIGN",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = GetUserId()
                });
            }
        }

        // Create RefundRecord for Shift cash balancing
        // Gắn ca bán đang mở để shift summary trừ đúng tiền mặt/doanh thu trong ca đó.
        // Skip cho exchange-diff — tiền xử lý riêng theo Delta trong nhánh trên.
        var isExchangeDiff = ret.ReturnType == "EXCHANGE" && ret.ExchangeItems.Any();
        var openShiftId = await _context.Shifts
            .Where(s => s.BranchId == branchId && s.Status == "OPEN")
            .Select(s => (Guid?)s.ShiftId)
            .FirstOrDefaultAsync();

        if (!isExchangeDiff)
        {
            var refund = new RefundRecord
            {
                RefundId = Guid.NewGuid(),
                RefundCode = $"RFD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
                OrderId = ret.OrderId,
                InvoiceId = (await _context.Invoices.FirstOrDefaultAsync(i => i.OrderId == ret.OrderId))?.InvoiceId,
                CustomerId = ret.Order?.CustomerId ?? Guid.Empty,
                UserId = ret.UserId,
                RefundAmount = ret.RefundAmount,
                RefundMethod = ret.ReturnType == "REFUND" ? "CASH" : (ret.RefundMethod ?? "CASH"),
                Status = "REFUNDED",
                Reason = ret.Note,
                ProcessedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                ShiftId = openShiftId
            };
            _context.RefundRecords.Add(refund);
        }

        await _context.SaveChangesAsync();

        // SignalR: thông báo POS cập nhật tồn khả dụng khi đổi hàng (EXCHANGE — cả bảo hành và đổi chênh).
        if (ret.ReturnType == "EXCHANGE")
        {
            try
            {
                var bpIds = ret.ReturnItems.Select(ri => ri.BranchProductId)
                    .Concat(ret.ExchangeItems.Select(ei => ei.BranchProductId))
                    .Distinct()
                    .ToList();
                foreach (var bpId in bpIds)
                {
                    var bp = await _context.BranchProducts.FindAsync(bpId);
                    if (bp != null)
                    {
                        await _posHub.Clients.Group($"branch_{bp.BranchId}")
                            .SendAsync("StockUpdated", new
                            {
                                ProductId = bp.ProductId,
                                BranchId = bp.BranchId,
                                NewSellableQuantity = bp.AvailableStock - bp.WarrantyQuantity,
                                WarrantyQuantity = bp.WarrantyQuantity
                            });
                    }
                }
            }
            catch { /* SignalR fail silently */ }
        }

        await TryWriteSystemLog(
            GetUserId(),
            "FINALIZE_RETURN_ORDER",
            "return_orders",
            "ReturnOrder",
            ret.ReturnOrderId,
            $"Hoàn tất đổi trả {ret.ReturnCode} - Hoàn tiền: {ret.RefundAmount:N0}đ");

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
            .Include(r => r.ReturnItems)
            .Include(r => r.ExchangeItems)
            .FirstOrDefaultAsync(r => r.ReturnOrderId == id && r.Order!.BranchId == branchId);

        if (ret == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy phiếu đổi trả."));
        if (ret.Status != "Pending")
            return BadRequest(ApiErrorResponse.Create("MSG-78", "Chỉ phiếu đang chờ xử lý mới được hủy."));

        // Đổi chênh lệch: hoàn lại ii.ReturnedQuantity đã reserve lúc CreateExchange (theo InvoiceItemId lưu).
        if (ret.ReturnType == "EXCHANGE" && ret.ExchangeItems.Any())
        {
            var riWithIi = ret.ReturnItems.Where(ri => ri.InvoiceItemId.HasValue).ToList();
            if (riWithIi.Any())
            {
                var iiIds = riWithIi.Select(ri => ri.InvoiceItemId!.Value).ToList();
                var invoiceItems = await _context.InvoiceItems
                    .Where(ii => iiIds.Contains(ii.InvoiceItemId))
                    .ToListAsync();
                var iiMap = invoiceItems.ToDictionary(ii => ii.InvoiceItemId);
                foreach (var ri in riWithIi)
                {
                    if (iiMap.TryGetValue(ri.InvoiceItemId!.Value, out var ii))
                        ii.ReturnedQuantity = Math.Max(0m, ii.ReturnedQuantity - ri.Quantity);
                }
            }

            // Dọn Payment chuyển khoản đang chờ (nếu có) — tránh orphan pending payment.
            if (ret.PaymentId.HasValue)
            {
                var linkedPay = await _context.Payments.FindAsync(ret.PaymentId.Value);
                if (linkedPay != null && linkedPay.Status == "Pending")
                {
                    linkedPay.Status = "Cancelled";
                }
                ret.PaymentId = null;
            }
        }

        ret.Status = "Cancelled";
        await _context.SaveChangesAsync();

        await TryWriteSystemLog(
            GetUserId(),
            "CANCEL_RETURN_ORDER",
            "return_orders",
            "ReturnOrder",
            ret.ReturnOrderId,
            $"Hủy phiếu đổi trả {ret.ReturnCode}");

        return Ok(new { message = "Đã hủy phiếu đổi trả." });
    }

    // === Thanh toán đổi chênh lệch (delta > 0) ===
    // delta > 0 (lệch giá) → popup thanh toán: CASH (Completed ngay + Payment Confirmed + move kho)
    //   hoặc TRANSFER (sinh QR + Payment Pending, RO vẫn Pending — chờ confirm-transfer).
    // delta == 0 (ngang giá) → KHÔNG qua endpoint này (tạo Pending, finalize ở detail).

    private static readonly string BankAccountNumber = "0975849675";
    private static readonly string BankName = "MB Bank";

    private async Task<(ReturnOrder ret, string? err)> LoadPendingExchangeDiff(Guid id)
    {
        var branchId = GetBranchId();
        var ret = await _context.ReturnOrders
            .Include(r => r.Order).ThenInclude(o => o!.Invoice)
            .Include(r => r.ReturnItems).ThenInclude(ri => ri.BranchProduct).ThenInclude(bp => bp!.Product)
            .Include(r => r.ExchangeItems).ThenInclude(ei => ei.BranchProduct)
            .FirstOrDefaultAsync(r => r.ReturnOrderId == id && r.Order!.BranchId == branchId);

        if (ret == null) return (ret, "Không tìm thấy phiếu đổi trả.");
        if (ret.Status != "Pending") return (ret, "Phiếu đổi trả không ở trạng thái chờ xử lý.");
        if (ret.ReturnType != "EXCHANGE" || !ret.ExchangeItems.Any())
            return (ret, "Phiếu không phải đổi chênh lệch.");
        if ((ret.DeltaAmount ?? 0m) <= 0)
            return (ret, "Phiếu đổi ngang giá — không cần thanh toán chênh lệch.");
        return (ret, null);
    }

    private async Task<Guid?> GetOpenShiftIdAsync(Guid branchId) =>
        await _context.Shifts
            .Where(s => s.BranchId == branchId && s.Status == "OPEN")
            .Select(s => (Guid?)s.ShiftId)
            .FirstOrDefaultAsync();

    // POST /api/pos/returns/{id}/exchange/pay
    /// <summary>
    /// Thanh toán chênh lệch đổi hàng (delta>0).
    /// CASH → tạo Payment Confirmed (cashReceived/changeAmount) + move kho + RO Completed.
    /// TRANSFER → sinh QR (VietQR) + Payment Pending, RO vẫn Pending chờ confirm-transfer.
    /// </summary>
    [HttpPost("{id:guid}/exchange/pay")]
    public async Task<ActionResult> PayExchangeDiff(Guid id, [FromBody] PayExchangeRequest req)
    {
        var branchId = GetBranchId();
        var (ret, err) = await LoadPendingExchangeDiff(id);
        if (err != null)
            return BadRequest(ApiErrorResponse.Create("MSG-78", err!));

        var delta = ret!.DeltaAmount ?? 0m;
        var method = (req?.Method?.ToUpper() ?? ret.PaymentMethod ?? "CASH");
        if (method != "CASH" && method != "TRANSFER")
            return BadRequest(ApiErrorResponse.Create("MSG-78", "Phương thức thanh toán không hợp lệ (CASH/TRANSFER)."));

        var openShiftId = await GetOpenShiftIdAsync(branchId);
        var invoiceId = ret.Order?.Invoice?.InvoiceId;

        if (method == "CASH")
        {
            if (req!.CashReceived == null || req.CashReceived < delta)
                return BadRequest(ApiErrorResponse.Create("MSG-76", "Tiền khách đưa nhỏ hơn số phải trả."));

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Nếu đang có Payment chuyển khoản pending → huỷ, chuyển sang cash.
                if (ret.PaymentId.HasValue)
                {
                    var old = await _context.Payments.FindAsync(ret.PaymentId.Value);
                    if (old != null && old.Status == "Pending")
                        old.Status = "Cancelled";
                    ret.PaymentId = null;
                }

                var payment = new Payment
                {
                    PaymentId = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    Method = "Cash",
                    Amount = delta,
                    CashReceived = req.CashReceived.Value,
                    ChangeAmount = Math.Max(0m, req.CashReceived.Value - delta),
                    Status = "Confirmed",
                    PaidAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    BranchId = branchId,
                    ShiftId = openShiftId
                };
                _context.Payments.Add(payment);
                ret.PaymentId = payment.PaymentId;

                var stockErr = await ApplyExchangeDiffStockAsync(ret);
                if (stockErr != null)
                    throw new Exception(stockErr);

                ret.Status = "Completed";
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return BadRequest(ApiErrorResponse.Create("MSG-81", ex.Message));
            }

            await TryWriteSystemLog(GetUserId(), "PAY_EXCHANGE_DIFF", "return_orders", "ReturnOrder",
                ret.ReturnOrderId, $"Thanh toán tiền mặt đổi chênh {ret.ReturnCode} - Completed - {delta:N0}đ");
            await NotifyExchangeDiffStockAsync(ret);

            var dtoCash = _mapper.Map<ReturnOrderDto>(ret);
            return Ok(new
            {
                returnOrder = dtoCash,
                method = "CASH",
                paidAmount = delta,
                cashReceived = req.CashReceived.Value,
                changeAmount = Math.Max(0m, req.CashReceived.Value - delta)
            });
        }

        // TRANSFER
        var content = $"MEP {ret.ReturnCode}";
        var (qrBase64, transactionContent) = await _vietQr.GenerateQrAsync(delta, content);

        Payment? p = null;
        using (var tx2 = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                if (ret.PaymentId.HasValue)
                {
                    p = await _context.Payments.FindAsync(ret.PaymentId.Value);
                    if (p != null && p.Method == "Transfer")
                    {
                        if (p.Status == "Confirmed")
                            return BadRequest(ApiErrorResponse.Create("MSG-78", "Đơn đã thanh toán xong."));
                        if (p.Status == "Pending")
                        {
                            // Refresh QR + gia hạn.
                            p.VietQRString = transactionContent;
                            p.QRExpiredAt = DateTime.UtcNow.AddMinutes(5);
                            p.Amount = delta;
                        }
                        else
                        {
                            // Cancelled / expired cũ → tạo mới.
                            p.Status = "Cancelled";
                            p = null;
                        }
                    }
                    else
                    {
                        p = null;
                    }
                }

                if (p == null)
                {
                    p = new Payment
                    {
                        PaymentId = Guid.NewGuid(),
                        InvoiceId = invoiceId,
                        Method = "Transfer",
                        Amount = delta,
                        Status = "Pending",
                        VietQRString = transactionContent,
                        QRExpiredAt = DateTime.UtcNow.AddMinutes(5),
                        CreatedAt = DateTime.UtcNow,
                        BranchId = branchId,
                        ShiftId = openShiftId
                    };
                    _context.Payments.Add(p);
                    ret.PaymentId = p.PaymentId;
                }

                await _context.SaveChangesAsync();
                await tx2.CommitAsync();
            }
            catch (Exception ex)
            {
                await tx2.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi tạo QR: " + ex.Message });
            }
        }

        await TryWriteSystemLog(GetUserId(), "PAY_EXCHANGE_DIFF_QR", "return_orders", "ReturnOrder",
            ret.ReturnOrderId, $"Tạo QR chuyển khoản đổi chênh {ret.ReturnCode} - {delta:N0}đ");

        return Ok(new
        {
            paymentId = p.PaymentId,
            qrImageBase64 = qrBase64,
            transactionContent,
            amount = delta,
            bankAccountNumber = BankAccountNumber,
            bankName = BankName,
            returnOrderId = ret.ReturnOrderId,
            method = "TRANSFER"
        });
    }

    // POST /api/pos/returns/{id}/exchange/confirm-transfer
    /// <summary>
    /// Xác nhận khách đã chuyển khoản → Payment Confirmed + move kho + RO Completed.
    /// </summary>
    [HttpPost("{id:guid}/exchange/confirm-transfer")]
    public async Task<ActionResult<ReturnOrderDto>> ConfirmExchangeTransfer(Guid id, [FromBody] ConfirmTransferRequest? req)
    {
        var (ret, err) = await LoadPendingExchangeDiff(id);
        if (err != null)
            return BadRequest(ApiErrorResponse.Create("MSG-78", err!));

        if (!ret!.PaymentId.HasValue)
            return BadRequest(ApiErrorResponse.Create("MSG-78", "Chưa có thanh toán chuyển khoản cho phiếu này."));

        var p = await _context.Payments.FindAsync(ret.PaymentId.Value);
        if (p == null || p.Method != "Transfer")
            return BadRequest(ApiErrorResponse.Create("MSG-78", "Không tìm thấy Payment chuyển khoản hợp lệ."));
        if (p.Status == "Confirmed")
            return BadRequest(ApiErrorResponse.Create("MSG-76", "Thanh toán đã được xác nhận."));
        if (req?.PaymentId.HasValue == true && req.PaymentId.Value != p.PaymentId)
            return BadRequest(ApiErrorResponse.Create("MSG-78", "Payment không khớp với phiếu đổi trả."));
        if (p.QRExpiredAt.HasValue && p.QRExpiredAt.Value < DateTime.UtcNow)
            return BadRequest(ApiErrorResponse.Create("MSG-76", "Mã QR đã hết hạn. Vui lòng tạo lại."));

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            p.Status = "Confirmed";
            p.PaidAt = DateTime.UtcNow;

            var stockErr = await ApplyExchangeDiffStockAsync(ret);
            if (stockErr != null)
                throw new Exception(stockErr);

            ret.Status = "Completed";
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return BadRequest(ApiErrorResponse.Create("MSG-81", ex.Message));
        }

        await TryWriteSystemLog(GetUserId(), "CONFIRM_EXCHANGE_TRANSFER", "return_orders", "ReturnOrder",
            ret.ReturnOrderId, $"Xác nhận chuyển khoản đổi chênh {ret.ReturnCode} - Completed");
        await NotifyExchangeDiffStockAsync(ret);

        return Ok(_mapper.Map<ReturnOrderDto>(ret));
    }

    // POST /api/pos/returns/{id}/exchange/cancel-payment
    /// <summary>
    /// Huỷ Payment chuyển khoản đang chờ (khi bỏ QR / đổi sang method khác). RO vẫn Pending.
    /// </summary>
    [HttpPost("{id:guid}/exchange/cancel-payment")]
    public async Task<ActionResult> CancelExchangePayment(Guid id)
    {
        var (ret, err) = await LoadPendingExchangeDiff(id);
        if (err != null)
            return BadRequest(ApiErrorResponse.Create("MSG-78", err!));

        if (ret!.PaymentId.HasValue)
        {
            var p = await _context.Payments.FindAsync(ret.PaymentId.Value);
            if (p != null && p.Status == "Pending")
                p.Status = "Cancelled";
            ret.PaymentId = null;
            await _context.SaveChangesAsync();
        }

        return Ok(new { message = "Đã huỷ thanh toán chênh lệch đang chờ." });
    }

    public class PayExchangeRequest
    {
        public string? Method { get; set; }
        public decimal? CashReceived { get; set; }
    }

    public class ConfirmTransferRequest
    {
        public Guid? PaymentId { get; set; }
    }

    // GET /api/pos/returns/return-policies
    /// <summary>
    /// Trả về danh sách chính sách đổi/trả theo nhóm hàng của chi nhánh hiện tại.
    /// </summary>
    [HttpGet("return-policies")]
    public async Task<ActionResult<List<CategoryPolicyDto>>> GetReturnPolicies()
    {
        var branchId = GetBranchId();

        var config = await _context.SystemConfigs
            .FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "CATEGORY_RETURN_POLICIES");

        var policies = new List<CategoryPolicyDto>();
        if (config != null && !string.IsNullOrWhiteSpace(config.ConfigValue))
        {
            try
            {
                var deserialized = JsonSerializer.Deserialize<List<CategoryPolicyDto>>(config.ConfigValue,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (deserialized != null) policies = deserialized;
            }
            catch { }
        }

        return Ok(policies);
    }

    // GET /api/pos/returns/invoice/{invoiceId:guid}/eligible-items
    /// <summary>
    /// Trả về danh sách sản phẩm trong hóa đơn kèm trạng thái được phép đổi/trả.
    /// Lấy chính sách đổi trả theo nhóm hàng của chi nhánh hiện tại (dùng cho POS).
    /// </summary>
    [HttpGet("category-policies")]
    public async Task<ActionResult<List<CategoryPolicyDto>>> GetCategoryPolicies()
    {
        var branchId = GetBranchId();
        var config = await _context.SystemConfigs
            .FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "CATEGORY_RETURN_POLICIES");

        if (config == null || string.IsNullOrWhiteSpace(config.ConfigValue))
            return Ok(new List<CategoryPolicyDto>());

        try
        {
            var policies = JsonSerializer.Deserialize<List<CategoryPolicyDto>>(config.ConfigValue,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return Ok(policies ?? new List<CategoryPolicyDto>());
        }
        catch
        {
            return Ok(new List<CategoryPolicyDto>());
        }
    }

    [HttpGet("discount-percent")]
    public async Task<ActionResult<object>> GetReturnDiscount()
    {
        var branchId = GetBranchId();
        var percent = await GetReturnDiscountPercent(branchId);
        return Ok(new { returnDiscountPercent = percent });
    }

    /// <summary>
    /// Kiểm tra sản phẩm trong hóa đơn có đủ điều kiện đổi/trả không.
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

    // GET /api/pos/returns/defective-items
    [HttpGet("defective-items")]
    [HasPermission(PermissionConstants.SALE_VIEW)]
    public async Task<ActionResult<PageResultDto<DefectiveItemDto>>> GetDefectiveItems(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var branchId = GetBranchId();

        var query = _context.ReturnItems
            .Include(ri => ri.ReturnOrder).ThenInclude(ro => ro.Order).ThenInclude(o => o.Customer)
            .Include(ri => ri.ReturnOrder).ThenInclude(ro => ro.Order).ThenInclude(o => o.Invoice)
            .Include(ri => ri.BranchProduct).ThenInclude(bp => bp.Product)
            .Include(ri => ri.WarrantyTicket).ThenInclude(wt => wt!.Supplier)
            .Where(ri => ri.ReturnOrder.OrderId != Guid.Empty && ri.ReturnOrder.Order.BranchId == branchId)
            .Where(ri => ri.ReturnOrder.ReturnType == "EXCHANGE" && ri.ReturnOrder.Status == "Completed");

        if (fromDate.HasValue) query = query.Where(ri => ri.ReturnOrder.CreatedAt >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(ri => ri.ReturnOrder.CreatedAt <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(ri => 
                (ri.BranchProduct.Product.ProductName != null && ri.BranchProduct.Product.ProductName.ToLower().Contains(s)) ||
                (ri.ReturnOrder.ReturnCode != null && ri.ReturnOrder.ReturnCode.ToLower().Contains(s)) ||
                (ri.ReturnOrder.Order.Customer != null && ri.ReturnOrder.Order.Customer.CustomerName != null && ri.ReturnOrder.Order.Customer.CustomerName.ToLower().Contains(s)) ||
                (ri.ReturnOrder.Order.Invoice != null && ri.ReturnOrder.Order.Invoice.InvoiceCode != null && ri.ReturnOrder.Order.Invoice.InvoiceCode.ToLower().Contains(s))
            );
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(ri => ri.ReturnOrder.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ri => new DefectiveItemDto
            {
                ReturnItemId = ri.ReturnItemId,
                ReturnCode = ri.ReturnOrder.ReturnCode ?? "",
                ExchangeDate = ri.ReturnOrder.CreatedAt,
                BranchProductId = ri.BranchProductId,
                ProductId = ri.BranchProduct.ProductId,
                ProductName = ri.BranchProduct.Product.ProductName ?? "",
                SkuCode = ri.BranchProduct.Product.ProductCode ?? "",
                Quantity = ri.Quantity,
                ConversionRate = ri.ConversionRate,
                BaseQuantity = ri.BaseQuantity,
                Reason = ri.Reason,
                CustomerName = ri.ReturnOrder.Order.Customer != null ? ri.ReturnOrder.Order.Customer.CustomerName : "",
                InvoiceCode = ri.ReturnOrder.Order.Invoice != null ? ri.ReturnOrder.Order.Invoice.InvoiceCode : "",
                WarrantyTicketId = ri.WarrantyTicket != null ? ri.WarrantyTicket.WarrantyTicketId : (Guid?)null,
                WarrantyStatus = ri.WarrantyTicket != null ? ri.WarrantyTicket.Status : null,
                AssignedSupplierId = ri.WarrantyTicket != null ? ri.WarrantyTicket.SupplierId : (Guid?)null,
                AssignedSupplierName = ri.WarrantyTicket != null && ri.WarrantyTicket.Supplier != null
                    ? ri.WarrantyTicket.Supplier.SupplierName : null
            })
            .ToListAsync();

        return Ok(new PageResultDto<DefectiveItemDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    // ============================================================
    // ĐỔI HÀNG CHÊNH LỆCH (Exchange with Price Difference)
    // ============================================================

    // GET policies helper
    private async Task<List<CategoryPolicyDto>?> GetCategoryPoliciesConfig(Guid branchId)
    {
        var config = await _context.SystemConfigs
            .FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "CATEGORY_RETURN_POLICIES");
        if (config == null || string.IsNullOrWhiteSpace(config.ConfigValue)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<CategoryPolicyDto>>(config.ConfigValue,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }

    /// <summary>
    /// Đọc chiết khấu trả hàng branch-level (%) từ system_config (key RETURN_DISCOUNT_PERCENT).
    /// Áp dụng cho cả trả hàng hoàn tiền và đổi hàng lệch giá. Trả 0 nếu không set.
    /// </summary>
    private async Task<decimal> GetReturnDiscountPercent(Guid branchId)
    {
        var config = await _context.SystemConfigs
            .FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "RETURN_DISCOUNT_PERCENT");
        if (config != null && !string.IsNullOrWhiteSpace(config.ConfigValue)
            && decimal.TryParse(config.ConfigValue, System.Globalization.CultureInfo.InvariantCulture, out var p)
            && p > 0m)
            return p;
        return 0m;
    }

    private static CategoryPolicyDto? MatchPolicy(List<CategoryPolicyDto> policies, Product? product)
    {
        if (product == null) return null;
        var categoryId = product.CategoryTaxonomyId;
        CategoryPolicyDto? p = null;
        if (categoryId.HasValue)
            p = policies.FirstOrDefault(c => c.CategoryId.HasValue && c.CategoryId.Value == categoryId.Value);
        if (p == null)
        {
            var name = product.CategoryTaxonomy?.Name ?? product.CategoryName;
            if (!string.IsNullOrEmpty(name))
                p = policies.FirstOrDefault(c => c.CategoryName != null && c.CategoryName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        return p;
    }

    private InventoryLedger MkLedger(BranchProduct bp, string docType, Guid docId, string? docCode, decimal qtyChange)
    {
        return new InventoryLedger
        {
            LedgerId = Guid.NewGuid(),
            BranchId = bp.BranchId,
            BranchProductId = bp.BranchProductId,
            DocType = docType,
            DocId = docId,
            DocCode = docCode,
            TransactionTime = DateTime.UtcNow,
            QtyChange = qtyChange,
            UnitCostPrice = Math.Round(bp.CostPrice, 4),
            TotalCostValue = Math.Round(qtyChange * bp.CostPrice, 4),
            StockAfter = bp.ActualStock,
            AvgCostAfter = Math.Round(bp.CostPrice, 4),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Move kho cho đổi chênh lệch: SP A trả về kho bán (CUSTOMER_RETURN) + SP B xuất kho (EXCHANGE_OUT).
    /// Pre-check tồn SP B khả dụng. Trả null nếu OK, trả message lỗi nếu thiếu tồn.
    /// KHÔNG đụng ii.ReturnedQuantity (đã reserve ở CreateExchange) — KHÔNG xử lý tiền (do pay/confirm lo).
    /// Dùng chung cho: FinalizeReturn (delta==0 ngang giá) + exchange/pay (cash) + exchange/confirm-transfer.
    /// </summary>
    private async Task<string?> ApplyExchangeDiffStockAsync(ReturnOrder ret)
    {
        // Pre-check tồn SP B khả dụng (kho có thể đổi từ lúc tạo pending).
        var bNeed = ret.ExchangeItems
            .GroupBy(ei => ei.BranchProductId)
            .Select(g => new { BranchProductId = g.Key, TotalBase = g.Sum(ei => ei.BaseQuantity > 0 ? ei.BaseQuantity : ei.Quantity) })
            .ToList();
        foreach (var pg in bNeed)
        {
            var bp = await _context.BranchProducts.FindAsync(pg.BranchProductId);
            if (bp == null) continue;
            var available = bp.AvailableStock - bp.WarrantyQuantity - bp.ReservedStock;
            if (available < pg.TotalBase)
                return $"Sản phẩm đổi mới không đủ tồn kho khả dụng để thực hiện giao dịch (Tồn bán: {available}, cần: {pg.TotalBase})";
        }

        var stockTicketReturn = new StockTicket
        {
            StockTicketId = Guid.NewGuid(),
            TicketCode = ret.ReturnCode,
            BranchId = ret.Order!.BranchId,
            UserId = ret.UserId,
            TicketType = "CUSTOMER_RETURN",
            Status = "COMPLETED",
            Notes = "Đổi hàng (Nhập trả) " + ret.ReturnCode,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        _context.StockTickets.Add(stockTicketReturn);

        // SP A: hàng trả về lại kho bán (CUSTOMER_RETURN). Không sinh bảo hành.
        foreach (var ri in ret.ReturnItems)
        {
            var bp = await _context.BranchProducts.Include(b => b.Product).FirstOrDefaultAsync(b => b.BranchProductId == ri.BranchProductId);
            if (bp == null) continue;
            var baseQty = ri.BaseQuantity > 0 ? ri.BaseQuantity : ri.Quantity;

            var ticketItem = new StockTicketItem
            {
                TicketItemId = Guid.NewGuid(),
                StockTicketId = stockTicketReturn.StockTicketId,
                BranchProductId = ri.BranchProductId,
                SystemQuantity = bp.ActualStock,
                ActualQuantity = bp.ActualStock + baseQty,
                Discrepancy = baseQty,
                UnitPrice = bp.CostPrice,
                UnitName = bp.Product?.BaseUnit ?? ""
            };
            _context.StockTicketItems.Add(ticketItem);

            bp.AvailableStock += baseQty;
            bp.ActualStock += baseQty;
            _context.InventoryLedgers.Add(MkLedger(bp, "CUSTOMER_RETURN", ret.ReturnOrderId, ret.ReturnCode, baseQty));
        }

        var stockTicketSale = new StockTicket
        {
            StockTicketId = Guid.NewGuid(),
            TicketCode = ret.ReturnCode + "-OUT",
            BranchId = ret.Order!.BranchId,
            UserId = ret.UserId,
            TicketType = "SALE", // Xuất cho KH như bán hàng
            Status = "COMPLETED",
            Notes = "Đổi hàng (Xuất giao) " + ret.ReturnCode,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        _context.StockTickets.Add(stockTicketSale);

        // SP B: xuất kho (EXCHANGE_OUT)
        foreach (var ei in ret.ExchangeItems)
        {
            var bp = await _context.BranchProducts.Include(b => b.Product).FirstOrDefaultAsync(b => b.BranchProductId == ei.BranchProductId);
            if (bp == null) continue;
            var baseQty = ei.BaseQuantity > 0 ? ei.BaseQuantity : ei.Quantity;

            var ticketItem = new StockTicketItem
            {
                TicketItemId = Guid.NewGuid(),
                StockTicketId = stockTicketSale.StockTicketId,
                BranchProductId = ei.BranchProductId,
                SystemQuantity = bp.ActualStock,
                ActualQuantity = bp.ActualStock - baseQty,
                Discrepancy = baseQty,
                UnitPrice = bp.CostPrice,
                UnitName = bp.Product?.BaseUnit ?? ""
            };
            _context.StockTicketItems.Add(ticketItem);

            bp.AvailableStock -= baseQty;
            bp.ActualStock -= baseQty;
            _context.InventoryLedgers.Add(MkLedger(bp, "EXCHANGE_OUT", ret.ReturnOrderId, ret.ReturnCode, -baseQty));
        }

        return null;
    }

    /// <summary>Phát SignalR StockUpdated cho các BranchProduct của exchange-diff (SP A + SP B).</summary>
    private async Task NotifyExchangeDiffStockAsync(ReturnOrder ret)
    {
        try
        {
            var bpIds = ret.ReturnItems.Select(ri => ri.BranchProductId)
                .Concat(ret.ExchangeItems.Select(ei => ei.BranchProductId))
                .Distinct()
                .ToList();
            foreach (var bpId in bpIds)
            {
                var bp = await _context.BranchProducts.FindAsync(bpId);
                if (bp != null)
                {
                    await _posHub.Clients.Group($"branch_{bp.BranchId}")
                        .SendAsync("StockUpdated", new
                        {
                            ProductId = bp.ProductId,
                            BranchId = bp.BranchId,
                            NewSellableQuantity = bp.AvailableStock - bp.WarrantyQuantity,
                            WarrantyQuantity = bp.WarrantyQuantity
                        });
                }
            }
        }
        catch { /* SignalR fail silently */ }
    }

    // --- Prepared data structures ---
    private class PreparedReturnItem
    {
        public InvoiceItem InvoiceItem { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal ConversionRate { get; set; }
        public decimal BaseQuantity { get; set; }
        public string Reason { get; set; } = "DEFECTIVE";
        public decimal LineRefundAmount { get; set; }
    }

    private class PreparedNewItem
    {
        public BranchProduct BranchProduct { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal ConversionRate { get; set; }
        public decimal BaseQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    private class ExchangePreparation
    {
        public bool Eligible { get; set; }
        public string? RestrictionReason { get; set; }
        public string? RuleBreak { get; set; }
        public decimal OldValue { get; set; }
        public decimal NewValue { get; set; }
        public decimal Delta { get; set; }
        public string DeltaLabel { get; set; } = "Đổi ngang giá";
        public List<StockAvailabilityDto> StockCheck { get; set; } = new();
        public List<PreparedReturnItem> Returns { get; set; } = new();
        public List<PreparedNewItem> NewItems { get; set; } = new();
        public Invoice? Invoice { get; set; }
    }

    /// <summary>
    /// Dry-run: validate + tính Delta + check tồn SP B. KHÔNG ghi DB.
    /// </summary>
    private async Task<ExchangePreparation> PrepareExchangeAsync(
        Guid invoiceId, List<ExchangeQuoteItem> returnItems, List<ExchangeNewItem> newItems)
    {
        var prep = new ExchangePreparation();
        var branchId = GetBranchId();

        var invoice = await _context.Invoices
            .Include(i => i.Order).ThenInclude(o => o!.Branch)
            .Include(i => i.Order).ThenInclude(o => o!.Customer)
            .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp!.Product).ThenInclude(p => p!.CategoryTaxonomy)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order!.BranchId == branchId);

        if (invoice == null)
        {
            prep.RestrictionReason = "Không tìm thấy hóa đơn."; prep.RuleBreak = "INVOICE"; return prep;
        }
        if (invoice.Status != "Completed")
        {
            prep.RestrictionReason = "Chỉ hóa đơn đã hoàn tất mới được đổi."; prep.RuleBreak = "INVOICE"; return prep;
        }
        prep.Invoice = invoice;

        var policies = await GetCategoryPoliciesConfig(branchId);
        if (policies == null || policies.Count == 0)
        {
            prep.RestrictionReason = "Chưa có chính sách đổi/trả theo nhóm hàng."; prep.RuleBreak = "POLICY"; return prep;
        }

        var branch = invoice.Order?.Branch;
        var branchExchangeDays = branch?.ExchangeDaysAllowed ?? 30;
        var invoiceDate = invoice.CreatedAt ?? DateTime.UtcNow;

        // Chiết khấu trả hàng branch-level (thay cho prorate chiết khấu của hóa đơn gốc)
        var returnDiscountPercent = await GetReturnDiscountPercent(branchId);
        var discountRatio = returnDiscountPercent > 0m ? returnDiscountPercent / 100m : 0m;

        decimal oldValue = 0m;

        // --- SP A (hàng trả về) ---
        foreach (var ri in returnItems)
        {
            if (ri.Quantity <= 0)
            {
                prep.RestrictionReason = "Số lượng đổi phải lớn hơn 0."; prep.RuleBreak = "QTY"; return prep;
            }
            var ii = invoice.InvoiceItems.FirstOrDefault(x => x.InvoiceItemId == ri.InvoiceItemId);
            if (ii == null)
            {
                prep.RestrictionReason = $"Dòng hóa đơn không có trong hóa đơn gốc."; prep.RuleBreak = "INVOICE_ITEM"; return prep;
            }

            var product = ii.BranchProduct?.Product;
            var policy = MatchPolicy(policies, product);
            if (policy == null)
            {
                prep.RestrictionReason = $"Sản phẩm \"{product?.ProductName}\" chưa có chính sách đổi/trả."; prep.RuleBreak = "POLICY"; return prep;
            }
            var exDays = policy.ExchangeDays;
            if (!exDays.HasValue || exDays.Value <= 0)
            {
                prep.RestrictionReason = $"Nhóm hàng \"{policy.CategoryName}\" không được phép đổi."; prep.RuleBreak = "EXCHANGE_DAYS"; return prep;
            }
            var allowedDays = Math.Min(exDays.Value, branchExchangeDays);
            if (invoiceDate.AddDays(allowedDays) < DateTime.UtcNow)
            {
                var overDays = (int)(DateTime.UtcNow - invoiceDate.AddDays(allowedDays)).TotalDays;
                prep.RestrictionReason = $"Đã quá thời hạn đổi {overDays} ngày."; prep.RuleBreak = "EXCHANGE_DAYS"; return prep;
            }
            // Chống đổi lặp
            if (ri.Quantity + ii.ReturnedQuantity > ii.Quantity)
            {
                var maxQty = ii.Quantity - ii.ReturnedQuantity;
                prep.RestrictionReason = $"Đã đổi/trả đủ số lượng cho dòng \"{product?.ProductName}\". Tối đa còn {maxQty}."; prep.RuleBreak = "DUPLICATE"; return prep;
            }

            var conversionRate = (ii.ConvertValue.HasValue && ii.ConvertValue.Value > 0) ? ii.ConvertValue.Value : 1m;
            var baseQty = ri.Quantity * conversionRate;
            var gross = ri.Quantity * ii.SellPrice;
            var refundLine = Math.Max(0, gross - gross * discountRatio);
            oldValue += refundLine;

            prep.Returns.Add(new PreparedReturnItem
            {
                InvoiceItem = ii,
                Quantity = ri.Quantity,
                ConversionRate = conversionRate,
                BaseQuantity = baseQty,
                Reason = (ri.Reason?.ToUpper() ?? "DEFECTIVE"),
                LineRefundAmount = refundLine
            });
        }

        // --- SP B (hàng mới) ---
        decimal newValue = 0m;
        foreach (var ni in newItems)
        {
            if (ni.Quantity <= 0)
            {
                prep.RestrictionReason = "Số lượng hàng mới phải lớn hơn 0."; prep.RuleBreak = "QTY"; return prep;
            }
            var bp = await _context.BranchProducts
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.BranchProductId == ni.BranchProductId && b.BranchId == branchId);
            if (bp == null)
            {
                prep.RestrictionReason = "Không tìm thấy sản phẩm mới trong chi nhánh."; prep.RuleBreak = "NEW_ITEM"; return prep;
            }
            var conversionRate = (ni.ConversionRate.HasValue && ni.ConversionRate.Value > 0) ? ni.ConversionRate.Value : 1m;
            var baseQty = ni.Quantity * conversionRate;
            // Đơn giá theo ĐVT khách chọn (client gửi — cùng cách với luồng bán hàng lưu ii.SellPrice).
            // Nếu client không gửi → quy đổi từ giá base: bp.SellPrice * conversionRate.
            // TRƯỚC ĐÂY dùng bp.SellPrice (giá/Cái) * ni.Quantity (Thùng) → thiếu hệ số -> sai Delta.
            var unitPrice = (ni.UnitPrice.HasValue && ni.UnitPrice.Value > 0)
                ? ni.UnitPrice.Value
                : bp.SellPrice * conversionRate;
            var lineTotal = ni.Quantity * unitPrice;
            newValue += lineTotal;

            prep.NewItems.Add(new PreparedNewItem
            {
                BranchProduct = bp,
                Quantity = ni.Quantity,
                ConversionRate = conversionRate,
                BaseQuantity = baseQty,
                UnitPrice = unitPrice,
                LineTotal = lineTotal
            });
        }

        // Check tồn SP B (group theo BranchProductId)
        bool stockSufficient = true;
        foreach (var g in prep.NewItems.GroupBy(x => x.BranchProduct.BranchProductId))
        {
            var bp = g.First().BranchProduct;
            var requestedBase = g.Sum(x => x.BaseQuantity);
            var available = bp.AvailableStock - bp.WarrantyQuantity - bp.ReservedStock;
            var suff = available >= requestedBase;
            if (!suff) stockSufficient = false;
            prep.StockCheck.Add(new StockAvailabilityDto
            {
                BranchProductId = bp.BranchProductId,
                ProductName = bp.Product?.ProductName,
                ProductCode = bp.Product?.ProductCode,
                RequestedBase = requestedBase,
                AvailableBase = available,
                Sufficient = suff
            });
        }
        if (!stockSufficient)
        {
            prep.RestrictionReason = "Sản phẩm mới không đủ tồn kho khả dụng để xuất đổi."; prep.RuleBreak = "STOCK";
        }

        prep.OldValue = oldValue;
        prep.NewValue = newValue;
        prep.Delta = newValue - oldValue;
        prep.DeltaLabel = prep.Delta > 0 ? "Thu thêm" : prep.Delta < 0 ? "Hoàn lại" : "Đổi ngang giá";

        // Chính sách: chỉ cho đổi sang SP B cùng giá hoặc CAO HƠN (Delta >= 0).
        // KHÔNG cho đổi sang hàng rẻ hơn (sẽ phải hoàn tiền lại cho khách).
        if (prep.Delta < 0)
        {
            prep.RestrictionReason = "Chỉ được đổi sang sản phẩm cùng giá hoặc cao hơn. Không được đổi sang hàng rẻ hơn để hoàn tiền lại.";
            prep.RuleBreak = "DELTA_NEGATIVE";
        }

        prep.Eligible = prep.RestrictionReason == null;
        return prep;
    }

    // POST /api/pos/returns/exchange/quote
    /// <summary>Dry-run: tính Delta + check tồn SP B, KHÔNG ghi DB. UI gọi mỗi khi đổi qty.</summary>
    [HttpPost("exchange/quote")]
    public async Task<ActionResult<ExchangeQuoteResponse>> QuoteExchange([FromBody] ExchangeQuoteRequest req)
    {
        var prep = await PrepareExchangeAsync(req.InvoiceId, req.ReturnItems, req.NewItems);
        return Ok(new ExchangeQuoteResponse
        {
            Eligible = prep.Eligible,
            RestrictionReason = prep.RestrictionReason,
            RuleBreak = prep.RuleBreak,
            OldValue = prep.OldValue,
            NewValue = prep.NewValue,
            DeltaAmount = prep.Delta,
            DeltaLabel = prep.DeltaLabel,
            StockCheck = prep.StockCheck
        });
    }

    // POST /api/pos/returns/exchange
    /// <summary>Thực thi đổi hàng chênh lệch đầy đủ (validate + tạo ReturnOrder(EXCHANGE) + ExchangeItems + stock moves + ReturnedQuantity + cân quỹ) trong 1 transaction.</summary>
    [HttpPost("exchange")]
    public async Task<ActionResult<ReturnOrderDto>> CreateExchange([FromBody] CreateExchangeRequest req)
    {
        var branchId = GetBranchId();
        var userId = GetUserId();

        var prep = await PrepareExchangeAsync(req.InvoiceId, req.ReturnItems, req.NewItems);
        if (!prep.Eligible || prep.Invoice == null)
            return BadRequest(ApiErrorResponse.Create("MSG-80", prep.RestrictionReason ?? "Không đủ điều kiện đổi hàng."));
        if (prep.Returns.Count == 0)
            return BadRequest(ApiErrorResponse.Create("MSG-61", "Phải có ít nhất 1 sản phẩm đổi (SP A)."));
        if (prep.NewItems.Count == 0)
            return BadRequest(ApiErrorResponse.Create("MSG-61", "Phải có ít nhất 1 sản phẩm mới (SP B)."));

        var payMethod = (req.PaymentMethod?.ToUpper() ?? "CASH");
        if (prep.Delta != 0 && payMethod != "CASH" && payMethod != "TRANSFER")
            return BadRequest(ApiErrorResponse.Create("MSG-78", "Phương thức thanh toán không hợp lệ (CASH/TRANSFER)."));

        var invoice = prep.Invoice;
        var delta = prep.Delta;

        // Chống 2 phiếu pending cùng lúc trên 1 hóa đơn (giống CreateReturn).
        var hasPending = await _context.ReturnOrders
            .AnyAsync(r => r.OrderId == invoice.OrderId && r.Status == "Pending");
        if (hasPending)
            return BadRequest(ApiErrorResponse.Create("MSG-66", "Hóa đơn này đang có phiếu đổi trả đang xử lý."));

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var ro = new ReturnOrder
            {
                ReturnOrderId = Guid.NewGuid(),
                ReturnCode = await GenCodeAsync("RET"),
                OrderId = invoice.OrderId,
                UserId = userId,
                RefundMethod = "EXCHANGE",
                ReturnType = "EXCHANGE",
                Status = "Pending",
                RefundAmount = prep.OldValue,
                DeltaAmount = delta,
                PayAmount = Math.Max(0, delta),
                RefundAmountCustomer = Math.Max(0, -delta),
                PaymentMethod = delta != 0 ? payMethod : null,
                Note = req.Note,
                CreatedAt = DateTime.UtcNow
            };
            _context.ReturnOrders.Add(ro);

            // 1) SP A: ReturnItem (+ InvoiceItemId để huỷ phục hồi) + reserve ReturnedQuantity (chống đổi lặp).
            //    KHÔNG move kho / KHÔNG tiền — chờ FinalizeReturn (pending như REFUND).
            foreach (var ri in prep.Returns)
            {
                var ii = ri.InvoiceItem;
                ro.ReturnItems.Add(new ReturnItem
                {
                    ReturnItemId = Guid.NewGuid(),
                    ReturnOrderId = ro.ReturnOrderId,
                    BranchProductId = ii.BranchProductId,
                    InvoiceItemId = ii.InvoiceItemId,
                    Quantity = ri.Quantity,
                    ConversionRate = ri.ConversionRate,
                    BaseQuantity = ri.BaseQuantity,
                    RefundAmount = ri.LineRefundAmount,
                    Reason = ri.Reason
                });
                ii.ReturnedQuantity += ri.Quantity; // reserve
            }

            // 2) SP B: ExchangeItem (record only). KHÔNG xuất kho — chờ FinalizeReturn.
            foreach (var ni in prep.NewItems)
            {
                var bp = ni.BranchProduct;
                ro.ExchangeItems.Add(new ExchangeItem
                {
                    ExchangeItemId = Guid.NewGuid(),
                    ReturnOrderId = ro.ReturnOrderId,
                    BranchProductId = bp.BranchProductId,
                    ProductId = bp.ProductId,
                    Quantity = ni.Quantity,
                    ConversionRate = ni.ConversionRate,
                    BaseQuantity = ni.BaseQuantity,
                    UnitPrice = ni.UnitPrice,
                    LineTotal = ni.LineTotal
                });
            }

            // delta == 0 (đổi ngang giá) → Completed ngay sau khi tạo (move kho, KHÔNG tiền).
            // delta > 0 (lệch giá) → giữ Pending, chờ popup thanh toán (exchange/pay → Completed).
            if (delta == 0)
            {
                var stockErr = await ApplyExchangeDiffStockAsync(ro);
                if (stockErr != null)
                {
                    await tx.RollbackAsync();
                    return BadRequest(ApiErrorResponse.Create("MSG-81", stockErr));
                }
                ro.Status = "Completed";
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            // delta == 0 đã move kho ở trên → phát SignalR cập nhật tồn.
            // delta > 0 (Pending) kho chưa đổi — KHÔNG phát (chờ pay/confirm-transfer).
            if (delta == 0)
            {
                await NotifyExchangeDiffStockAsync(ro);
            }

            await TryWriteSystemLog(userId, "CREATE_EXCHANGE", "return_orders", "ReturnOrder",
                ro.ReturnOrderId, $"Tạo phiếu đổi hàng chênh lệch {ro.ReturnCode} - {(delta == 0 ? "Completed" : "Pending")} - Delta={delta:N0}đ");

            var full = await _context.ReturnOrders
                .Include(r => r.Order).ThenInclude(o => o!.Branch)
                .Include(r => r.Order).ThenInclude(o => o!.Customer)
                .Include(r => r.Order).ThenInclude(o => o!.Invoice)
                .Include(r => r.User)
                .Include(r => r.ReturnItems).ThenInclude(ri => ri.BranchProduct).ThenInclude(bp => bp!.Product).ThenInclude(p => p!.ProductConversionUnits)
                .Include(r => r.ExchangeItems).ThenInclude(ei => ei.BranchProduct).ThenInclude(bp => bp!.Product)
                .FirstOrDefaultAsync(r => r.ReturnOrderId == ro.ReturnOrderId);

            return CreatedAtAction(nameof(GetReturn), new { id = ro.ReturnOrderId }, _mapper.Map<ReturnOrderDto>(full));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            Console.WriteLine($"[Exchange API Error]: {ex}");
            return StatusCode(500, new { message = "Lỗi xử lý đổi hàng: " + ex.Message });
        }
    }
}
