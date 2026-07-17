using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly MEPDatabaseContext _context;
        private readonly IMapper _mapper;
        private readonly IOrderDiscountService _orderDiscountService;

        public InvoiceService(MEPDatabaseContext context, IMapper mapper, IOrderDiscountService orderDiscountService)
        {
            _context = context;
            _mapper = mapper;
            _orderDiscountService = orderDiscountService;
        }

        private Guid GetBranchId(ClaimsPrincipal user)
        {
            var branchIdStr = user.FindFirst("branchId")?.Value;
            if (Guid.TryParse(branchIdStr, out var branchId))
            {
                return branchId;
            }
            throw new UnauthorizedAccessException("branchId not found in token.");
        }

        public async Task<InvoiceDto?> GetInvoiceByIdAsync(Guid invoiceId, ClaimsPrincipal currentUser)
        {
            var branchId = GetBranchId(currentUser);
            var invoice = await _context.Invoices
                .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp.Product)
                .Include(i => i.Payments)
                .Include(i => i.Order).ThenInclude(o => o.Customer)
                .Include(i => i.Order).ThenInclude(o => o.User)
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);

            if (invoice == null) return null;

            var dto = _mapper.Map<InvoiceDto>(invoice);
            dto.OrderId = invoice.OrderId;
            dto.UserId = invoice.UserId;
            dto.UserName = invoice.Order?.User?.FullName;
            
            // Map the discount manually in case AutoMapper missed it from SaleMappingProfile
            dto.DiscountPercent = invoice.DiscountPercent;
            dto.DiscountAmount = invoice.DiscountAmount;

            if (invoice.InvoiceItems != null && invoice.InvoiceItems.Any())
            {
                var subtotal = invoice.InvoiceItems.Sum(ii => ii.Quantity * ii.SellPrice);
                dto.Subtotal = subtotal;

                // Nếu hóa đơn đang Draft, tính discount on the fly để POS hiển thị
                if (invoice.Status == "Draft")
                {
                    var (dynDiscountPercent, dynDiscountAmount, _) = await _orderDiscountService.CalculateDiscountAsync(subtotal);
                    dto.DiscountPercent = dynDiscountPercent;
                    dto.DiscountAmount = dynDiscountAmount;
                }

                dto.TotalAmount = subtotal - dto.DiscountAmount;
                dto.Items = _mapper.Map<System.Collections.Generic.List<DTOs.SaleDTOs.InvoiceItemDto>>(invoice.InvoiceItems);
            }

            if (invoice.Payments != null && invoice.Payments.Any())
            {
                dto.PaymentMethod = invoice.Payments.Count > 1 ? "Combined" : invoice.Payments.First().Method.ToString();
            }

            return dto;
        }
    }
}
