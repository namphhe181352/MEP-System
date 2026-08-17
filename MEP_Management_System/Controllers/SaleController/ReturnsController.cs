using System;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/returns")]
[Authorize]
[Tags("Đổi trả hàng (Return Order)")]
public class ReturnsController : ControllerBase
{
    private readonly IReturnService _service;

    private static readonly string BankAccountNumber = "0975849675";
    private static readonly string BankName = "MB Bank";

    public ReturnsController(IReturnService service)
    {
        _service = service;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found in token"));

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(userIdStr ?? throw new Exception("userId not found in token"));
    }

    private ActionResult ToBusinessError(BusinessException b) =>
        BadRequest(ApiErrorResponse.Create(b.Message, b.Errors?.FirstOrDefault() ?? b.Message));

    private ActionResult ToNotFound(KeyNotFoundException nf) =>
        NotFound(ApiErrorResponse.Create("MSG-01", nf.Message));

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
        var result = await _service.GetReturnsAsync(branchId, invoiceId, status, dateFrom, dateTo, page, pageSize);
        return Ok(result);
    }

    // GET /api/pos/returns/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReturnOrderDto>> GetReturn(Guid id)
    {
        var branchId = GetBranchId();
        var dto = await _service.GetReturnAsync(id, branchId);
        if (dto == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy phiếu đổi trả."));
        return Ok(dto);
    }

    // POST /api/pos/returns
    [HttpPost]
    public async Task<ActionResult<ReturnOrderDto>> CreateReturn([FromBody] CreateReturnOrderRequest req)
    {
        try
        {
            var dto = await _service.CreateReturnAsync(req, GetUserId(), GetBranchId());
            return CreatedAtAction(nameof(GetReturn), new { id = dto.ReturnOrderId }, dto);
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo phiếu trả hàng" });
        }
    }

    // POST /api/pos/returns/{id}/items
    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<ReturnItemDto>> AddReturnItem(Guid id, [FromBody] AddReturnItemRequest req)
    {
        try
        {
            var dto = await _service.AddReturnItemAsync(id, req, GetBranchId());
            return Ok(dto);
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
    }

    // POST /api/pos/returns/{id}/finalize
    [HttpPost("{id:guid}/finalize")]
    public async Task<ActionResult<ReturnOrderDto>> FinalizeReturn(Guid id, [FromBody] FinalizeReturnRequest req)
    {
        try
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
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
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
    }

    // PATCH /api/pos/returns/{id}/cancel
    [HttpPatch("{id:guid}/cancel")]
    public async Task<ActionResult> CancelReturn(Guid id)
    {
        try
        {
            await _service.CancelReturnAsync(id, GetUserId(), GetBranchId());
            return Ok(new { message = "Đã hủy phiếu đổi trả." });
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
    }

    // POST /api/pos/returns/{id}/exchange/pay
    /// <summary>
    /// Thanh toán chênh lệch đổi hàng (delta&gt;0).
    /// CASH → tạo Payment Confirmed (cashReceived/changeAmount) + move kho + RO Completed.
    /// TRANSFER → sinh QR (VietQR) + Payment Pending, RO vẫn Pending chờ confirm-transfer.
    /// </summary>
    [HttpPost("{id:guid}/exchange/pay")]
    public async Task<ActionResult> PayExchangeDiff(Guid id, [FromBody] PayExchangeRequest req)
    {
        try
        {
            var res = await _service.PayExchangeDiffAsync(id, req, GetUserId(), GetBranchId());

            if (string.Equals(res.Method, "CASH", StringComparison.OrdinalIgnoreCase))
            {
                var cashReceived = req?.CashReceived ?? 0m;
                return Ok(new
                {
                    returnOrder = res.ReturnOrder,
                    method = "CASH",
                    paidAmount = res.Amount,
                    cashReceived = cashReceived,
                    changeAmount = Math.Max(0m, cashReceived - res.Amount)
                });
            }

            // TRANSFER
            return Ok(new
            {
                paymentId = res.PaymentId,
                qrImageBase64 = res.QRImageBase64,
                transactionContent = res.TransactionContent,
                amount = res.Amount,
                bankAccountNumber = BankAccountNumber,
                bankName = BankName,
                returnOrderId = res.ReturnOrderId,
                method = "TRANSFER"
            });
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
    }

    // POST /api/pos/returns/{id}/exchange/confirm-transfer
    /// <summary>
    /// Xác nhận khách đã chuyển khoản → Payment Confirmed + move kho + RO Completed.
    /// </summary>
    [HttpPost("{id:guid}/exchange/confirm-transfer")]
    public async Task<ActionResult<ReturnOrderDto>> ConfirmExchangeTransfer(Guid id, [FromBody] ConfirmTransferRequest? req)
    {
        try
        {
            var dto = await _service.ConfirmExchangeTransferAsync(id, req, GetUserId(), GetBranchId());
            return Ok(dto);
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
    }

    // POST /api/pos/returns/{id}/exchange/cancel-payment
    /// <summary>
    /// Huỷ Payment chuyển khoản đang chờ (khi bỏ QR / đổi sang method khác). RO vẫn Pending.
    /// </summary>
    [HttpPost("{id:guid}/exchange/cancel-payment")]
    public async Task<ActionResult> CancelExchangePayment(Guid id)
    {
        try
        {
            await _service.CancelExchangePaymentAsync(id, GetBranchId());
            return Ok(new { message = "Đã huỷ thanh toán chênh lệch đang chờ." });
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
    }

    // GET /api/pos/returns/return-policies
    /// <summary>
    /// Trả về danh sách chính sách đổi/trả theo nhóm hàng của chi nhánh hiện tại.
    /// </summary>
    [HttpGet("return-policies")]
    public async Task<ActionResult<List<CategoryPolicyDto>>> GetReturnPolicies()
    {
        return Ok(await _service.GetReturnPoliciesAsync(GetBranchId()));
    }

    // GET /api/pos/returns/category-policies
    [HttpGet("category-policies")]
    public async Task<ActionResult<List<CategoryPolicyDto>>> GetCategoryPolicies()
    {
        return Ok(await _service.GetCategoryPoliciesAsync(GetBranchId()));
    }

    // GET /api/pos/returns/discount-percent
    [HttpGet("discount-percent")]
    public async Task<ActionResult<object>> GetReturnDiscount()
    {
        return Ok(await _service.GetReturnDiscountAsync(GetBranchId()));
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
    /// </summary>
    [HttpGet("invoice/{invoiceId:guid}/eligible-items")]
    public async Task<ActionResult<InvoiceEligibilityResponse>> GetEligibleItems(Guid invoiceId)
    {
        try
        {
            return Ok(await _service.GetEligibleItemsAsync(invoiceId, GetBranchId()));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(ApiErrorResponse.Create("MSG-01", "Không tìm thấy hóa đơn."));
        }
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

        // SP A: hàng trả về lại kho bán (CUSTOMER_RETURN). Không sinh bảo hành.
        foreach (var ri in ret.ReturnItems)
        {
            var bp = await _context.BranchProducts.FindAsync(ri.BranchProductId);
            if (bp == null) continue;
            var baseQty = ri.BaseQuantity > 0 ? ri.BaseQuantity : ri.Quantity;
            bp.AvailableStock += baseQty;
            bp.ActualStock += baseQty;
            _context.InventoryLedgers.Add(MkLedger(bp, "CUSTOMER_RETURN", ret.ReturnOrderId, ret.ReturnCode, baseQty));
        }

        // SP B: xuất kho (EXCHANGE_OUT)
        foreach (var ei in ret.ExchangeItems)
        {
            var bp = await _context.BranchProducts.FindAsync(ei.BranchProductId);
            if (bp == null) continue;
            var baseQty = ei.BaseQuantity > 0 ? ei.BaseQuantity : ei.Quantity;
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
        return Ok(await _service.QuoteExchangeAsync(req, GetBranchId()));
    }

    // POST /api/pos/returns/exchange
    /// <summary>Thực thi đổi hàng chênh lệch đầy đủ (validate + tạo ReturnOrder(EXCHANGE) + ExchangeItems + stock moves + ReturnedQuantity) trong 1 transaction.</summary>
    [HttpPost("exchange")]
    public async Task<ActionResult<ReturnOrderDto>> CreateExchange([FromBody] CreateExchangeRequest req)
    {
        try
        {
            var dto = await _service.CreateExchangeAsync(req, GetUserId(), GetBranchId());
            return CreatedAtAction(nameof(GetReturn), new { id = dto.ReturnOrderId }, dto);
        }
        catch (BusinessException b)
        {
            return ToBusinessError(b);
        }
        catch (KeyNotFoundException nf)
        {
            return ToNotFound(nf);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi xử lý đổi hàng: " + ex.Message });
        }
    }
}