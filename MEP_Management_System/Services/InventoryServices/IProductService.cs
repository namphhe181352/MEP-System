using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Services.InventoryServices;

public interface IProductService
{
    Task<ApiResponse<object>> GetProductsLookupAsync(ClaimsPrincipal user, Guid? branchId, int pageNumber, int pageSize, string? searchTerm);
    Task<ApiResponse<PaginatedResponse<ProductUpsertDto>>> GetProductsAsync(ClaimsPrincipal user, ProductParameters @params, Guid? branchId);
    Task<ApiResponse<object>> SearchProductsAsync(ClaimsPrincipal user, string term, int limit, Guid? branchId);
    Task<ApiResponse<object>> SuggestNewProductAsync(ClaimsPrincipal user, string term, Guid? branchId);
    Task<ApiResponse<ProductUpsertDto>> GetProductByIdAsync(ClaimsPrincipal user, Guid id, Guid? branchId);
    Task<ApiResponse<ProductUpsertDto>> CreateProductAsync(ClaimsPrincipal user, ProductUpsertDto dto, Guid? branchId);
    Task<ApiResponse<ProductUpsertDto>> UpdateProductAsync(ClaimsPrincipal user, Guid id, UpdateProductDto dto, Guid? branchId);
    Task<ApiResponse<object>> DeleteProductAsync(ClaimsPrincipal user, Guid id, Guid? branchId);
    Task<ApiResponse<object>> ToggleStatusAsync(ClaimsPrincipal user, Guid id, bool isActive, Guid? branchId);
    Task<ApiResponse<object>> ToggleStatusBulkAsync(ClaimsPrincipal user, BulkToggleStatusRequest request, Guid? branchId);
    Task<ApiResponse<object>> BulkImportProductsAsync(ClaimsPrincipal user, ProductBulkImportDto dto, Guid? branchId);
    Task<ApiResponse<List<TaxonomyItemDto>>> GetCategoriesAsync(ClaimsPrincipal user, Guid? branchId);
    Task<ApiResponse<List<TaxonomyItemDto>>> GetBrandsAsync(ClaimsPrincipal user, Guid? branchId);
    Task<ApiResponse<object>> CreateCategoryAsync(ClaimsPrincipal user, CreateTaxonomyDto dto, Guid? branchId);
    Task<ApiResponse<object>> CreateBrandAsync(ClaimsPrincipal user, CreateTaxonomyDto dto, Guid? branchId);
    Task<ApiResponse<object>> RenameCategoryAsync(ClaimsPrincipal user, RenameTaxonomyDto dto, Guid? branchId);
    Task<ApiResponse<object>> RenameBrandAsync(ClaimsPrincipal user, RenameTaxonomyDto dto, Guid? branchId);
    Task<ApiResponse<object>> DeleteCategoryAsync(ClaimsPrincipal user, string name, Guid? branchId);
    Task<ApiResponse<object>> DeleteBrandAsync(ClaimsPrincipal user, string name, Guid? branchId);
    Task<ApiResponse<List<TaxonomyItemDto>>> GetLocationsAsync(ClaimsPrincipal user, Guid? branchId);
    Task<ApiResponse<object>> CreateLocationAsync(ClaimsPrincipal user, CreateTaxonomyDto dto, Guid? branchId);
    Task<ApiResponse<object>> RenameLocationAsync(ClaimsPrincipal user, RenameTaxonomyDto dto, Guid? branchId);
}
