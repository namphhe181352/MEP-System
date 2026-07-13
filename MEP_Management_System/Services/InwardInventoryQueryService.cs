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
    public class InwardInventoryQueryService : IInwardInventoryQueryService
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

            if (!IsOwner(currentUser))
            {
                var staffBranchId = GetUserBranchFromTokenOrDefault(currentUser);
                if (staffBranchId == null) return new List<InwardInventoryResponseDto>();
                query = query.Where(st => st.BranchId == staffBranchId.Value);
            }

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
                TicketCode = st.TicketCode,
                TicketType = st.TicketType,
                Status = st.Status,
                CreatedAt = st.CreatedAt ?? DateTime.MinValue,
                TotalAmount = st.TotalAmount,
                Items = st.StockTicketItems?.Select(i => new InwardInventoryItemDto
                {
                    BranchProductId = i.BranchProductId,
                    ProductId = i.ProductId,
                    ProductCode = i.BranchProduct?.Product?.ProductCode,
                    ProductName = i.BranchProduct?.Product?.ProductName,
                    Quantity = i.Discrepancy,
                    CostPrice = i.UnitCost
                }).ToList() ?? new List<InwardInventoryItemDto>()
            };
        }
    }
}
