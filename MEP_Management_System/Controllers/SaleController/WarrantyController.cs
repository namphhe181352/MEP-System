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

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/warranty")]
[Authorize]
[Tags("Quản lý bảo hành (Warranty)")]
public class WarrantyController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;
    private readonly IHubContext<PosHub> _posHub;

    public WarrantyController(Models.MEPDatabaseContext context, IHubContext<PosHub> posHub)
    {
        _context = context;
        _posHub = posHub;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found in token"));

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(userIdStr ?? throw new Exception("userId not found in token"));
    }

    // GET /api/warranty/{productId}/suggested-suppliers
    [HttpGet("{productId:guid}/suggested-suppliers")]
    public async Task<ActionResult<List<SuggestedSupplierDto>>> GetSuggestedSuppliers(Guid productId)
    {
        var branchId = GetBranchId();

        // Query InwardInventory (StockTicket) để tìm NCC từng cung cấp sản phẩm này
        var suppliers = await _context.StockTicketItems
            .Include(sti => sti.StockTicket)
            .Where(sti => sti.BranchProduct != null
                && sti.BranchProduct.ProductId == productId
                && sti.StockTicket!.TicketType == "PURCHASE"
                && sti.StockTicket!.Status == "COMPLETED"
                && sti.StockTicket!.BranchId == branchId
                && sti.StockTicket!.SupplierId != null)
            .GroupBy(sti => sti.StockTicket!.SupplierId!.Value)
            .Select(g => new SuggestedSupplierDto
            {
                SupplierId = g.Key,
                SupplierName = g.First().StockTicket!.Supplier!.SupplierName ?? "",
                ContactPhone = g.First().StockTicket!.Supplier!.ContactPhone,
                PurchaseCount = g.Select(sti => sti.StockTicketId).Distinct().Count(),
                LastPurchaseAt = g.Max(sti => sti.StockTicket!.CreatedAt)
            })
            .OrderByDescending(s => s.LastPurchaseAt)
            .ToListAsync();

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
                    LastPurchaseAt = null
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
        var ticket = new WarrantyTicket
        {
            WarrantyTicketId = Guid.NewGuid(),
            ReturnItemId = ri.ReturnItemId,
            BranchProductId = ri.BranchProductId,
            ProductId = bp!.ProductId,
            Quantity = ri.Quantity,
            Status = "PENDING_ASSIGN",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        // Tăng WarrantyQuantity nếu chưa được tính
        bp!.WarrantyQuantity += ri.Quantity;

        _context.WarrantyTickets.Add(ticket);
        await _context.SaveChangesAsync();

        return Ok(new { warrantyTicketId = ticket.WarrantyTicketId, status = ticket.Status });
    }

    // POST /api/warranty/{id}/assign-supplier
    [HttpPost("{id:guid}/assign-supplier")]
    public async Task<ActionResult> AssignSupplier(Guid id, [FromBody] AssignSupplierRequest req)
    {
        var userId = GetUserId();

        var ticket = await _context.WarrantyTickets
            .FirstOrDefaultAsync(wt => wt.WarrantyTicketId == id);

        if (ticket == null) return NotFound(new { message = "Không tìm thấy phiếu bảo hành." });
        if (ticket.Status != "PENDING_ASSIGN")
            return BadRequest(new { message = "Phiếu bảo hành không ở trạng thái chờ phân công." });

        var supplier = await _context.Suppliers.FindAsync(req.SupplierId);
        if (supplier == null) return NotFound(new { message = "Không tìm thấy nhà cung cấp." });

        ticket.SupplierId = req.SupplierId;
        ticket.Status = "AWAITING_SUPPLIER";
        ticket.AssignedAt = DateTime.UtcNow;
        ticket.AssignedBy = userId;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã gửi bảo hành cho NCC.", status = ticket.Status });
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

        // Giảm WarrantyQuantity, khôi phục SellableQuantity
        bp.WarrantyQuantity = Math.Max(0, bp.WarrantyQuantity - ticket.Quantity);

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