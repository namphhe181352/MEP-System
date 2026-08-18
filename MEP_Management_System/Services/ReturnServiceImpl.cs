using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Hubs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Exceptions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

/// <summary>
/// Service implementing return order business logic.
/// </summary>
public class ReturnServiceImpl : IReturnService
{
    private readonly MEPDatabaseContext _context;
    private readonly IMapper _mapper;
    private readonly IHubContext<PosHub> _posHub;
    private readonly VietQrService _vietQr;

    public ReturnServiceImpl(
        MEPDatabaseContext context,
        IMapper mapper,
        IHubContext<PosHub> posHub,
        VietQrService vietQr)
    {
        _context = context;
        _mapper = mapper;
        _posHub = posHub;
        _vietQr = vietQr;
    }

    public async Task<PageResultDto<ReturnOrderDto>> GetReturnsAsync(
        Guid branchId, Guid? invoiceId, string? status,
        DateTime? dateFrom, DateTime? dateTo, int page, int pageSize)
    {
        var query = _context.ReturnOrders
            .Include(r => r.Order).ThenInclude(o => o!.Branch)
            .Include(r => r.Order).ThenInclude(o => o!.Invoice)
            .Include(r => r.Order).ThenInclude(o => o!.Customer)
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

        var dtos = items.Select(r => _mapper.Map<ReturnOrderDto>(r)).ToList();

        // Nạp CashReceived/ChangeAmount từ Payment liên kết (cho đơn đổi chênh dương CASH)
        var paymentIds = dtos.Where(d => d.PaymentId.HasValue).Select(d => d.PaymentId!.Value).Distinct().ToList();
        if (paymentIds.Count > 0)
        {
            var payments = await _context.Payments
                .Where(p => paymentIds.Contains(p.PaymentId))
                .ToDictionaryAsync(p => p.PaymentId);
            foreach (var d in dtos)
            {
                if (d.PaymentId.HasValue && payments.TryGetValue(d.PaymentId.Value, out var pay))
                {
                    d.CashReceived = pay.CashReceived;
                    d.ChangeAmount = pay.ChangeAmount;
                }
            }
        }

        return new PageResultDto<ReturnOrderDto>
        {
            Items = dtos,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<ReturnOrderDto?> GetReturnAsync(Guid id, Guid branchId)
    {
        var r = await _context.ReturnOrders
            .Include(r => r.Order).ThenInclude(o => o!.Branch)
            .Include(r => r.Order).ThenInclude(o => o!.Invoice)
            .Include(r => r.Order).ThenInclude(o => o!.Customer)
            .Include(r => r.User)
            .Include(r => r.ReturnItems).ThenInclude(ri => ri.BranchProduct).ThenInclude(bp => bp!.Product).ThenInclude(p => p!.ProductConversionUnits)
            .Include(r => r.ExchangeItems).ThenInclude(ei => ei.BranchProduct).ThenInclude(bp => bp!.Product)
            .FirstOrDefaultAsync(r => r.ReturnOrderId == id && r.Order!.BranchId == branchId);

        if (r == null) return null;

        var dto = _mapper.Map<ReturnOrderDto>(r);

        // Nạp CashReceived/ChangeAmount từ Payment liên kết
        if (r.PaymentId.HasValue)
        {
            var pay = await _context.Payments.FindAsync(r.PaymentId.Value);
            if (pay != null)
            {
                dto.CashReceived = pay.CashReceived;
                dto.ChangeAmount = pay.ChangeAmount;
            }
        }

        return dto;
    }

    public async Task<ReturnOrderDto> CreateReturnAsync(CreateReturnOrderRequest req, Guid userId, Guid branchId)
    {
        // Validate invoice exists and belongs to branch
        var invoice = await _context.Invoices
            .Include(i => i.Order).ThenInclude(o => o!.Branch)
            .Include(i => i.Order).ThenInclude(o => o!.Customer)
            .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp!.Product).ThenInclude(p => p!.CategoryTaxonomy)
            .FirstOrDefaultAsync(i => i.InvoiceId == req.InvoiceId && i.Order!.BranchId == branchId);

        if (invoice == null)
            throw new KeyNotFoundException("Không tìm thấy hóa đơn gốc.");

        // Chặn tạo đổi/trả (và gián tiếp chặn luồng bảo hành khiếu nại) cho đơn bán đã bị hủy
        // (do hủy phiếu xuất bán → hóa đơn/đơn hàng = "Cancelled"). Không động đến dữ liệu cũ.
        if (string.Equals(invoice.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("MSG-78", "Đơn bán này đã bị hủy (phiếu xuất bán đã hủy). Không thể tạo đổi/trả hoặc bảo hành cho đơn đã hủy.");

        if (invoice.Status != "Completed")
            throw new BusinessException("MSG-78", "Chỉ hóa đơn đã hoàn tất mới được đổi trả.");

        // Check return window from branch settings
        var branch = invoice.Order?.Branch;
        var allowedDays = branch?.ReturnDaysAllowed ?? (int?)30;
        var invoiceDate = invoice.CreatedAt ?? DateTime.UtcNow;
        if (allowedDays.HasValue && invoiceDate.AddDays(allowedDays.Value) < DateTime.UtcNow)
            throw new BusinessException("MSG-65", $"Đã quá thời hạn {allowedDays.Value} ngày kể từ ngày mua. Không thể đổi trả.");

        // Check no pending return exists
        var existing = await _context.ReturnOrders
            .AnyAsync(r => r.OrderId == invoice.OrderId && r.Status == "Pending");
        if (existing)
            throw new BusinessException("MSG-66", "Hóa đơn này đang có phiếu đổi trả đang xử lý.");

        // Get category policies
        var policies = await GetCategoryPoliciesListAsync(branchId);
        if (policies == null || policies.Count == 0)
            throw new BusinessException("MSG-80", "Chưa có chính sách đổi/trả theo nhóm hàng. Vui lòng thiết lập trong phần Cài đặt trước khi thực hiện đổi/trả.");

        var returnType = req.ReturnType?.ToUpper() ?? "REFUND";
        var hasEligibleItem = false;
        var ineligibleReasons = new List<string>();

        foreach (var invoiceItem in invoice.InvoiceItems)
        {
            var product = invoiceItem.BranchProduct?.Product;
            var categoryId = product?.CategoryTaxonomyId;

            CategoryPolicyDto? itemPolicy = null;
            if (categoryId.HasValue)
            {
                itemPolicy = policies.FirstOrDefault(c =>
                    c.CategoryId.HasValue && c.CategoryId.Value == categoryId.Value);
            }
            if (itemPolicy == null)
            {
                var categoryName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName;
                if (!string.IsNullOrEmpty(categoryName))
                {
                    itemPolicy = policies.FirstOrDefault(c =>
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
                ineligibleReasons.Add($"\"{product?.ProductName}\" (nhóm \"{displayName}\"): đã quá hạn {policyAllowedDays.Value} ngày");
            }
        }

        if (!hasEligibleItem)
            throw new BusinessException("MSG-80",
                $"Hóa đơn này không có sản phẩm nào được phép {(returnType == "EXCHANGE" ? "đổi" : "trả")}. " +
                string.Join("; ", ineligibleReasons.Take(3)) +
                (ineligibleReasons.Count > 3 ? $" (và {ineligibleReasons.Count - 3} sản phẩm khác)" : ""));

        var entity = new ReturnOrder
        {
            ReturnOrderId = Guid.NewGuid(),
            ReturnCode = await GenCodeAsync("RET"),
            OrderId = invoice.OrderId,
            UserId = userId,
            RefundMethod = returnType == "REFUND" ? "CASH" : (req.RefundMethod?.ToUpper() ?? "CASH"),
            ReturnType = returnType,
            Status = "Pending",
            Note = req.Note,
            CreatedAt = DateTime.UtcNow
        };

        _context.ReturnOrders.Add(entity);
        await LogActionAsync(userId, "CREATE_RETURN_ORDER", "return_orders", "ReturnOrder",
            entity.ReturnOrderId, $"Tao phieu doi tra {entity.ReturnCode} - {entity.RefundMethod ?? "-"}");
        await _context.SaveChangesAsync();

        return _mapper.Map<ReturnOrderDto>(entity);
    }

    public async Task<ReturnItemDto> AddReturnItemAsync(Guid id, AddReturnItemRequest req, Guid branchId)
    {
        var ret = await _context.ReturnOrders
            .Include(r => r.Order)
            .FirstOrDefaultAsync(r => r.ReturnOrderId == id && r.Order!.BranchId == branchId);

        if (ret == null)
            throw new KeyNotFoundException("Không tìm thấy phiếu đổi trả.");

        if (ret.Status != "Pending")
            throw new BusinessException("MSG-78", "Chỉ phiếu đang chờ xử lý mới được thêm sản phẩm.");

        // Validate product belongs to original invoice
        var originalItems = await _context.InvoiceItems
            .Include(ii => ii.Invoice)
            .Include(ii => ii.BranchProduct).ThenInclude(bp => bp.Product).ThenInclude(p => p.CategoryTaxonomy)
            .Where(ii => ii.Invoice != null && ii.Invoice.OrderId == ret.OrderId && ii.BranchProduct!.ProductId == req.ProductId)
            .ToListAsync();

        if (!originalItems.Any())
            throw new BusinessException("MSG-59", "Sản phẩm này không có trong hóa đơn gốc.");

        // Chặn thêm dòng trả cho đơn đã bị hủy (trường hợp hủy sale sau khi tạo phiếu đổi trả chờ).
        var inv0 = originalItems.First().Invoice;
        if (inv0 != null && string.Equals(inv0.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("MSG-78", "Đơn bán này đã bị hủy (phiếu xuất bán đã hủy). Không thể đổi/trả hoặc bảo hành cho đơn đã hủy.");

        var branchProductId = originalItems.First().BranchProductId;

        var totalBought = originalItems.Sum(ii => ii.Quantity);
        var totalReturned = await _context.ReturnItems
            .Where(ri => ri.ReturnOrderId == id && ri.BranchProductId == branchProductId)
            .SumAsync(ri => ri.Quantity);

        if (totalReturned + req.Quantity > totalBought)
            throw new BusinessException("MSG-66", $"Số lượng trả vượt quá số lượng đã mua. Tối đa: {totalBought - totalReturned}");

        // Category-level policy check
        var product = originalItems.First().BranchProduct?.Product;
        var policies = await GetCategoryPoliciesListAsync(branchId);

        if (policies == null || policies.Count == 0)
            throw new BusinessException("MSG-80",
                "Chưa có chính sách đổi/trả theo nhóm hàng. Vui lòng thiết lập trong phần Cài đặt trước khi thực hiện đổi/trả.");

        CategoryPolicyDto? categoryPolicy = null;
        var categoryId = product?.CategoryTaxonomyId;

        if (categoryId.HasValue)
        {
            categoryPolicy = policies.FirstOrDefault(c =>
                c.CategoryId.HasValue && c.CategoryId.Value == categoryId.Value);
        }

        if (categoryPolicy == null)
        {
            var categoryName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName;
            if (!string.IsNullOrEmpty(categoryName))
            {
                categoryPolicy = policies.FirstOrDefault(c =>
                    c.CategoryName != null && c.CategoryName.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (categoryPolicy == null)
        {
            var displayName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName ?? "(không xác định)";
            throw new BusinessException("MSG-80",
                $"Sản phẩm \"{product?.ProductName}\" thuộc nhóm \"{displayName}\" không được phép {(ret.ReturnType == "EXCHANGE" ? "đổi" : "trả")}.");
        }

        var allowedDays = ret.ReturnType == "EXCHANGE"
            ? categoryPolicy.ExchangeDays
            : categoryPolicy.ReturnDays;

        if (!allowedDays.HasValue || allowedDays.Value <= 0)
        {
            var displayName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName ?? "(không xác định)";
            throw new BusinessException("MSG-80",
                $"Sản phẩm \"{product?.ProductName}\" thuộc nhóm \"{displayName}\" không được phép {(ret.ReturnType == "EXCHANGE" ? "đổi" : "trả")}.");
        }

        var invoiceDate = originalItems.First().Invoice?.CreatedAt ?? DateTime.UtcNow;
        if (invoiceDate.AddDays(allowedDays.Value) < DateTime.UtcNow)
        {
            var displayName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName ?? "(không xác định)";
            throw new BusinessException("MSG-79",
                $"Sản phẩm \"{product?.ProductName}\" thuộc nhóm \"{displayName}\" đã quá thời hạn {allowedDays.Value} ngày. Không thể đổi trả.");
        }

        var avgPrice = originalItems.Sum(ii => ii.SellPrice * ii.Quantity) / totalBought;
        var grossRefundLine = req.Quantity * avgPrice;

        // === Áp dụng chiết khấu HÓA ĐƠN GỐC: hoàn theo GIÁ KHÁCH THỰC TRẢ ===
        //    Dùng tỉ lệ TotalAmount/SubTotalAmount để đúng cho cả CK % và CK số tiền.
        var inv = originalItems.First().Invoice;
        decimal invoiceDiscountRatio = 0m;
        if (inv != null && inv.SubTotalAmount > 0m)
            invoiceDiscountRatio = Math.Max(0m, 1m - inv.TotalAmount / inv.SubTotalAmount);
        var paidRefundLine = Math.Max(0m, grossRefundLine * (1m - invoiceDiscountRatio));

        // === Áp dụng chiết khấu trả hàng branch-level.
        //    QUY TẮC: Chiết khấu CHỈ áp dụng cho luồng TRẢ HÀNG (REFUND).
        //    Luồng ĐỔI (EXCHANGE / đổi lệch giá) KHÔNG trừ chiết khấu — tính giá trị gốc.
        decimal refundLine;
        if (ret.ReturnType == "EXCHANGE")
        {
            refundLine = grossRefundLine;
        }
        else
        {
            var returnDiscountPercent = await GetReturnDiscountPercentAsync(branchId);
            var discountRatio = returnDiscountPercent > 0m ? returnDiscountPercent / 100m : 0m;
            var discountPortion = paidRefundLine * discountRatio;
            refundLine = Math.Max(0, paidRefundLine - discountPortion);
        }

        // Determine conversion rate
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

        return _mapper.Map<ReturnItemDto>(returnItem);
    }

    public async Task<ReturnOrderDto> FinalizeReturnAsync(Guid id, FinalizeReturnRequest? req, Guid userId, Guid branchId)
    {
        var ret = await _context.ReturnOrders
            .Include(r => r.Order).ThenInclude(o => o!.Branch)
            .Include(r => r.Order).ThenInclude(o => o!.Customer)
            .Include(r => r.Order).ThenInclude(o => o!.Invoice)
            .Include(r => r.User)
            .Include(r => r.ReturnItems).ThenInclude(ri => ri.BranchProduct).ThenInclude(bp => bp!.Product).ThenInclude(p => p!.ProductConversionUnits)
            .Include(r => r.ExchangeItems).ThenInclude(ei => ei.BranchProduct).ThenInclude(bp => bp!.Product)
            .FirstOrDefaultAsync(r => r.ReturnOrderId == id && r.Order!.BranchId == branchId);

        if (ret == null)
            throw new KeyNotFoundException("Không tìm thấy phiếu đổi trả.");

        if (ret.Status != "Pending")
            throw new BusinessException("MSG-78", "Phiếu đổi trả không ở trạng thái chờ xử lý.");

        if (!ret.ReturnItems.Any())
            throw new BusinessException("MSG-61", "Phiếu đổi trả phải có ít nhất 1 sản phẩm.");

        // Chặn hoàn tất đổi/trả nếu đơn bán gốc đã bị hủy (phiếu xuất bán bị hủy sau khi tạo phiếu chờ).
        var retInvoice = ret.Order?.Invoice;
        if (retInvoice != null && string.Equals(retInvoice.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            throw new BusinessException("MSG-78", "Đơn bán này đã bị hủy (phiếu xuất bán đã hủy). Không thể hoàn tất đổi/trả hoặc bảo hành cho đơn đã hủy.");

        // Calculate total refund
        ret.RefundAmount = ret.ReturnItems.Sum(ri => ri.RefundAmount ?? 0);
        ret.Status = "Completed";
        if (req?.Note != null) ret.Note = req.Note;

        // Process REFUND or EXCHANGE
        if (ret.ReturnType == "REFUND")
        {
            await ProcessRefundStockAsync(ret, userId);
        }
        else if (ret.ReturnType == "EXCHANGE" && ret.ExchangeItems.Any())
        {
            var exDelta = ret.DeltaAmount ?? 0m;
            if (exDelta > 0)
                throw new BusinessException("MSG-78", "Đơn đổi chênh lệch (thu thêm) phải thanh toán qua popup, không xác nhận trực tiếp.");

            var stockErr = await ApplyExchangeDiffStockAsync(ret, userId);
            if (stockErr != null)
                throw new BusinessException("MSG-81", stockErr);
        }
        else if (ret.ReturnType == "EXCHANGE")
        {
            await ProcessExchangeStockAsync(ret, userId);
        }

        // Create RefundRecord for shift cash balancing
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
                CustomerId = ret.Order?.CustomerId,
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

        // SignalR: notify POS of stock update
        if (ret.ReturnType == "EXCHANGE")
        {
            await NotifyExchangeDiffStockAsync(ret);
        }

        await LogActionAsync(userId, "FINALIZE_RETURN_ORDER", "return_orders", "ReturnOrder",
            ret.ReturnOrderId, $"Hoan tat doi tra {ret.ReturnCode} - Hoan tien: {ret.RefundAmount:N0}d");

        return _mapper.Map<ReturnOrderDto>(ret);
    }

    public async Task<bool> CancelReturnAsync(Guid id, Guid userId, Guid branchId)
    {
        var ret = await _context.ReturnOrders
            .Include(r => r.Order)
            .Include(r => r.ReturnItems)
            .Include(r => r.ExchangeItems)
            .FirstOrDefaultAsync(r => r.ReturnOrderId == id && r.Order!.BranchId == branchId);

        if (ret == null)
            throw new KeyNotFoundException("Không tìm thấy phiếu đổi trả.");

        if (ret.Status != "Pending")
            throw new BusinessException("MSG-78", "Chỉ phiếu đang chờ xử lý mới được hủy.");

        // Restore reserved quantities for exchange
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

            // Cancel pending transfer payment
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

        await LogActionAsync(userId, "CANCEL_RETURN_ORDER", "return_orders", "ReturnOrder",
            ret.ReturnOrderId, $"Huy phieu doi tra {ret.ReturnCode}");

        return true;
    }

    public async Task<ExchangeQuoteResponse> QuoteExchangeAsync(ExchangeQuoteRequest req, Guid branchId)
    {
        var prep = await PrepareExchangeAsync(req.InvoiceId, req.ReturnItems, req.NewItems, branchId);
        return new ExchangeQuoteResponse
        {
            Eligible = prep.Eligible,
            RestrictionReason = prep.RestrictionReason,
            RuleBreak = prep.RuleBreak,
            OldValue = prep.OldValue,
            NewValue = prep.NewValue,
            DeltaAmount = prep.Delta,
            DeltaLabel = prep.DeltaLabel,
            StockCheck = prep.StockCheck
        };
    }

    public async Task<ReturnOrderDto> CreateExchangeAsync(CreateExchangeRequest req, Guid userId, Guid branchId)
    {
        var prep = await PrepareExchangeAsync(req.InvoiceId, req.ReturnItems, req.NewItems, branchId);
        if (!prep.Eligible || prep.Invoice == null)
            throw new BusinessException("MSG-80", prep.RestrictionReason ?? "Không đủ điều kiện đổi hàng.");

        if (prep.Returns.Count == 0)
            throw new BusinessException("MSG-61", "Phải có ít nhất 1 sản phẩm đổi (SP A).");

        if (prep.NewItems.Count == 0)
            throw new BusinessException("MSG-61", "Phải có ít nhất 1 sản phẩm mới (SP B).");

        var payMethod = (req.PaymentMethod?.ToUpper() ?? "CASH");
        if (prep.Delta != 0 && payMethod != "CASH" && payMethod != "TRANSFER")
            throw new BusinessException("MSG-78", "Phương thức thanh toán không hợp lệ (CASH/TRANSFER).");

        var invoice = prep.Invoice;
        var delta = prep.Delta;

        // Prevent duplicate pending returns
        var hasPending = await _context.ReturnOrders
            .AnyAsync(r => r.OrderId == invoice.OrderId && r.Status == "Pending");
        if (hasPending)
            throw new BusinessException("MSG-66", "Hóa đơn này đang có phiếu đổi trả đang xử lý.");

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

            // SP A: ReturnItem + reserve ReturnedQuantity
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
                ii.ReturnedQuantity += ri.Quantity;
            }

            // SP B: ExchangeItem
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

            // delta == 0: Complete immediately
            if (delta == 0)
            {
                var stockErr = await ApplyExchangeDiffStockAsync(ro, userId);
                if (stockErr != null)
                {
                    await tx.RollbackAsync();
                    throw new BusinessException("MSG-81", stockErr);
                }
                ro.Status = "Completed";
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            if (delta == 0)
            {
                await NotifyExchangeDiffStockAsync(ro);
            }

            await LogActionAsync(userId, "CREATE_EXCHANGE", "return_orders", "ReturnOrder",
                ro.ReturnOrderId, $"Tao phieu doi hang chenh lech {ro.ReturnCode} - {(delta == 0 ? "Completed" : "Pending")} - Delta={delta:N0}d");

            var full = await _context.ReturnOrders
                .Include(r => r.Order).ThenInclude(o => o!.Branch)
                .Include(r => r.Order).ThenInclude(o => o!.Customer)
                .Include(r => r.Order).ThenInclude(o => o!.Invoice)
                .Include(r => r.User)
                .Include(r => r.ReturnItems).ThenInclude(ri => ri.BranchProduct).ThenInclude(bp => bp!.Product).ThenInclude(p => p!.ProductConversionUnits)
                .Include(r => r.ExchangeItems).ThenInclude(ei => ei.BranchProduct).ThenInclude(bp => bp!.Product)
                .FirstOrDefaultAsync(r => r.ReturnOrderId == ro.ReturnOrderId);

            return _mapper.Map<ReturnOrderDto>(full!);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<PayExchangeResultDto> PayExchangeDiffAsync(Guid id, PayExchangeRequest? req, Guid userId, Guid branchId)
    {
        var (ret, err) = await LoadPendingExchangeDiffAsync(id, branchId);
        if (err != null)
            throw new BusinessException("MSG-78", err);

        var delta = ret!.DeltaAmount ?? 0m;
        var method = (req?.Method?.ToUpper() ?? ret.PaymentMethod ?? "CASH");
        if (method != "CASH" && method != "TRANSFER")
            throw new BusinessException("MSG-78", "Phương thức thanh toán không hợp lệ (CASH/TRANSFER).");

        var openShiftId = await _context.Shifts
            .Where(s => s.BranchId == branchId && s.Status == "OPEN")
            .Select(s => (Guid?)s.ShiftId)
            .FirstOrDefaultAsync();

        if (method == "CASH")
        {
            if (req!.CashReceived == null || req.CashReceived < delta)
                throw new BusinessException("MSG-76", "Tiền khách đưa nhỏ hơn số phải trả.");

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
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
                    InvoiceId = ret.Order?.Invoice?.InvoiceId,
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

                var stockErr = await ApplyExchangeDiffStockAsync(ret, userId);
                if (stockErr != null)
                    throw new BusinessException("MSG-81", stockErr);

                ret.Status = "Completed";
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            await LogActionAsync(userId, "PAY_EXCHANGE_DIFF", "return_orders", "ReturnOrder",
                ret.ReturnOrderId, $"Thanh toán tiền mặt đổi chênh {ret.ReturnCode} - Completed - {delta:N0}đ");
            await NotifyExchangeDiffStockAsync(ret);

            return new PayExchangeResultDto
            {
                Payments = new List<object>
                {
                    new
                    {
                        method = "CASH",
                        paidAmount = delta,
                        cashReceived = req.CashReceived!.Value,
                        changeAmount = Math.Max(0m, req.CashReceived.Value - delta)
                    }
                },
                ReturnOrder = _mapper.Map<ReturnOrderDto>(ret),
                Method = "CASH",
                Amount = delta,
                ReturnOrderId = ret.ReturnOrderId,
                PaymentId = ret.PaymentId,
                TransactionContent = null
            };
        }

        // TRANSFER: generate QR
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
                            throw new BusinessException("MSG-78", "Đơn đã thanh toán xong.");
                        if (p.Status == "Pending")
                        {
                            p.VietQRString = transactionContent;
                            p.QRExpiredAt = DateTime.UtcNow.AddMinutes(5);
                            p.Amount = delta;
                        }
                        else
                        {
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
                        InvoiceId = ret.Order?.Invoice?.InvoiceId,
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
            catch
            {
                await tx2.RollbackAsync();
                throw;
            }
        }

        await LogActionAsync(userId, "PAY_EXCHANGE_DIFF_QR", "return_orders", "ReturnOrder",
            ret.ReturnOrderId, $"Tạo QR chuyển khoản đổi chênh {ret.ReturnCode} - {delta:N0}đ");

        return new PayExchangeResultDto
        {
            VietQRString = transactionContent,
            QRImageBase64 = qrBase64,
            ReturnOrder = null,
            Method = "TRANSFER",
            Amount = delta,
            ReturnOrderId = ret.ReturnOrderId,
            PaymentId = p.PaymentId,
            TransactionContent = transactionContent
        };
    }

    public async Task<ReturnOrderDto> ConfirmExchangeTransferAsync(Guid id, ConfirmTransferRequest? req, Guid userId, Guid branchId)
    {
        var (ret, err) = await LoadPendingExchangeDiffAsync(id, branchId);
        if (err != null)
            throw new BusinessException("MSG-78", err!);

        if (!ret!.PaymentId.HasValue)
            throw new BusinessException("MSG-78", "Chưa có thanh toán chuyển khoản cho phiếu này.");

        var p = await _context.Payments.FindAsync(ret.PaymentId.Value);
        if (p == null || p.Method != "Transfer")
            throw new BusinessException("MSG-78", "Không tìm thấy Payment chuyển khoản hợp lệ.");
        if (p.Status == "Confirmed")
            throw new BusinessException("MSG-76", "Thanh toán đã được xác nhận.");
        if (req?.PaymentId != null && req.PaymentId != p.PaymentId)
            throw new BusinessException("MSG-78", "Payment không khớp với phiếu đổi trả.");
        if (p.QRExpiredAt.HasValue && p.QRExpiredAt.Value < DateTime.UtcNow)
            throw new BusinessException("MSG-76", "Mã QR đã hết hạn. Vui lòng tạo lại.");

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            p.Status = "Confirmed";
            p.PaidAt = DateTime.UtcNow;
            ret.PaymentMethod = "TRANSFER";

            var stockErr = await ApplyExchangeDiffStockAsync(ret, userId);
            if (stockErr != null)
                throw new BusinessException("MSG-81", stockErr);

            ret.Status = "Completed";
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        await LogActionAsync(userId, "CONFIRM_EXCHANGE_TRANSFER", "return_orders", "ReturnOrder",
            ret.ReturnOrderId, $"Xác nhận chuyển khoản đổi chênh {ret.ReturnCode} - Completed");
        await NotifyExchangeDiffStockAsync(ret);

        return _mapper.Map<ReturnOrderDto>(ret);
    }

    public async Task<bool> CancelExchangePaymentAsync(Guid id, Guid branchId)
    {
        var (ret, err) = await LoadPendingExchangeDiffAsync(id, branchId);
        if (err != null)
            throw new BusinessException("MSG-78", err!);

        if (ret!.PaymentId.HasValue)
        {
            var p = await _context.Payments.FindAsync(ret.PaymentId.Value);
            if (p != null && p.Status == "Pending")
                p.Status = "Cancelled";
            ret.PaymentId = null;
            await _context.SaveChangesAsync();
        }

        return true;
    }

    public async Task<List<CategoryPolicyDto>> GetReturnPoliciesAsync(Guid branchId)
    {
        var config = await _context.SystemConfigs
            .FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "CATEGORY_RETURN_POLICIES");

        if (config == null || string.IsNullOrWhiteSpace(config.ConfigValue))
            return new List<CategoryPolicyDto>();

        try
        {
            var deserialized = JsonSerializer.Deserialize<List<CategoryPolicyDto>>(config.ConfigValue,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return deserialized ?? new List<CategoryPolicyDto>();
        }
        catch
        {
            return new List<CategoryPolicyDto>();
        }
    }

    public async Task<List<CategoryPolicyDto>> GetCategoryPoliciesAsync(Guid branchId)
    {
        return await GetReturnPoliciesAsync(branchId);
    }

    public async Task<object> GetReturnDiscountAsync(Guid branchId)
    {
        var percent = await GetReturnDiscountPercentAsync(branchId);
        return new { returnDiscountPercent = percent };
    }

    public async Task<InvoiceEligibilityResponse> GetEligibleItemsAsync(Guid invoiceId, Guid branchId)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Order)
            .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp!.Product).ThenInclude(p => p!.CategoryTaxonomy)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order!.BranchId == branchId);

        if (invoice == null)
            throw new KeyNotFoundException("Không tìm thấy hóa đơn.");

        var policies = await GetCategoryPoliciesListAsync(branchId);
        var invoiceDate = invoice.CreatedAt ?? DateTime.UtcNow;
        // Đơn đã hủy: toàn bộ dòng đều không được đổi/trả (gián tiếp chặn luồng bảo hành khiếu nại).
        var invoiceCancelled = string.Equals(invoice.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);
        var items = new List<InvoiceItemEligibilityDto>();

        foreach (var invoiceItem in invoice.InvoiceItems)
        {
            var product = invoiceItem.BranchProduct?.Product;
            var categoryId = product?.CategoryTaxonomyId;
            var categoryName = product?.CategoryTaxonomy?.Name ?? product?.CategoryName;

            CategoryPolicyDto? itemPolicy = null;
            if (policies != null)
            {
                if (categoryId.HasValue)
                {
                    itemPolicy = policies.FirstOrDefault(c =>
                        c.CategoryId.HasValue && c.CategoryId.Value == categoryId.Value);
                }
                if (itemPolicy == null && !string.IsNullOrEmpty(categoryName))
                {
                    itemPolicy = policies.FirstOrDefault(c =>
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
                if (itemPolicy.ReturnDays.HasValue && itemPolicy.ReturnDays.Value > 0)
                {
                    var refundDeadline = invoiceDate.AddDays(itemPolicy.ReturnDays.Value);
                    if (refundDeadline >= DateTime.UtcNow)
                    {
                        eligibility.RefundAllowed = true;
                        eligibility.RemainingRefundDays = (int)(refundDeadline - DateTime.UtcNow).TotalDays;
                    }
                }

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

            // Override: đơn đã hủy → không đổi/trả/bảo hành.
            if (invoiceCancelled)
            {
                eligibility.RefundAllowed = false;
                eligibility.ExchangeAllowed = false;
                eligibility.RemainingRefundDays = 0;
                eligibility.RemainingExchangeDays = 0;
                eligibility.RestrictionReason = "Đơn bán này đã bị hủy (phiếu xuất bán đã hủy). Không thể đổi/trả hoặc bảo hành.";
            }

            items.Add(eligibility);
        }

        return new InvoiceEligibilityResponse
        {
            InvoiceId = invoice.InvoiceId,
            InvoiceCode = invoice.InvoiceCode,
            InvoiceDate = invoice.CreatedAt,
            Items = items
        };
    }

    public async Task<PageResultDto<DefectiveItemDto>> GetDefectiveItemsAsync(
        Guid branchId, int page, int pageSize, string? search, DateTime? fromDate, DateTime? toDate)
    {
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

        return new PageResultDto<DefectiveItemDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    #region Private Helpers

    private async Task<List<CategoryPolicyDto>?> GetCategoryPoliciesListAsync(Guid branchId)
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

    private async Task<decimal> GetReturnDiscountPercentAsync(Guid branchId)
    {
        var config = await _context.SystemConfigs
            .FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "RETURN_DISCOUNT_PERCENT");
        if (config != null && !string.IsNullOrWhiteSpace(config.ConfigValue)
            && decimal.TryParse(config.ConfigValue, System.Globalization.CultureInfo.InvariantCulture, out var p)
            && p > 0m)
            return p;
        return 0m;
    }

    private async Task<string> GenCodeAsync(string prefix)
    {
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var seq = await _context.ReturnOrders.CountAsync(r => r.CreatedAt >= today) + 1;
        return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{seq:D4}";
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

    private async Task ProcessRefundStockAsync(ReturnOrder ret, Guid userId)
    {
        var stockTicketReturn = new StockTicket
        {
            StockTicketId = Guid.NewGuid(),
            TicketCode = ret.ReturnCode,
            BranchId = ret.Order!.BranchId,
            UserId = userId,
            TicketType = "CUSTOMER_RETURN",
            Status = "COMPLETED",
            Notes = "Khach tra hang " + ret.ReturnCode,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        _context.StockTickets.Add(stockTicketReturn);

        foreach (var ri in ret.ReturnItems)
        {
            var bp = await _context.BranchProducts.Include(b => b.Product).FirstOrDefaultAsync(b => b.BranchProductId == ri.BranchProductId);
            if (bp == null) continue;

            var baseQty = ri.BaseQuantity > 0 ? ri.BaseQuantity : ri.Quantity;
            var isDefective = string.Equals(ri.Reason, "DEFECTIVE", StringComparison.OrdinalIgnoreCase);

            _context.StockTicketItems.Add(new StockTicketItem
            {
                TicketItemId = Guid.NewGuid(),
                StockTicketId = stockTicketReturn.StockTicketId,
                BranchProductId = ri.BranchProductId,
                SystemQuantity = bp.ActualStock,
                ActualQuantity = bp.ActualStock + baseQty,
                Discrepancy = baseQty,
                UnitPrice = bp.CostPrice,
                UnitName = bp.Product?.BaseUnit ?? ""
            });

            bp.ActualStock += baseQty;
            bp.AvailableStock += baseQty;

            if (isDefective)
            {
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
                    CreatedBy = userId
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
                    QtyChange = baseQty,
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

    private async Task ProcessExchangeStockAsync(ReturnOrder ret, Guid userId)
    {
        // Pre-check sellable stock
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
                throw new BusinessException("MSG-81",
                    $"Sản phẩm xuất đổi hiện không đủ tồn kho khả dụng để thực hiện giao dịch (Tồn khả dụng bán: {sellable}, cần: {pg.TotalBase})");
        }

        // 1 ticket per ReturnItem
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
                ReturnItemId = ri.ReturnItemId,
                BranchProductId = ri.BranchProductId,
                ProductId = bp.ProductId,
                Quantity = ri.Quantity,
                ConversionRate = ri.ConversionRate > 0 ? ri.ConversionRate : 1,
                BaseQuantity = baseQty,
                Status = "PENDING_ASSIGN",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = userId
            });
        }
    }

    private async Task<string?> ApplyExchangeDiffStockAsync(ReturnOrder ret, Guid userId)
    {
        // Pre-check SP B stock
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

        // SP A: return to stock (CUSTOMER_RETURN)
        var stockTicketReturn = new StockTicket
        {
            StockTicketId = Guid.NewGuid(),
            TicketCode = ret.ReturnCode,
            BranchId = ret.Order!.BranchId,
            UserId = userId,
            TicketType = "CUSTOMER_RETURN",
            Status = "COMPLETED",
            Notes = "Doi hang (Nhap tra) " + ret.ReturnCode,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        _context.StockTickets.Add(stockTicketReturn);

        foreach (var ri in ret.ReturnItems)
        {
            var bp = await _context.BranchProducts.Include(b => b.Product).FirstOrDefaultAsync(b => b.BranchProductId == ri.BranchProductId);
            if (bp == null) continue;
            var baseQty = ri.BaseQuantity > 0 ? ri.BaseQuantity : ri.Quantity;

            _context.StockTicketItems.Add(new StockTicketItem
            {
                TicketItemId = Guid.NewGuid(),
                StockTicketId = stockTicketReturn.StockTicketId,
                BranchProductId = ri.BranchProductId,
                SystemQuantity = bp.ActualStock,
                ActualQuantity = bp.ActualStock + baseQty,
                Discrepancy = baseQty,
                UnitPrice = bp.CostPrice,
                UnitName = bp.Product?.BaseUnit ?? ""
            });

            bp.AvailableStock += baseQty;
            bp.ActualStock += baseQty;
            _context.InventoryLedgers.Add(MkLedger(bp, "CUSTOMER_RETURN", ret.ReturnOrderId, ret.ReturnCode, baseQty));
        }

        // SP B: outward (SALE)
        var stockTicketSale = new StockTicket
        {
            StockTicketId = Guid.NewGuid(),
            TicketCode = ret.ReturnCode + "-OUT",
            BranchId = ret.Order!.BranchId,
            UserId = userId,
            TicketType = "SALE",
            Status = "COMPLETED",
            Notes = "Doi hang (Xuat giao) " + ret.ReturnCode,
            TotalAmount = (ret.DeltaAmount ?? 0m) > 0 ? (ret.DeltaAmount ?? 0m) : 0m,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        _context.StockTickets.Add(stockTicketSale);

        foreach (var ei in ret.ExchangeItems)
        {
            var bp = await _context.BranchProducts.Include(b => b.Product).FirstOrDefaultAsync(b => b.BranchProductId == ei.BranchProductId);
            if (bp == null) continue;
            var convRate = ei.ConversionRate > 0 ? ei.ConversionRate : 1m;
            var baseQty = ei.BaseQuantity > 0 ? ei.BaseQuantity : (ei.Quantity * convRate);
            var netBaseUnitPrice = Math.Round(ei.UnitPrice / convRate, 4);

            _context.StockTicketItems.Add(new StockTicketItem
            {
                TicketItemId = Guid.NewGuid(),
                StockTicketId = stockTicketSale.StockTicketId,
                BranchProductId = ei.BranchProductId,
                SystemQuantity = bp.ActualStock,
                ActualQuantity = bp.ActualStock - baseQty,
                Discrepancy = baseQty,
                UnitPrice = netBaseUnitPrice,
                UnitName = bp.Product?.BaseUnit ?? ""
            });

            bp.AvailableStock -= baseQty;
            bp.ActualStock -= baseQty;
            _context.InventoryLedgers.Add(MkLedger(bp, "EXCHANGE_OUT", ret.ReturnOrderId, ret.ReturnCode, -baseQty));
        }

        return null;
    }

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

    private async Task<(ReturnOrder ret, string? err)> LoadPendingExchangeDiffAsync(Guid id, Guid? branchId)
    {
        var query = _context.ReturnOrders
            .Include(r => r.Order).ThenInclude(o => o!.Invoice)
            .Include(r => r.ReturnItems).ThenInclude(ri => ri.BranchProduct).ThenInclude(bp => bp!.Product)
            .Include(r => r.ExchangeItems).ThenInclude(ei => ei.BranchProduct)
            .Where(r => r.ReturnOrderId == id);

        if (branchId.HasValue)
            query = query.Where(r => r.Order!.BranchId == branchId.Value);

        var ret = await query.FirstOrDefaultAsync();

        if (ret == null) return (ret!, "Không tìm thấy phiếu đổi trả.");
        if (ret.Status != "Pending") return (ret, "Phiếu đổi trả không ở trạng thái chờ xử lý.");
        if (ret.ReturnType != "EXCHANGE" || !ret.ExchangeItems.Any())
            return (ret, "Phiếu không phải đổi chênh lệch.");
        if ((ret.DeltaAmount ?? 0m) <= 0)
            return (ret, "Phiếu đổi ngang giá — không cần thanh toán chênh lệch.");
        return (ret!, null);
    }

    private async Task LogActionAsync(Guid userId, string action, string tableName,
        string entityType, Guid entityId, string description)
    {
        try
        {
            _context.SystemLogs.Add(new SystemLog
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                Action = action,
                TableName = tableName,
                EntityType = entityType,
                EntityId = entityId,
                Description = description,
                Level = "INFO",
                Source = "ReturnService",
                Timestamp = DateTime.UtcNow
            });
        }
        catch { /* Silent */ }
    }

    #endregion

    #region Exchange Preparation

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
        public string DeltaLabel { get; set; } = "Doi ngang gia";
        public List<StockAvailabilityDto> StockCheck { get; set; } = new();
        public List<PreparedReturnItem> Returns { get; set; } = new();
        public List<PreparedNewItem> NewItems { get; set; } = new();
        public Invoice? Invoice { get; set; }
    }

    private async Task<ExchangePreparation> PrepareExchangeAsync(
        Guid invoiceId, List<ExchangeQuoteItem> returnItems, List<ExchangeNewItem> newItems, Guid branchId)
    {
        var prep = new ExchangePreparation();

        var invoice = await _context.Invoices
            .Include(i => i.Order).ThenInclude(o => o!.Branch)
            .Include(i => i.Order).ThenInclude(o => o!.Customer)
            .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp!.Product).ThenInclude(p => p!.CategoryTaxonomy)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order!.BranchId == branchId);

        if (invoice == null)
        { prep.RestrictionReason = "Không tìm thấy hóa đơn."; prep.RuleBreak = "INVOICE"; return prep; }
        if (string.Equals(invoice.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        { prep.RestrictionReason = "Đơn bán này đã bị hủy (phiếu xuất bán đã hủy). Không thể đổi hàng cho đơn đã hủy."; prep.RuleBreak = "INVOICE"; return prep; }
        if (invoice.Status != "Completed")
        { prep.RestrictionReason = "Chỉ hóa đơn đã hoàn tất mới được đổi."; prep.RuleBreak = "INVOICE"; return prep; }
        prep.Invoice = invoice;

        var policies = await GetCategoryPoliciesListAsync(branchId);
        if (policies == null || policies.Count == 0)
        { prep.RestrictionReason = "Chưa có chính sách đổi/trả theo nhóm hàng."; prep.RuleBreak = "POLICY"; return prep; }

        var branch = invoice.Order?.Branch;
        var branchExchangeDays = branch?.ExchangeDaysAllowed ?? 30;
        var invoiceDate = invoice.CreatedAt ?? DateTime.UtcNow;

        // === QUY TẮC: Chiết khấu trả hàng (RETURN_DISCOUNT_PERCENT) KHÔNG áp dụng
        //    cho luồng ĐỔI LỆCH GIÁ. Đổi trả chỉ tính giá trị ngang/giá gốc, không trừ %.
        //    Chiết khấu này chỉ dùng cho luồng TRẢ HÀNG (REFUND) ở AddReturnItem.
        decimal oldValue = 0m;

        // SP A (returns)
        foreach (var ri in returnItems)
        {
            if (ri.Quantity <= 0)
            { prep.RestrictionReason = "Số lượng đổi phải lớn hơn 0."; prep.RuleBreak = "QTY"; return prep; }

            var ii = invoice.InvoiceItems.FirstOrDefault(x => x.InvoiceItemId == ri.InvoiceItemId);
            if (ii == null)
            { prep.RestrictionReason = $"Dòng hóa đơn không có trong hóa đơn gốc."; prep.RuleBreak = "INVOICE_ITEM"; return prep; }

            var product = ii.BranchProduct?.Product;
            var policy = MatchPolicy(policies, product);
            if (policy == null)
            { prep.RestrictionReason = $"Sản phẩm \"{product?.ProductName}\" chưa có chính sách đổi/trả."; prep.RuleBreak = "POLICY"; return prep; }

            var exDays = policy.ExchangeDays;
            if (!exDays.HasValue || exDays.Value <= 0)
            { prep.RestrictionReason = $"Nhóm hàng \"{policy.CategoryName}\" không được phép đổi."; prep.RuleBreak = "EXCHANGE_DAYS"; return prep; }

            var allowedDays = Math.Min(exDays.Value, branchExchangeDays);
            if (invoiceDate.AddDays(allowedDays) < DateTime.UtcNow)
            {
                var overDays = (int)(DateTime.UtcNow - invoiceDate.AddDays(allowedDays)).TotalDays;
                prep.RestrictionReason = $"Đã quá thời hạn đổi {overDays} ngày."; prep.RuleBreak = "EXCHANGE_DAYS"; return prep;
            }

            if (ri.Quantity + ii.ReturnedQuantity > ii.Quantity)
            {
                var maxQty = ii.Quantity - ii.ReturnedQuantity;
                prep.RestrictionReason = $"Đã đổi/trả đủ số lượng cho dòng \"{product?.ProductName}\". Tối đa còn {maxQty}."; prep.RuleBreak = "DUPLICATE"; return prep;
            }

            var conversionRate = (ii.ConvertValue.HasValue && ii.ConvertValue.Value > 0) ? ii.ConvertValue.Value : 1m;
            var baseQty = ri.Quantity * conversionRate;
            // Giá trị hàng cũ = giá gốc (KHÔNG trừ chiết khấu trả hàng).
            var refundLine = ri.Quantity * ii.SellPrice;
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

        // SP B (new items)
        decimal newValue = 0m;
        foreach (var ni in newItems)
        {
            if (ni.Quantity <= 0)
            { prep.RestrictionReason = "Số lượng hàng mới phải lớn hơn 0."; prep.RuleBreak = "QTY"; return prep; }

            var bp = await _context.BranchProducts
                .Include(b => b.Product)
                .FirstOrDefaultAsync(b => b.BranchProductId == ni.BranchProductId && b.BranchId == branchId);
            if (bp == null)
            { prep.RestrictionReason = "Không tìm thấy sản phẩm mới trong chi nhánh."; prep.RuleBreak = "NEW_ITEM"; return prep; }

            var conversionRate = (ni.ConversionRate.HasValue && ni.ConversionRate.Value > 0) ? ni.ConversionRate.Value : 1m;
            var baseQty = ni.Quantity * conversionRate;
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

        // Check SP B stock
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
        { prep.RestrictionReason = "Sản phẩm mới không đủ tồn kho khả dụng để xuất đổi."; prep.RuleBreak = "STOCK"; }

        prep.OldValue = oldValue;
        prep.NewValue = newValue;
        prep.Delta = newValue - oldValue;
        prep.DeltaLabel = prep.Delta > 0 ? "Thu thêm" : prep.Delta < 0 ? "Hoàn lại" : "Đổi ngang giá";

        if (prep.Delta < 0)
        {
            prep.RestrictionReason = "Chỉ được đổi sang sản phẩm cùng giá hoặc cao hơn. Không được đổi sang hàng rẻ hơn để hoàn tiền lại.";
            prep.RuleBreak = "DELTA_NEGATIVE";
        }

        prep.Eligible = prep.RestrictionReason == null;
        return prep;
    }

    #endregion
}
