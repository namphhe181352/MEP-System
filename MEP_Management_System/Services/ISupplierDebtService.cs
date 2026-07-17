using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.InventoryDTOs;

namespace MEP_Management_System.Services
{
    public interface ISupplierDebtService
    {
        Task<SupplierDebtDetailResponse?> GetSupplierDebtDetailAsync(Guid supplierId, ClaimsPrincipal currentUser);
    }
}
