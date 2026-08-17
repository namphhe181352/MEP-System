using MEP_Management_System.DTOs.SaleDTOs;
using Microsoft.AspNetCore.SignalR;
using MEP_Management_System.Hubs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

public interface IWarrantyService
{
    Task<List<SupplierWarrantyBatchDto>> GetSupplierWarrantyBatchesAsync(Guid? supplierId, string? search, string? status, Guid? branchId);
    Task<List<SuggestedSupplierDto>> GetSuggestedSuppliersAsync(Guid productId, Guid branchId);
    Task<WarrantyClaimResultDto> CreateClaimAsync(Guid productId, Guid ticketItemId, CreateWarrantyClaimRequest request, Guid userId, Guid branchId);
    Task<List<WarrantyClaimDto>> GetClaimsAsync(Guid? productId, string? status, Guid branchId);
    Task<WarrantyClaimDto?> GetClaimByIdAsync(Guid claimId);
    Task<bool> UpdateClaimStatusAsync(Guid claimId, UpdateWarrantyClaimRequest request, Guid userId);
}

public class WarrantyService : IWarrantyService
{
    private readonly MEPDatabaseContext _context;
    private readonly IHubContext<PosHub> _posHub;

    public WarrantyService(MEPDatabaseContext context, IHubContext<PosHub> posHub)
    {
        _context = context;
        _posHub = posHub;
    }

    private static decimal ComputeRemainingWarranty(decimal suppliedQty, decimal usedForWarranty, DateTime? expiryDate, DateTime now)
    {
        if (expiryDate.HasValue && expiryDate.Value < now) return 0;
        return Math.Max(0, suppliedQty - usedForWarranty);
    }

    public async Task<List<SupplierWarrantyBatchDto>> GetSupplierWarrantyBatchesAsync(Guid? supplierId, string? search, string? status, Guid? branchId)
    {
        var query = _context.StockTicketItems
            .Include(sti => sti.StockTicket).ThenInclude(st => st!.Supplier)
            .Include(sti => sti.BranchProduct).ThenInclude(bp => bp!.Product)
            .Where(sti => sti.StockTicket!.TicketType == "PURCHASE"
                && sti.StockTicket!.Status == "COMPLETED"
                && sti.StockTicket!.BranchId == branchId.Value
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
            var suppliedQty = sti.Discrepancy;
            var usedForWarranty = sti.ClaimedWarrantyQty;
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

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
            result = result.Where(r => r.BatchStatus == status.ToUpper()).ToList();

        return result.OrderByDescending(r => r.ImportDate).ToList();
    }

    public async Task<List<SuggestedSupplierDto>> GetSuggestedSuppliersAsync(Guid productId, Guid branchId)
    {
        var supplierGroups = await _context.StockTicketItems
            .Include(sti => sti.StockTicket).ThenInclude(st => st!.Supplier)
            .Where(sti => sti.BranchProduct != null
                && sti.BranchProduct.ProductId == productId
                && sti.StockTicket!.TicketType == "PURCHASE"
                && sti.StockTicket!.Status == "COMPLETED"
                && sti.StockTicket!.BranchId == branchId
                && sti.StockTicket!.SupplierId != null
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
                TotalSuppliedQuantity = g.Sum(sti => (decimal?)sti.Discrepancy) ?? 0,
                RemainingWarrantyQuantity = g.Count(sti => sti.WarrantyExpiryDate >= DateTime.UtcNow)
            })
            .OrderByDescending(x => x.PurchaseCount)
            .Take(5)
            .ToListAsync();

        return supplierGroups.Select(g => new SuggestedSupplierDto
        {
            SupplierId = g.SupplierId,
            SupplierName = g.SupplierName,
            ContactPhone = g.ContactPhone,
            PurchaseCount = g.PurchaseCount,
            LastPurchaseAt = g.LastPurchaseAt,
            TotalSuppliedQuantity = g.TotalSuppliedQuantity,
            RemainingWarrantyQuantity = g.RemainingWarrantyQuantity
        }).ToList();
    }

    public async Task<WarrantyClaimResultDto> CreateClaimAsync(Guid productId, Guid ticketItemId, CreateWarrantyClaimRequest request, Guid userId, Guid branchId)
    {
        var ticketItem = await _context.StockTicketItems
            .Include(sti => sti.StockTicket)
            .Include(sti => sti.BranchProduct).ThenInclude(bp => bp!.Product)
            .FirstOrDefaultAsync(sti => sti.TicketItemId == ticketItemId && sti.BranchProduct != null && sti.BranchProduct.ProductId == productId)
            ?? throw new BusinessException("Không tìm thấy lô hàng.");

        if (ticketItem.WarrantyPeriod <= 0 || ticketItem.WarrantyExpiryDate == null)
            throw new BusinessException("Sản phẩm không có bảo hành.");

        var now = DateTime.UtcNow;
        if (ticketItem.WarrantyExpiryDate < now)
            throw new BusinessException("Bảo hành đã hết hạn.");

        var remainingWarranty = ComputeRemainingWarranty(ticketItem.Discrepancy, ticketItem.ClaimedWarrantyQty, ticketItem.WarrantyExpiryDate, now);
        if (remainingWarranty <= 0)
            throw new BusinessException("Đã hết quota bảo hành cho lô này.");

        var totalQty = ticketItem.ClaimedWarrantyQty + request.Quantity;
        if (totalQty > ticketItem.Discrepancy)
            throw new BusinessException($"Số lượng yêu cầu ({totalQty}) vượt quá số lượng đã nhập ({ticketItem.Discrepancy}).");

        var claim = new WarrantyClaim
        {
            ClaimId = Guid.NewGuid(),
            TicketItemId = ticketItemId,
            ProductId = productId,
            ClaimCode = $"WC{DateTime.UtcNow:yyyyMMdd}{new Random().Next(1000, 9999)}",
            ClaimDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            ClaimedQuantity = request.Quantity,
            ClaimType = request.ClaimType,
            IssueDescription = request.IssueDescription,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            ReplacementProductId = request.ReplacementProductId,
            SupplierId = ticketItem.StockTicket?.SupplierId,
            Status = "PENDING",
            BranchId = branchId,
            ClaimedByUserId = userId,
            CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        _context.WarrantyClaims.Add(claim);
        ticketItem.ClaimedWarrantyQty += request.Quantity;
        await _context.SaveChangesAsync();

        await _posHub.Clients.Group($"branch_{branchId}").SendAsync("WarrantyClaimCreated", new { claim.ClaimId, claim.ClaimCode });

        return new WarrantyClaimResultDto
        {
            ClaimId = claim.ClaimId,
            ClaimCode = claim.ClaimCode,
            Status = claim.Status,
            RemainingWarranty = ComputeRemainingWarranty(ticketItem.Discrepancy, ticketItem.ClaimedWarrantyQty, ticketItem.WarrantyExpiryDate, now)
        };
    }

    public async Task<List<WarrantyClaimDto>> GetClaimsAsync(Guid? productId, string? status, Guid branchId)
    {
        var query = _context.WarrantyClaims
            .Include(wc => wc.TicketItem).ThenInclude(ti => ti!.BranchProduct).ThenInclude(bp => bp!.Product)
            .Include(wc => wc.TicketItem).ThenInclude(ti => ti!.StockTicket).ThenInclude(st => st!.Supplier)
            .Include(wc => wc.ClaimedByUser)
            .Include(wc => wc.ProcessedByUser)
            .Where(wc => wc.BranchId == branchId);

        if (productId.HasValue) query = query.Where(wc => wc.ProductId == productId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(wc => wc.Status == status.ToUpper());

        var claims = await query.OrderByDescending(wc => wc.CreatedAt).ToListAsync();
        return claims.Select(MapToDto).ToList();
    }

    public async Task<WarrantyClaimDto?> GetClaimByIdAsync(Guid claimId)
    {
        var claim = await _context.WarrantyClaims
            .Include(wc => wc.TicketItem).ThenInclude(ti => ti!.BranchProduct).ThenInclude(bp => bp!.Product)
            .Include(wc => wc.TicketItem).ThenInclude(ti => ti!.StockTicket).ThenInclude(st => st!.Supplier)
            .Include(wc => wc.ClaimedByUser)
            .Include(wc => wc.ProcessedByUser)
            .FirstOrDefaultAsync(wc => wc.ClaimId == claimId);
        return claim == null ? null : MapToDto(claim);
    }

    public async Task<bool> UpdateClaimStatusAsync(Guid claimId, UpdateWarrantyClaimRequest request, Guid userId)
    {
        var claim = await _context.WarrantyClaims.FindAsync(claimId)
            ?? throw new KeyNotFoundException("Không tìm thấy phiếu bảo hành.");

        claim.Status = request.Status.ToUpper();
        claim.ProcessedByUserId = userId;
        claim.ProcessedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        claim.ProcessNotes = request.ProcessNotes;

        if (request.Status == "REJECTED" && claim.ClaimedQuantity > 0)
        {
            var ticketItem = await _context.StockTicketItems.FindAsync(claim.TicketItemId);
            if (ticketItem != null) ticketItem.ClaimedWarrantyQty -= claim.ClaimedQuantity;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    private static WarrantyClaimDto MapToDto(WarrantyClaim claim)
    {
        return new WarrantyClaimDto
        {
            ClaimId = claim.ClaimId,
            ClaimCode = claim.ClaimCode,
            ClaimDate = claim.ClaimDate,
            ClaimedQuantity = claim.ClaimedQuantity,
            ClaimType = claim.ClaimType,
            IssueDescription = claim.IssueDescription,
            CustomerName = claim.CustomerName,
            CustomerPhone = claim.CustomerPhone,
            Status = claim.Status,
            ProcessNotes = claim.ProcessNotes,
            ProductId = claim.ProductId,
            ProductName = claim.TicketItem?.BranchProduct?.Product?.ProductName,
            TicketCode = claim.TicketItem?.StockTicket?.TicketCode,
            SupplierName = claim.TicketItem?.StockTicket?.Supplier?.SupplierName,
            ClaimedByUserName = claim.ClaimedByUser?.FullName,
            ProcessedByUserName = claim.ProcessedByUser?.FullName,
            ProcessedAt = claim.ProcessedAt,
            CreatedAt = claim.CreatedAt
        };
    }
}
