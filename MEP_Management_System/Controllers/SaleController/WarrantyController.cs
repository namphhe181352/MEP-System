using System;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.Hubs;
using MEP_Management_System.Services;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/warranty")]
[Authorize]
[Tags("Quản lý bảo hành (Warranty)")]
public class WarrantyController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;
    private readonly IHubContext<PosHub> _posHub;
    private readonly ICurrentBranchAccessor _currentBranch;

    public WarrantyController(Models.MEPDatabaseContext context, IHubContext<PosHub> posHub, ICurrentBranchAccessor currentBranch)
    {
        _context = context;
        _posHub = posHub;
        _currentBranch = currentBranch;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found in token"));

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(userIdStr ?? throw new Exception("userId not found in token"));
    }

    // GET /api/warranty/supplier-batches?supplierId=&search=&status=&branchId=
    [HttpGet("supplier-batches")]
    public async Task<ActionResult<List<SupplierWarrantyBatchDto>>> GetSupplierWarrantyBatches(
        [FromQuery] Guid? supplierId,
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] Guid? branchId)
    {
        var branchIdValue = branchId ?? _currentBranch?.GetBranchId();
        if (branchIdValue == null) return BadRequest(new { message = "Không xác định được chi nhánh." });

        var query = _context.StockTicketItems
            .Include(sti => sti.StockTicket).ThenInclude(st => st!.Supplier)
            .Include(sti => sti.BranchProduct).ThenInclude(bp => bp!.Product)
            .Where(sti => sti.StockTicket!.TicketType == "PURCHASE"
                && sti.StockTicket!.Status == "COMPLETED"
                && sti.StockTicket!.BranchId == branchIdValue.Value
                // Chỉ lấy lô có dữ liệu bảo hành
                && sti.WarrantyPeriod > 0
                && sti.WarrantyExpiryDate != null);

        if (supplierId.HasValue)
            query = query.Where(sti => sti.StockTicket!.SupplierId == supplierId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(sti =>
                (sti.BranchProduct!.Product.ProductName != null && sti.BranchProduct.Product.ProductName.ToLower().Contains(s)) ||
                (sti.BranchProduct.Product.ProductCode != null && sti.BranchProduct.Product.ProductCode.ToLower().Contains(s)) ||
                (sti.StockTicket!.TicketCode != null && sti.StockTicket.TicketCode.ToLower().Contains(s)));
        }

        var now = DateTime.UtcNow;
        var allItems = await query.ToListAsync();

        var result = allItems.Select(sti =>
        {
            var expiryDate = sti.WarrantyExpiryDate;
            var suppliedQty = sti.Discrepancy;                 // FIX L6: Discrepancy (SL nhập lô, ổn định)
            var usedForWarranty = sti.ClaimedWarrantyQty;      // FIX L3: per-lô (cột), không SUM cấp NCC

            var isExpired = expiryDate.HasValue && expiryDate.Value < now;
            // Nếu hết hạn -> remaining = 0 (không thể dùng để BH nữa)
            var remaining = ComputeRemainingWarranty(suppliedQty, usedForWarranty, expiryDate, now);
            var daysLeft = expiryDate.HasValue ? (int)(expiryDate.Value - now).TotalDays : (int?)null;
            string batchStatus;
            if (!expiryDate.HasValue || expiryDate.Value >= now.AddDays(30)) batchStatus = "ACTIVE";
            else if (expiryDate.Value >= now) batchStatus = "EXPIRING";
            else batchStatus = "EXPIRED";

            return new SupplierWarrantyBatchDto
            {
                TicketItemId = sti.TicketItemId,
                TicketCode = sti.StockTicket!.TicketCode ?? "",
                ProductId = sti.BranchProduct!.ProductId,
                ProductName = sti.BranchProduct.Product.ProductName ?? "",
                ProductCode = sti.BranchProduct.Product.ProductCode ?? "",
                ImportDate = sti.StockTicket.CreatedAt,
                WarrantyPeriod = sti.WarrantyPeriod,
                WarrantyUnit = sti.WarrantyUnit ?? "MONTH",
                WarrantyExpiryDate = expiryDate,
                SuppliedQuantity = suppliedQty,
                UsedForWarranty = usedForWarranty,
                RemainingWarranty = remaining,
                DaysUntilExpiry = daysLeft,
                BatchStatus = batchStatus,
                SupplierId = sti.StockTicket.SupplierId ?? Guid.Empty,
                SupplierName = sti.StockTicket.Supplier?.SupplierName ?? ""
            };
        }).ToList();

        // Filter by status
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            result = result.Where(r => r.BatchStatus == status.ToUpper()).ToList();
        }

        return Ok(result.OrderByDescending(r => r.ImportDate).ToList());
    }

    // GET /api/warranty/{productId}/suggested-suppliers
    [HttpGet("{productId:guid}/suggested-suppliers")]
    public async Task<ActionResult<List<SuggestedSupplierDto>>> GetSuggestedSuppliers(Guid productId)
    {
        var branchId = GetBranchId();

        // Query InwardInventory (StockTicket) để tìm NCC từng cung cấp sản phẩm này
        var supplierGroups = await _context.StockTicketItems
            .Include(sti => sti.StockTicket).ThenInclude(st => st!.Supplier)
            .Where(sti => sti.BranchProduct != null
                && sti.BranchProduct.ProductId == productId
                && sti.StockTicket!.TicketType == "PURCHASE"
                && sti.StockTicket!.Status == "COMPLETED"
                && sti.StockTicket!.BranchId == branchId
                && sti.StockTicket!.SupplierId != null
                // Quy tắc 1: chỉ tính lô NHẬP CÓ BẢO HÀNH và CÒN HẠN
                && sti.WarrantyPeriod > 0
                && sti.WarrantyExpiryDate != null
                && sti.WarrantyExpiryDate >= DateTime.UtcNow)
            .GroupBy(sti => sti.StockTicket!.SupplierId!.Value)
            .Select(g => new
            {
                SupplierId = g.Key,
                SupplierName = g.First().StockTicket!.Supplier!.SupplierName ?? "",
                ContactPhone = g.First().StockTicket!.Supplier!.ContactPhone,
                PurchaseCount = g.Select(sti => sti.StockTicketId).Distinct().Count(),
                LastPurchaseAt = g.Max(sti => sti.StockTicket!.CreatedAt),
                TotalSuppliedQuantity = g.Sum(sti => sti.Discrepancy),
                // FIX: dùng ClaimedWarrantyQty (cột atomic counter, nguồn sự thật) thay vì
                // SUM(BaseQuantity) của WarrantyTicket. Ticket legacy tạo trước hệ thống lot-quota
                // không có WarrantyLotAllocation -> SUM(ticket) over-count -> remaining bị 0 ảo.
                // Cùng bộ lọc lot hợp lệ với TotalSuppliedQuantity (đã GROUP ở đây) -> khớp GetAvailableLots.
                ClaimedQty = g.Sum(sti => sti.ClaimedWarrantyQty),
                NearestExpiryDate = g.Min(sti => sti.WarrantyExpiryDate)
            })
            .OrderByDescending(s => s.LastPurchaseAt)
            .ToListAsync();

        var suppliers = supplierGroups.Select(g =>
        {
            var alreadySent = g.ClaimedQty;
            return new SuggestedSupplierDto
            {
                SupplierId = g.SupplierId,
                SupplierName = g.SupplierName,
                ContactPhone = g.ContactPhone,
                PurchaseCount = g.PurchaseCount,
                LastPurchaseAt = g.LastPurchaseAt,
                TotalSuppliedQuantity = g.TotalSuppliedQuantity,
                RemainingWarrantyQuantity = Math.Max(0, g.TotalSuppliedQuantity - alreadySent),
                NearestExpiryDate = g.NearestExpiryDate
            };
        }).ToList();

        // Fallback: dùng Product.Supplier nếu không có lịch sử nhập kho
        if (!suppliers.Any())
        {
            var product = await _context.Products
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product?.Supplier != null)
            {
                suppliers.Add(new SuggestedSupplierDto
                {
                    SupplierId = product.Supplier.SupplierId,
                    SupplierName = product.Supplier.SupplierName ?? "",
                    ContactPhone = product.Supplier.ContactPhone,
                    PurchaseCount = 0,
                    LastPurchaseAt = null,
                    TotalSuppliedQuantity = 0,
                    RemainingWarrantyQuantity = 0
                });
            }
        }

        return Ok(suppliers);
    }

    // POST /api/warranty/init/{returnItemId} - Tạo WarrantyTicket cho đơn đổi trả cũ
    [HttpPost("init/{returnItemId:guid}")]
    public async Task<ActionResult<WarrantyTicketDto>> InitFromReturnItem(Guid returnItemId)
    {
        var userId = GetUserId();

        // Kiểm tra đã có WarrantyTicket chưa
        var existing = await _context.WarrantyTickets
            .FirstOrDefaultAsync(wt => wt.ReturnItemId == returnItemId);
        if (existing != null)
            return Ok(new { warrantyTicketId = existing.WarrantyTicketId, status = existing.Status });

        var ri = await _context.ReturnItems
            .Include(ri => ri.BranchProduct).ThenInclude(bp => bp.Product)
            .Include(ri => ri.ReturnOrder)
            .FirstOrDefaultAsync(ri => ri.ReturnItemId == returnItemId);

        if (ri == null) return NotFound(new { message = "Không tìm thấy ReturnItem." });

        // FIX L2: chỉ tạo ticket cho return hợp lệ (EXCHANGE hoặc REFUND lỗi).
        // Tránh tạo phiếu BH cho REFUND hàng bình thường (không lỗi) -> kẹt hold ảo.
        var ro = ri.ReturnOrder;
        var isEligible = ro != null
            && (string.Equals(ro.ReturnType, "EXCHANGE", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(ro.ReturnType, "REFUND", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(ri.Reason, "DEFECTIVE", StringComparison.OrdinalIgnoreCase)));
        if (!isEligible)
            return BadRequest(new { message = "ReturnItem này không thuộc luồng bảo hành (EXCHANGE hoặc REFUND lỗi)." });

        var bp = ri.BranchProduct;
        var baseQty = ri.BaseQuantity > 0 ? ri.BaseQuantity : ri.Quantity;
        var ticket = new WarrantyTicket
        {
            WarrantyTicketId = Guid.NewGuid(),
            ReturnItemId = ri.ReturnItemId,
            BranchProductId = ri.BranchProductId,
            ProductId = bp!.ProductId,
            Quantity = ri.Quantity,
            ConversionRate = ri.ConversionRate > 0 ? ri.ConversionRate : 1,
            BaseQuantity = baseQty,
            Status = "PENDING_ASSIGN",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        // FIX L2: KHÔNG tăng WarrantyQuantity ở đây — hold đã được FinalizeReturn thực hiện
        // khi finalize EXCHANGE/REFUND-lỗi. Tăng lại ở đây sẽ double-count hold.
        _context.WarrantyTickets.Add(ticket);
        await _context.SaveChangesAsync();

        return Ok(new { warrantyTicketId = ticket.WarrantyTicketId, status = ticket.Status });
    }

    // POST /api/warranty/{id}/assign-supplier
    // QUY TẮC 2 (FEFO): Nếu req.ManualLots rỗng → tự động phân bổ theo lô sắp hết hạn trước.
    [HttpPost("{id:guid}/assign-supplier")]
    public async Task<ActionResult> AssignSupplier(Guid id, [FromBody] AssignSupplierRequest req)
    {
        var userId = GetUserId();
        var branchId = GetBranchId();

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var ticket = await _context.WarrantyTickets
                .FirstOrDefaultAsync(wt => wt.WarrantyTicketId == id);

            if (ticket == null) return NotFound(new { message = "Không tìm thấy phiếu bảo hành." });
            if (ticket.Status != "PENDING_ASSIGN")
                return BadRequest(new { message = "Phiếu bảo hành không ở trạng thái chờ phân công." });

            var supplier = await _context.Suppliers.FindAsync(req.SupplierId);
            if (supplier == null) return NotFound(new { message = "Không tìm thấy nhà cung cấp." });

            var baseQty = ticket.BaseQuantity > 0 ? ticket.BaseQuantity : ticket.Quantity;
            // Tổng SL gửi bảo hành (theo spec FEFO). Mặc định = BaseQuantity của phiếu.
            var claimQty = (req.TotalClaimQty is > 0) ? req.TotalClaimQty.Value : baseQty;
            var now = DateTime.UtcNow;

            // Quy tắc 1+2: chỉ tính các lô NHẬP CÓ BẢO HÀNH và CÒN HẠN
            var totalSupplied = await _context.StockTicketItems
                .Include(sti => sti.StockTicket)
                .Where(sti => sti.BranchProduct != null
                    && sti.BranchProduct.ProductId == ticket.ProductId
                    && sti.StockTicket!.TicketType == "PURCHASE"
                    && sti.StockTicket!.Status == "COMPLETED"
                    && sti.StockTicket!.BranchId == branchId
                    && sti.StockTicket!.SupplierId == req.SupplierId
                    && sti.WarrantyPeriod > 0
                    && sti.WarrantyExpiryDate != null
                    && sti.WarrantyExpiryDate >= now)
                .SumAsync(sti => sti.Discrepancy);   // FIX L6: Discrepancy

            // TotalClaimed = tổng đang HOLD (AWAITING_SUPPLIER) cho NCC+SP này, đọc từ
            // claimed_warranty_qty (mô hình HOLD ĐỒNG THỜI — COMPLETED đã trả lại hạn mức lô).
            // Đồng bộ nguồn sự thật với GetSuggestedSuppliers / GetAvailableLots (cùng bộ lọc lot),
            // thay vì SUM(WarrantyTicket.BaseQuantity) cũ — cái đó over-count khi BH một phần
            // (ticket ghi BaseQuantity=20 nhưng chỉ claim 10 vào lô).
            var totalClaimed = await _context.StockTicketItems
                .Include(sti => sti.StockTicket)
                .Where(sti => sti.BranchProduct != null
                    && sti.BranchProduct.ProductId == ticket.ProductId
                    && sti.StockTicket!.TicketType == "PURCHASE"
                    && sti.StockTicket!.Status == "COMPLETED"
                    && sti.StockTicket!.BranchId == branchId
                    && sti.StockTicket!.SupplierId == req.SupplierId
                    && sti.WarrantyPeriod > 0
                    && sti.WarrantyExpiryDate != null
                    && sti.WarrantyExpiryDate >= now)
                .SumAsync(sti => sti.ClaimedWarrantyQty);

            var remainingQuota = Math.Max(0, totalSupplied - totalClaimed);

            // BH MỘT PHẦN: nếu claimQty vượt hạn mức NCC còn lại, chỉ gửi tối đa remainingQuota.
            // Phần dư (lot nhập thiếu/quá hạn/không đủ điều kiện BH) sẽ KHÔNG được BH — người dùng
            // chấp nhận chia lô (vd. trả 20 cái nhưng NCC chỉ đủ BH 10 -> BH 10/20). Chỉ block khi
            // không còn chút hạn mức nào.
            var actualClaimQty = Math.Min(claimQty, remainingQuota);

            if (actualClaimQty <= 0)
            {
                await transaction.RollbackAsync();
                return BadRequest(new
                {
                    message = $"Nhà cung cấp {supplier.SupplierName} đã hết hạn mức bảo hành cho sản phẩm này (tổng nhập có BH: {totalSupplied}, đang hold: {totalClaimed})."
                });
            }

            // ===== QUY TẮC 2: Phân bổ theo lô (FEFO nếu không tự chọn lô) =====
            List<LotAllocationItemDto> allocations;
            try
            {
                allocations = await AllocateWarrantyLotsAsync(ticket, req.SupplierId, branchId, actualClaimQty, req.ManualLots, userId);
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }

            ticket.SupplierId = req.SupplierId;
            ticket.Status = "AWAITING_SUPPLIER";
            ticket.AssignedAt = DateTime.UtcNow;
            ticket.AssignedBy = userId;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var allocatedQty = allocations.Sum(a => a.AllocateQty);
            var isPartial = allocatedQty < claimQty;
            return Ok(new
            {
                message = isPartial
                    ? $"Đã gửi bảo hành MỘT PHẦN cho NCC: {allocatedQty}/{claimQty} (theo ĐVT cơ bản). Phần dư {claimQty - allocatedQty} không có lô đủ điều kiện bảo hành."
                    : "Đã gửi bảo hành cho NCC.",
                status = ticket.Status,
                remainingQuota = Math.Max(0, remainingQuota - allocatedQty),
                claimQty,
                allocatedQty,
                isPartial,
                uncoveredQty = claimQty - allocatedQty,
                allocationMode = (req.ManualLots != null && req.ManualLots.Count > 0) ? "MANUAL" : "FEFO",
                allocations
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // QUY TẮC 1: GET /api/warranty/available-lots
    // Tự động LỌC BỎ (trong WHERE LINQ):
    //   - Lô có WarrantyExpiryDate < now (quá hạn BH)
    //   - Lô có SL_Con_BH_NCC == 0 (hết suất BH)
    // Chỉ trả về lô CÒN HẠN & CÒN SUẤT, sắp xếp FEFO (expiry ASC).
    // ============================================================
    [HttpGet("available-lots")]
    public async Task<ActionResult<List<AvailableWarrantyLotDto>>> GetAvailableWarrantyLots(
        [FromQuery] Guid productId,
        [FromQuery] Guid? supplierId,
        [FromQuery] Guid? branchId)
    {
        if (productId == Guid.Empty)
            return BadRequest(new { message = "productId là bắt buộc." });

        var branchIdValue = branchId ?? _currentBranch?.GetBranchId();
        if (branchIdValue == null) return BadRequest(new { message = "Không xác định được chi nhánh." });

        var now = DateTime.UtcNow;
        var lots = await GetAvailableLotsInternalAsync(productId, supplierId, branchIdValue.Value, now);
        return Ok(lots);
    }

    // ============================================================
    // Helper: công thức SL_Con_BH_NCC (tập trung, L12).
    // SL_Con_BH = (now > Expiry) ? 0 : Max(0, Supplied - Claimed)
    // ============================================================
    private static decimal ComputeRemainingWarranty(decimal suppliedQty, decimal claimedQty, DateTime? expiry, DateTime now)
        => (expiry.HasValue && now > expiry.Value)
            ? 0m
            : Math.Max(0, suppliedQty - claimedQty);

    // ============================================================
    // Helper: lấy danh sách lô còn đủ điều kiện BH (Quy tắc 1) theo thứ tự FEFO.
    // FIX L6: supplied = Discrepancy (SL nhập lô, ổn định) — không phải ActualQuantity.
    // FIX L7/L3: claimed đọc trực tiếp cột ClaimedWarrantyQty (fast, không SUM aggregate).
    // ============================================================
    private async Task<List<AvailableWarrantyLotDto>> GetAvailableLotsInternalAsync(
        Guid productId, Guid? supplierId, Guid branchId, DateTime now)
    {
        var lotQuery = _context.StockTicketItems
            .Include(sti => sti.StockTicket).ThenInclude(st => st!.Supplier)
            .Include(sti => sti.BranchProduct).ThenInclude(bp => bp!.Product)
            .Where(sti => sti.BranchProduct != null
                && sti.BranchProduct.ProductId == productId
                && sti.StockTicket!.TicketType == "PURCHASE"
                && sti.StockTicket!.Status == "COMPLETED"
                && sti.StockTicket!.BranchId == branchId
                && sti.WarrantyPeriod > 0
                && sti.WarrantyExpiryDate != null
                && sti.WarrantyExpiryDate >= now); // QUY TẮC 1 (WHERE): lọc lô hết hạn

        if (supplierId.HasValue)
            lotQuery = lotQuery.Where(sti => sti.StockTicket!.SupplierId == supplierId.Value);

        var lots = await lotQuery.ToListAsync();
        if (!lots.Any()) return new List<AvailableWarrantyLotDto>();

        var result = lots.Select(sti =>
        {
            var supplied = sti.Discrepancy;                 // FIX L6: Discrepancy
            var claimed = sti.ClaimedWarrantyQty;           // FIX L3/L7: đọc cột
            var remaining = ComputeRemainingWarranty(supplied, claimed, sti.WarrantyExpiryDate, now);
            return new AvailableWarrantyLotDto
            {
                TicketItemId = sti.TicketItemId,
                TicketCode = sti.StockTicket!.TicketCode ?? "",
                ProductId = sti.BranchProduct!.ProductId,
                ProductName = sti.BranchProduct.Product!.ProductName ?? "",
                ProductCode = sti.BranchProduct.Product.ProductCode ?? "",
                ImportDate = sti.StockTicket.CreatedAt,
                WarrantyPeriod = sti.WarrantyPeriod,
                WarrantyUnit = sti.WarrantyUnit ?? "MONTH",
                WarrantyExpiryDate = sti.WarrantyExpiryDate,
                SuppliedQuantity = supplied,
                ClaimedWarrantyQty = claimed,
                RemainingWarranty = remaining,
                DaysUntilExpiry = sti.WarrantyExpiryDate.HasValue
                    ? (int)(sti.WarrantyExpiryDate.Value - now).TotalDays : (int?)null,
                SupplierId = sti.StockTicket.SupplierId ?? Guid.Empty,
                SupplierName = sti.StockTicket.Supplier?.SupplierName ?? ""
            };
        })
        .Where(dto => dto.RemainingWarranty > 0)   // QUY TẮC 1: lọc lô hết suất
        .OrderBy(dto => dto.WarrantyExpiryDate)    // QUY TẮC 2 (FEFO): hết hạn sớm nhất đứng đầu
        .ToList();

        return result;
    }

    // ============================================================
    // Helper: Phân bổ số lượng gửi BH theo lô — ATOMIC (FIX L7).
    //   - ManualLots rỗng/null → FEFO tự động (sắp hết hạn trước).
    //   - Có ManualLots → validate & dùng lựa chọn người dùng.
    // Atomic conditional UPDATE: SET claimed += take WHERE claimed+take <= discrepancy
    //   AND expiry >= now → affected==1 mới OK; 0 → throw (chống concurrent over-allocate).
    // ============================================================
    private async Task<List<LotAllocationItemDto>> AllocateWarrantyLotsAsync(
        WarrantyTicket ticket, Guid supplierId, Guid branchId, decimal claimQty,
        List<ManualLotAllocationInput>? manualLots, Guid userId)
    {
        var now = DateTime.UtcNow;
        var allocations = new List<LotAllocationItemDto>();

        // Lô khả dụng (FEFO order, đã lọc hết hạn/hết suất)
        var available = await GetAvailableLotsInternalAsync(ticket.ProductId, supplierId, branchId, now);
        var availDict = available.ToDictionary(l => l.TicketItemId);

        // Xây danh sách (lot, take) cần phân bổ theo 2 mode
        var plan = new List<(AvailableWarrantyLotDto Lot, decimal Take)>();
        if (manualLots != null && manualLots.Count > 0)
        {
            // MANUAL
            foreach (var m in manualLots)
            {
                if (m.AllocateQty <= 0)
                    throw new InvalidOperationException(
                        $"Số lượng phân bổ cho lô {m.TicketItemId} phải lớn hơn 0.");
                if (!availDict.TryGetValue(m.TicketItemId, out var lot))
                {
                    // Phân biệt: hết hạn vs không thuộc NCC/SP
                    var raw = await _context.StockTicketItems.AsNoTracking()
                        .FirstOrDefaultAsync(sti => sti.TicketItemId == m.TicketItemId);
                    if (raw == null)
                        throw new InvalidOperationException($"Lô {m.TicketItemId} không tồn tại.");
                    if (raw.WarrantyExpiryDate.HasValue && raw.WarrantyExpiryDate.Value < now)
                        throw new InvalidOperationException(
                            "Lô hàng này đã hết hạn bảo hành của Nhà cung cấp.");
                    throw new InvalidOperationException(
                        $"Lô {m.TicketItemId} không thuộc nhà cung cấp/sản phẩm này hoặc đã hết suất bảo hành.");
                }
                if (m.AllocateQty > lot.RemainingWarranty)
                    throw new InvalidOperationException(
                        "Số lượng gửi bảo hành vượt quá hạn mức còn lại của Lô nhập kho này!");
                plan.Add((lot, m.AllocateQty));
            }
            var totalAllocated = plan.Sum(p => p.Take);
            if (totalAllocated != claimQty)
                throw new InvalidOperationException(
                    $"Tổng số lượng phân bổ theo lô ({totalAllocated}) không khớp tổng số lượng gửi bảo hành ({claimQty}).");
        }
        else
        {
            // FEFO: lấy theo thứ tự đến khi đủ claimQty
            var remaining = claimQty;
            foreach (var lot in available)
            {
                if (remaining <= 0) break;
                var take = Math.Min(remaining, lot.RemainingWarranty);
                plan.Add((lot, take));
                remaining -= take;
            }
            // BH MỘT PHẦN: KHÔNG throw khi còn dư sau khi duyệt hết lô FEFO — phân bổ tới đâu
            // hết tới đó. (actualClaimQty đã được kẹp theo remainingQuota ở AssignSupplier; nếu
            // vẫn dư là do lot vừa hết hạn/giữa chừng -> trả về partial, response đánh dấu isPartial.)
            // Guard thực sự nằm ở atomic UPDATE từng lô dưới đây (chống concurrent over-allocate).
        }

        // Thực hiện phân bổ — atomic conditional UPDATE từng lô + INSERT audit row
        foreach (var (lot, take) in plan)
        {
            var affected = await _context.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE stock_ticket_items
                SET claimed_warranty_qty = claimed_warranty_qty + {take}
                WHERE ticket_item_id = {lot.TicketItemId}
                  AND warranty_expiry_date >= {now}
                  AND claimed_warranty_qty + {take} <= discrepancy");

            if (affected != 1)
                throw new InvalidOperationException(
                    "Số lượng gửi bảo hành vượt quá hạn mức còn lại của Lô nhập kho này!");

            var alloc = new WarrantyLotAllocation
            {
                AllocationId = Guid.NewGuid(),
                WarrantyTicketId = ticket.WarrantyTicketId,
                TicketItemId = lot.TicketItemId,
                AllocateQty = take,
                CreatedAt = now,
                CreatedBy = userId
            };
            _context.WarrantyLotAllocations.Add(alloc);

            allocations.Add(new LotAllocationItemDto
            {
                AllocationId = alloc.AllocationId,
                TicketItemId = lot.TicketItemId,
                TicketCode = lot.TicketCode,
                WarrantyExpiryDate = lot.WarrantyExpiryDate,
                AvailableBefore = lot.RemainingWarranty,
                AllocateQty = take,
                RemainingAfter = lot.RemainingWarranty - take
            });
        }

        return allocations;
    }

    // POST /api/warranty/{id}/accept
    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult> AcceptWarrantyReturn(Guid id)
    {
        var userId = GetUserId();

        var ticket = await _context.WarrantyTickets
            .FirstOrDefaultAsync(wt => wt.WarrantyTicketId == id);

        if (ticket == null) return NotFound(new { message = "Không tìm thấy phiếu bảo hành." });
        if (ticket.Status != "AWAITING_SUPPLIER")
            return BadRequest(new { message = "Phiếu bảo hành không ở trạng thái chờ hàng về." });

        var bp = await _context.BranchProducts.FindAsync(ticket.BranchProductId);
        if (bp == null) return NotFound(new { message = "Không tìm thấy thông tin tồn kho." });

        // Giảm WarrantyQuantity, khôi phục SellableQuantity (dùng BaseQuantity)
        var baseQty = ticket.BaseQuantity > 0 ? ticket.BaseQuantity : ticket.Quantity;
        bp.WarrantyQuantity = Math.Max(0, bp.WarrantyQuantity - baseQty);

        ticket.Status = "COMPLETED";
        ticket.CompletedAt = DateTime.UtcNow;
        ticket.CompletedBy = userId;

        // Giải phóng hạn mức lô (FEFO): NCC đã trả hàng bảo hành về -> giảm claimed_warranty_qty
        // của các lô đã phân bổ cho phiếu này. claimed_warranty_qty theo mô hình HOLD ĐỒNG THỜI
        // (chỉ tính số đang ở NCC = AWAITING_SUPPLIER), nên khi COMPLETED phải trả lại hạn mức
        // cho lô -> lô có thể nhận bảo hành tiếp trong thời hạn BH (nếu chưa hết hạn).
        var lotAllocs = await _context.WarrantyLotAllocations
            .Where(a => a.WarrantyTicketId == id)
            .ToListAsync();
        foreach (var a in lotAllocs)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE stock_ticket_items
                SET claimed_warranty_qty = GREATEST(0, claimed_warranty_qty - {a.AllocateQty})
                WHERE ticket_item_id = {a.TicketItemId}");
        }

        // Ghi InventoryLedger
        _context.InventoryLedgers.Add(new InventoryLedger
        {
            LedgerId = Guid.NewGuid(),
            BranchId = bp.BranchId,
            BranchProductId = bp.BranchProductId,
            DocType = "WARRANTY_ACP",
            DocId = ticket.WarrantyTicketId,
            DocCode = $"WAR-ACP-{ticket.WarrantyTicketId.ToString()[..8]}",
            TransactionTime = DateTime.UtcNow,
            QtyChange = 0, // Không thay đổi tồn thực tế, chỉ giải phóng WarrantyQuantity
            UnitCostPrice = Math.Round(bp.CostPrice, 4),
            TotalCostValue = 0,
            StockAfter = bp.ActualStock,
            AvgCostAfter = Math.Round(bp.CostPrice, 4),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        // SignalR: thông báo POS cập nhật tồn khả dụng
        try
        {
            await _posHub.Clients.Group($"branch_{bp.BranchId}")
                .SendAsync("StockUpdated", new
                {
                    ProductId = ticket.ProductId,
                    BranchId = bp.BranchId,
                    NewSellableQuantity = bp.AvailableStock - bp.WarrantyQuantity,
                    WarrantyQuantity = bp.WarrantyQuantity
                });
        }
        catch { /* SignalR fail silently */ }

        return Ok(new { message = "Đã xác nhận hàng bảo hành về kho.", status = ticket.Status });
    }
}