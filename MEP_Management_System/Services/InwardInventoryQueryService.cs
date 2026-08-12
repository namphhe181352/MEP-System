using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services
{
    public class InwardInventoryQueryService
    {
        private readonly MEPDatabaseContext _context;
        private readonly ICurrentBranchAccessor _currentBranch;

        public InwardInventoryQueryService(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch)
        {
            _context = context;
            _currentBranch = currentBranch;
        }

        private bool IsOwner(ClaimsPrincipal user) => user?.IsInRole("Owner") ?? false;

        private Guid? GetUserBranchFromTokenOrDefault(ClaimsPrincipal user)
        {
            return _currentBranch.GetBranchId();
        }

        public async Task<List<InwardInventoryResponseDto>> GetInwardInventoryAsync(string? status, ClaimsPrincipal currentUser)
        {
            var query = _context.StockTickets
                .Include(st => st.User)
                .Include(st => st.StockTicketItems)
                    .ThenInclude(item => item.BranchProduct)
                        .ThenInclude(bp => bp.Product)
                .Where(st => st.TicketType == "PURCHASE" || st.TicketType == "CUSTOMER_RETURN" || st.TicketType == "BALANCE_ADJUST")
                .AsQueryable();

            // Lọc theo branch từ token - áp dụng cho TẤT CẢ user (bao gồm Owner)
            var branchId = GetUserBranchFromTokenOrDefault(currentUser);
            if (branchId == null) return new List<InwardInventoryResponseDto>();
            query = query.Where(st => st.BranchId == branchId.Value);

            if (!string.IsNullOrWhiteSpace(status))
            {
                var normalizedStatus = status.Trim().ToUpperInvariant();
                if (normalizedStatus == "ALL")
                {
                    // show all
                }
                else if (normalizedStatus == "CANCELLED")
                {
                    query = query.Where(st => st.Status != null && st.Status.ToUpper() == "CANCELLED");
                }
                else
                {
                    query = query.Where(st => st.Status != null && st.Status.ToUpper() == normalizedStatus);
                }
            }
            else
            {
                query = query.Where(st => st.Status == null || (st.Status != "CANCELLED"));
            }

            query = query.OrderByDescending(st => st.CreatedAt);

            var items = await query.ToListAsync();

            // Map to DTO
            return items.Select(MapToInwardInventoryResponse).ToList();
        }

        private InwardInventoryResponseDto MapToInwardInventoryResponse(StockTicket st)
        {
            return new InwardInventoryResponseDto
            {
                StockTicketId = st.StockTicketId,
                TicketCode = st.TicketCode ?? string.Empty,
                TicketType = st.TicketType ?? string.Empty,
                Status = st.Status ?? string.Empty,
                BranchId = st.BranchId,
                UserId = st.UserId,
                SupplierId = st.SupplierId,
                UserName = st.User?.FullName ?? st.User?.Username,
                Reason = st.Notes,
                Note = st.Notes,
                CreatedAt = st.CreatedAt,
                CompletedAt = st.CompletedAt,
                CancelledAt = st.CancelledAt,
                ConfirmedBy = st.ConfirmedBy,
                CancelledBy = st.CancelledBy,
                CancelReason = st.CancelReason,
                CostPrice = st.TicketType == "CUSTOMER_RETURN"
                    ? 0
                    : st.StockTicketItems?.Sum(i => i.UnitPrice ?? 0) ?? 0,
                Items = st.StockTicketItems?.Select(i => new InwardInventoryItemResponseDto
                {
                    TicketItemId = i.TicketItemId,
                    BranchProductId = i.BranchProductId,
                    ProductId = i.BranchProduct?.ProductId ?? Guid.Empty,
                    ProductCode = i.BranchProduct?.Product?.ProductCode ?? string.Empty,
                    ProductName = i.BranchProduct?.Product?.ProductName ?? string.Empty,
                    SystemQuantity = i.SystemQuantity,
                    Quantity = i.Discrepancy,
                    ActualQuantity = i.ActualQuantity,
                    CostPrice = st.TicketType == "CUSTOMER_RETURN"
                        ? 0
                        : (i.UnitPrice ?? 0),
                    UnitPrice = i.UnitPrice,
                    UnitName = i.UnitName ?? i.BranchProduct?.Product?.BaseUnit
                }).ToList() ?? new List<InwardInventoryItemResponseDto>()
            };
        }
    }
}
