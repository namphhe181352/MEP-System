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

    public ReturnsController(Models.MEPDatabaseContext context, IMapper mapper, IHubContext<PosHub> posHub)
    {
        _context = context;
        _mapper = mapper;
        _posHub = posHub;
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
            .Include(r => r.Order).ThenInclude(o => o!.Customer)
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
                RefundMethod = req.RefundMethod?.ToUpper() ?? "CASH",
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
            ConversionRate = req.ConversionRate > 0 ? req.ConversionRate : 1,
            BaseQuantity = req.Quantity * (req.ConversionRate > 0 ? req.ConversionRate : 1),
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
            .Include(r => r.ReturnItems).ThenInclude(ri => ri.BranchProduct).ThenInclude(bp => bp!.Product)
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
            foreach (var ri in ret.ReturnItems)
            {
                var bp = await _context.BranchProducts.FindAsync(ri.BranchProductId);
                if (bp != null)
                {
                    var baseQty = ri.BaseQuantity > 0 ? ri.BaseQuantity : ri.Quantity;
                    var isDefective = string.Equals(ri.Reason, "DEFECTIVE", StringComparison.OrdinalIgnoreCase);

                    if (isDefective)
                    {
                        // FIX L1: hàng LỖI không về sellable — giữ làm defective hold (WarrantyQuantity)
                        // + tạo WarrantyTicket (PENDING_ASSIGN) để gửi BH NCC claim lại.
                        bp.WarrantyQuantity += baseQty;

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
                            DocType = "DEFECTIVE_HOLD",
                            DocId = ret.ReturnOrderId,
                            DocCode = ret.ReturnCode,
                            TransactionTime = DateTime.UtcNow,
                            QtyChange = 0, // tồn vật lý giữ nguyên (hàng lỗi nằm trong ActualStock, chỉ đánh dấu hold)
                            UnitCostPrice = Math.Round(bp.CostPrice, 4),
                            TotalCostValue = 0,
                            StockAfter = bp.ActualStock,
                            AvgCostAfter = Math.Round(bp.CostPrice, 4),
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        // REFUND bình thường (không lỗi) → hàng về sellable
                        bp.AvailableStock += baseQty;
                        bp.ActualStock += baseQty;

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
        var refund = new RefundRecord
        {
            RefundId = Guid.NewGuid(),
            RefundCode = $"RFD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..4].ToUpper()}",
            OrderId = ret.OrderId,
            InvoiceId = (await _context.Invoices.FirstOrDefaultAsync(i => i.OrderId == ret.OrderId))?.InvoiceId,
            CustomerId = ret.Order?.CustomerId ?? Guid.Empty,
            UserId = ret.UserId,
            RefundAmount = ret.RefundAmount,
            RefundMethod = ret.RefundMethod ?? "CASH",
            Status = "REFUNDED",
            Reason = ret.Note,
            ProcessedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _context.RefundRecords.Add(refund);

        await _context.SaveChangesAsync();

        // SignalR: thông báo POS cập nhật tồn khả dụng khi đổi hàng (EXCHANGE)
        if (ret.ReturnType == "EXCHANGE")
        {
            try
            {
                foreach (var ri in ret.ReturnItems)
                {
                    var bp = await _context.BranchProducts.FindAsync(ri.BranchProductId);
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
            .FirstOrDefaultAsync(r => r.ReturnOrderId == id && r.Order!.BranchId == branchId);

        if (ret == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy phiếu đổi trả."));
        if (ret.Status != "Pending")
            return BadRequest(ApiErrorResponse.Create("MSG-78", "Chỉ phiếu đang chờ xử lý mới được hủy."));

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
}
