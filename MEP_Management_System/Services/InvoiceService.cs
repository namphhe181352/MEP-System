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

        public InvoiceService(MEPDatabaseContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        private Guid GetBranchId(ClaimsPrincipal user)
        {
            return Guid.Parse(user.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found in token"));
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

            if (invoice.InvoiceItems != null && invoice.InvoiceItems.Any())
            {
                var subtotal = invoice.InvoiceItems.Sum(ii => ii.Quantity * ii.SellPrice);
                dto.Subtotal = subtotal;
                dto.DiscountAmount = invoice.DiscountAmount;
                dto.DiscountPercent = invoice.DiscountPercent;
                dto.TotalAmount = subtotal - invoice.DiscountAmount;
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
