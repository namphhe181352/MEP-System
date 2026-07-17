//using System;
//using System.Linq;
//using System.Security.Claims;
//using System.Threading.Tasks;
//using AutoMapper;
//using MEP_Management_System.DTOs.SaleDTOs;
//using MEP_Management_System.Models;
//using Microsoft.EntityFrameworkCore;

//namespace MEP_Management_System.Services
//{
//    public static class InvoiceService
//    {
//        private static Guid GetBranchId(ClaimsPrincipal user)
//        {
//            var branchIdStr = user.FindFirst("branchId")?.Value;
//            if (Guid.TryParse(branchIdStr, out var branchId))
//            {
//                return branchId;
//            }
//            throw new UnauthorizedAccessException("branchId not found in token.");
//        }

//        public static async Task<InvoiceDto?> GetInvoiceByIdAsync(MEPDatabaseContext context, IMapper mapper, IOrderDiscountService orderDiscountService, Guid invoiceId, ClaimsPrincipal currentUser)
//        {
//            var branchId = GetBranchId(currentUser);
//            var invoice = await context.Invoices
//                .Include(i => i.InvoiceItems).ThenInclude(ii => ii.BranchProduct).ThenInclude(bp => bp.Product)
//                .Include(i => i.Payments)
//                .Include(i => i.Order).ThenInclude(o => o.Customer)
//                .Include(i => i.Order).ThenInclude(o => o.User)
//                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.Order.BranchId == branchId);

//            if (invoice == null) return null;

//            var dto = mapper.Map<InvoiceDto>(invoice);
//            dto.OrderId = invoice.OrderId;
//            dto.UserId = invoice.UserId;
//            dto.UserName = invoice.Order?.User?.FullName;

//            // Map the discount manually in case AutoMapper missed it from SaleMappingProfile
//            dto.DiscountPercent = invoice.DiscountPercent;
//            dto.DiscountAmount = invoice.DiscountAmount;

//            if (invoice.InvoiceItems != null && invoice.InvoiceItems.Any())
//            {
//                var subtotal = invoice.InvoiceItems.Sum(ii => ii.Quantity * ii.SellPrice);
//                dto.Subtotal = subtotal;

//                // Nếu hóa đơn đang Draft, tính discount on the fly để POS hiển thị
//                if (invoice.Status == "Draft")
//                {
//                    var (dynDiscountPercent, dynDiscountAmount, _) = await orderDiscountService.CalculateDiscountAsync(subtotal);
//                    dto.DiscountPercent = dynDiscountPercent;
//                    dto.DiscountAmount = dynDiscountAmount;
//                }

//                dto.TotalAmount = subtotal - dto.DiscountAmount;
//                dto.Items = mapper.Map<System.Collections.Generic.List<DTOs.SaleDTOs.InvoiceItemDto>>(invoice.InvoiceItems);
//            }

//            if (invoice.Payments != null && invoice.Payments.Any())
//            {
//                dto.PaymentMethod = invoice.Payments.Count > 1 ? "Combined" : invoice.Payments.First().Method.ToString();
//            }

//            return dto;
//        }
//    }
//}
