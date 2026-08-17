using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.SaleDTOs;

namespace MEP_Management_System.Services.SaleServices;

public interface IPOSProductService
{
    Task<List<ProductPriceDto>> GetProductsAsync(Guid branchId, string? search);
}
