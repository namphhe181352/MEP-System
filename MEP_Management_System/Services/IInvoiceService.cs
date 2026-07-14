using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.SaleDTOs;

namespace MEP_Management_System.Services
{
    public interface IInvoiceService
    {
        Task<InvoiceDto?> GetInvoiceByIdAsync(Guid invoiceId, ClaimsPrincipal currentUser);
    }
}
