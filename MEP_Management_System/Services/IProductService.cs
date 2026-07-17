//using System;
//using System.Collections.Generic;
//using System.Security.Claims;
//using System.Threading.Tasks;
//using MEP_Management_System.DTOs.InventoryDTOs;
//using MEP_Management_System.DTOs.SaleDTOs;

//namespace MEP_Management_System.Services
//{
//    public interface IProductService
//    {
//        Task<ProductUpsertDto?> GetProductByIdAsync(Guid id, ClaimsPrincipal currentUser);
//        Task<ProductPriceDto?> GetProductPriceAsync(Guid productId, ClaimsPrincipal currentUser);
//        Task<ProductStockDto?> GetProductStockAsync(Guid productId, ClaimsPrincipal currentUser);
//        Task<ProductUpsertDto?> GetProductByCodeAsync(string productCode, ClaimsPrincipal currentUser);
//        Task<List<ProductUpsertDto>> SearchProductsAsync(string keyword, ClaimsPrincipal currentUser);
//    }
//}
