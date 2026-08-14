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
            var suppliedQty = sti.ActualQuantity;
            var usedForWarranty = _context.WarrantyTickets
                .Where(wt => wt.ProductId == sti.BranchProduct!.ProductId
                    && wt.SupplierId == sti.StockTicket!.SupplierId
                    && (wt.Status == "AWAITING_SUPPLIER" || wt.Status == "COMPLETED"))
                .Sum(wt => wt.BaseQuantity > 0 ? wt.BaseQuantity : wt.Quantity);

            var isExpired = expiryDate.HasValue && expiryDate.Value < now;
            // Nếu hết hạn -> remaining = 0 (không thể dùng để BH nữa)
            var remaining = isExpired ? 0 : Math.Max(0, suppliedQty - usedForWarranty);
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
                TotalSuppliedQuantity = g.Sum(sti => sti.ActualQuantity),
                NearestExpiryDate = g.Min(sti => sti.WarrantyExpiryDate)
            })
            .OrderByDescending(s => s.LastPurchaseAt)
            .ToListAsync();

        // Tính số lượng đã gửi bảo hành cho từng NCC (AWAITING_SUPPLIER + COMPLETED = đã chiếm hạn mức BH)
        var warrantyBySupplier = await _context.WarrantyTickets
            .Where(wt => wt.ProductId == productId
                && wt.SupplierId != null
                && (wt.Status == "AWAITING_SUPPLIER" || wt.Status == "COMPLETED"))
            .GroupBy(wt => wt.SupplierId!.Value)
            .Select(g => new { SupplierId = g.Key, WarrantyQty = g.Sum(wt => wt.BaseQuantity > 0 ? wt.BaseQuantity : wt.Quantity) })
            .ToListAsync();

        var suppliers = supplierGroups.Select(g =>
        {
            var alreadySent = warrantyBySupplier
                .FirstOrDefault(w => w.SupplierId == g.SupplierId)?.WarrantyQty ?? 0;
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

        // Tăng WarrantyQuantity nếu chưa được tính (dùng BaseQuantity)
        bp!.WarrantyQuantity += baseQty;

        _context.WarrantyTickets.Add(ticket);
        await _context.SaveChangesAsync();

        return Ok(new { warrantyTicketId = ticket.WarrantyTicketId, status = ticket.Status });
    }

    // POST /api/warranty/{id}/assign-supplier
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
            var now = DateTime.UtcNow;

            // Quy tắc 1+2: chỉ tính các lô NHẬP CÓ BẢO HÀNH và CÒN HẠN (có thời hạn BH + ngày hết hạn + chưa hết hạn)
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
                .SumAsync(sti => sti.ActualQuantity);

            // Tính TotalAssigned = SUM(WarrantyTicket.BaseQuantity) đã gán cho NCC (AWAITING_SUPPLIER hoặc COMPLETED, trừ chính nó)
            var totalAssigned = await _context.WarrantyTickets
                .Where(wt => wt.ProductId == ticket.ProductId
                    && wt.SupplierId == req.SupplierId
                    && wt.WarrantyTicketId != ticket.WarrantyTicketId
                    && (wt.Status == "AWAITING_SUPPLIER" || wt.Status == "COMPLETED"))
                .SumAsync(wt => wt.BaseQuantity > 0 ? wt.BaseQuantity : wt.Quantity);

            var remainingQuota = Math.Max(0, totalSupplied - totalAssigned);

            if (baseQty > remainingQuota)
            {
                await transaction.RollbackAsync();
                return BadRequest(new
                {
                    message = $"Số lượng bảo hành ({baseQty}) vượt quá hạn mức còn lại của NCC {supplier.SupplierName} (Chỉ còn {remainingQuota}). Tổng đã nhập: {totalSupplied}, đã gửi: {totalAssigned}."
                });
            }

            ticket.SupplierId = req.SupplierId;
            ticket.Status = "AWAITING_SUPPLIER";
            ticket.AssignedAt = DateTime.UtcNow;
            ticket.AssignedBy = userId;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new { message = "Đã gửi bảo hành cho NCC.", status = ticket.Status, remainingQuota = remainingQuota - baseQty });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
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