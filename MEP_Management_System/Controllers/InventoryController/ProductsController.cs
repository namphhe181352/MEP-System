using AutoMapper;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Services;
using MEP_Management_System.Services.InventoryServices;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Quan ly Kho (Inventory)")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly ICurrentBranchAccessor _currentBranch;

        public ProductsController(IProductService productService, ICurrentBranchAccessor currentBranch)
        {
            _productService = productService;
            _currentBranch = currentBranch;
        }

        private Guid? GetCurrentBranchId() => _currentBranch.GetBranchId();

        [HttpGet("lookup")]
        public async Task<IActionResult> GetProductsLookup(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 200,
            [FromQuery] string? searchTerm = null)
        {
            var result = await _productService.GetProductsLookupAsync(User, GetCurrentBranchId(), pageNumber, pageSize, searchTerm);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] DTOs.InventoryDTOs.ProductParameters @params)
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_VIEW)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CREATE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_UPDATE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_DELETE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Ban khong co quyen xem danh sach san pham." });

            var result = await _productService.GetProductsAsync(User, @params, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchProducts([FromQuery] string term, [FromQuery] int limit = 10)
        {
            var result = await _productService.SearchProductsAsync(User, term, limit, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpGet("suggest-new-product")]
        public async Task<IActionResult> SuggestNewProduct([FromQuery] string term)
        {
            var result = await _productService.SuggestNewProductAsync(User, term, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetProductById(Guid id)
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_VIEW)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CREATE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_UPDATE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_DELETE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Ban khong co quyen xem chi tiet san pham." });

            var result = await _productService.GetProductByIdAsync(User, id, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPost]
        [HasPermission(PermissionConstants.PRODUCT_CREATE)]
        public async Task<IActionResult> CreateProduct([FromBody] ProductUpsertDto dto)
        {
            var result = await _productService.CreateProductAsync(User, dto, GetCurrentBranchId());
            return CreatedAtAction(nameof(GetProductById), new { id = result.Data?.ProductId }, result);
        }

        [HttpPost("bulk-import")]
        [HasPermission(PermissionConstants.PRODUCT_CREATE)]
        public async Task<IActionResult> BulkImportProducts([FromBody] ProductBulkImportDto dto)
        {
            var result = await _productService.BulkImportProductsAsync(User, dto, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductDto dto)
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_UPDATE))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Ban khong co quyen chinh sua san pham nay." });

            var result = await _productService.UpdateProductAsync(User, id, dto, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_DELETE))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Ban khong co quyen xoa san pham nay." });

            var result = await _productService.DeleteProductAsync(User, id, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPatch("{id:guid}/toggle-status")]
        [HasPermission(PermissionConstants.PRODUCT_UPDATE)]
        public async Task<IActionResult> ToggleStatus(Guid id, [FromQuery] bool isActive)
        {
            var result = await _productService.ToggleStatusAsync(User, id, isActive, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPatch("toggle-status-bulk")]
        [HasPermission(PermissionConstants.PRODUCT_UPDATE)]
        public async Task<IActionResult> ToggleStatusBulk([FromBody] BulkToggleStatusRequest request)
        {
            var result = await _productService.ToggleStatusBulkAsync(User, request, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var result = await _productService.GetCategoriesAsync(User, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpGet("brands")]
        public async Task<IActionResult> GetBrands()
        {
            var result = await _productService.GetBrandsAsync(User, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPost("categories")]
        [HasPermission(PermissionConstants.PRODUCT_CATEGORY_MANAGE)]
        public async Task<IActionResult> CreateCategory([FromBody] CreateTaxonomyDto dto)
        {
            var result = await _productService.CreateCategoryAsync(User, dto, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPost("brands")]
        [HasPermission(PermissionConstants.PRODUCT_CATEGORY_MANAGE)]
        public async Task<IActionResult> CreateBrand([FromBody] CreateTaxonomyDto dto)
        {
            var result = await _productService.CreateBrandAsync(User, dto, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPut("categories/rename")]
        [HasPermission(PermissionConstants.PRODUCT_CATEGORY_MANAGE)]
        public async Task<IActionResult> RenameCategory([FromBody] RenameTaxonomyDto dto)
        {
            var result = await _productService.RenameCategoryAsync(User, dto, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPut("brands/rename")]
        [HasPermission(PermissionConstants.PRODUCT_CATEGORY_MANAGE)]
        public async Task<IActionResult> RenameBrand([FromBody] RenameTaxonomyDto dto)
        {
            var result = await _productService.RenameBrandAsync(User, dto, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpDelete("categories/{name}")]
        [HasPermission(PermissionConstants.PRODUCT_CATEGORY_MANAGE)]
        public async Task<IActionResult> DeleteCategory(string name)
        {
            var result = await _productService.DeleteCategoryAsync(User, name, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpDelete("brands/{name}")]
        [HasPermission(PermissionConstants.PRODUCT_CATEGORY_MANAGE)]
        public async Task<IActionResult> DeleteBrand(string name)
        {
            var result = await _productService.DeleteBrandAsync(User, name, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpGet("locations")]
        public async Task<IActionResult> GetLocations()
        {
            var result = await _productService.GetLocationsAsync(User, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPost("locations")]
        public async Task<IActionResult> CreateLocation([FromBody] CreateTaxonomyDto dto)
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_CREATE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Ban khong co quyen them vi tri kho." });

            var result = await _productService.CreateLocationAsync(User, dto, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPut("locations")]
        public async Task<IActionResult> RenameLocation([FromBody] RenameTaxonomyDto dto)
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_UPDATE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Ban khong co quyen quan ly vi tri kho." });

            var result = await _productService.RenameLocationAsync(User, dto, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpGet("attribute-types")]
        public async Task<IActionResult> GetAttributeTypes()
        {
            var branchId = GetCurrentBranchId();
            var db = HttpContext.RequestServices.GetRequiredService<MEP_Management_System.Models.MEPDatabaseContext>();
            
            // Temporary index fix
            try {
                await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(db.Database, "DROP INDEX IF EXISTS ix_product_attribute_types_type_name;");
                await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(db.Database, "CREATE UNIQUE INDEX IF NOT EXISTS ix_product_attribute_types_type_name_branch_id ON product_attribute_types (type_name, branch_id);");
            } catch { }

            var types = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                System.Linq.Queryable.Where(db.ProductAttributeTypes, t => t.BranchId == branchId || t.BranchId == null));
            return Ok(new ApiResponse<List<MEP_Management_System.Models.ProductAttributeType>> { Success = true, Data = types });
        }

        [HttpPost("attribute-types")]
        public async Task<IActionResult> CreateAttributeType([FromBody] CreateAttributeTypeDto dto)
        {
            var branchId = GetCurrentBranchId();
            var db = HttpContext.RequestServices.GetRequiredService<MEP_Management_System.Models.MEPDatabaseContext>();
            var exists = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                db.ProductAttributeTypes, t => t.TypeName.ToLower() == dto.TypeName.ToLower() && t.BranchId == branchId);
            if (exists) return BadRequest(new ApiResponse<object> { Success = false, Message = "Tên thuộc tính đã tồn tại trong chi nhánh này." });
            
            var type = new MEP_Management_System.Models.ProductAttributeType
            {
                TypeId = Guid.NewGuid(),
                BranchId = branchId,
                TypeName = dto.TypeName.Trim(),
                TypeCode = dto.TypeCode?.Trim()
            };
            db.ProductAttributeTypes.Add(type);
            await db.SaveChangesAsync();
            return Ok(new ApiResponse<MEP_Management_System.Models.ProductAttributeType> { Success = true, Data = type });
        }

        [HttpPut("attribute-types/{id:guid}")]
        public async Task<IActionResult> UpdateAttributeType(Guid id, [FromBody] UpdateAttributeTypeDto dto)
        {
            var branchId = GetCurrentBranchId();
            var db = HttpContext.RequestServices.GetRequiredService<MEP_Management_System.Models.MEPDatabaseContext>();
            var type = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                db.ProductAttributeTypes, t => t.TypeId == id && (t.BranchId == branchId || t.BranchId == null));
            if (type == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy thuộc tính." });
            
            if (!string.IsNullOrWhiteSpace(dto.TypeName) && dto.TypeName.Trim() != type.TypeName)
            {
                var exists = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                    db.ProductAttributeTypes, t => t.TypeName.ToLower() == dto.TypeName.ToLower() && (t.BranchId == branchId || t.BranchId == null) && t.TypeId != id);
                if (exists) return BadRequest(new ApiResponse<object> { Success = false, Message = "Tên thuộc tính đã tồn tại." });
                type.TypeName = dto.TypeName.Trim();
            }
            if (dto.TypeCode != null) type.TypeCode = dto.TypeCode.Trim();
            await db.SaveChangesAsync();
            return Ok(new ApiResponse<MEP_Management_System.Models.ProductAttributeType> { Success = true, Data = type });
        }

        [HttpDelete("attribute-types/{id:guid}")]
        public async Task<IActionResult> DeleteAttributeType(Guid id)
        {
            var branchId = GetCurrentBranchId();
            var db = HttpContext.RequestServices.GetRequiredService<MEP_Management_System.Models.MEPDatabaseContext>();
            var type = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                db.ProductAttributeTypes, t => t.TypeId == id && (t.BranchId == branchId || t.BranchId == null));
            if (type == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy thuộc tính." });
            
            db.ProductAttributeTypes.Remove(type);
            await db.SaveChangesAsync();
            return Ok(new ApiResponse<object> { Success = true, Message = "Xóa thành công." });
        }
    }
}
