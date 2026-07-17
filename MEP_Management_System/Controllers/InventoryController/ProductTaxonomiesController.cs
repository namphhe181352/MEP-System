using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.InventoryController
{
    /// <summary>
    /// API quản lý Brand và Category theo từng chi nhánh.
    /// Sử dụng bảng product_taxonomies với branch_id để phân biệt.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductTaxonomiesController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly Services.ICurrentBranchAccessor _branchAccessor;

        public ProductTaxonomiesController(MEPDatabaseContext context, Services.ICurrentBranchAccessor branchAccessor)
        {
            _context = context;
            _branchAccessor = branchAccessor;
        }

        private Guid? GetBranchId() => _branchAccessor.GetBranchId();

        /// <summary>
        /// GET /api/product-taxonomies?type=Brand|Category
        /// Lấy danh sách Brand hoặc Category của chi nhánh hiện tại.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTaxonomies([FromQuery] string? type)
        {
            var branchId = GetBranchId();
            if (branchId == null)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

            var query = _context.ProductTaxonomies
                .Where(t => (t.BranchId == branchId || t.BranchId == null));

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(t => t.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
            }

            var items = await query
                .OrderBy(t => t.Name)
                .Select(t => new ProductTaxonomyDto
                {
                    TaxonomyId = t.TaxonomyId,
                    Name = t.Name,
                    Type = t.Type,
                    BranchId = t.BranchId,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            return Ok(new ApiResponse<List<ProductTaxonomyDto>>
            {
                Success = true,
                Data = items
            });
        }

        /// <summary>
        /// GET /api/product-taxonomies/{id}
        /// Lấy chi tiết một taxonomy.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetTaxonomyById(Guid id)
        {
            var taxonomy = await _context.ProductTaxonomies.FindAsync(id);
            if (taxonomy == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy taxonomy." });

            var dto = new ProductTaxonomyDto
            {
                TaxonomyId = taxonomy.TaxonomyId,
                Name = taxonomy.Name,
                Type = taxonomy.Type,
                BranchId = taxonomy.BranchId,
                CreatedAt = taxonomy.CreatedAt
            };

            return Ok(new ApiResponse<ProductTaxonomyDto> { Success = true, Data = dto });
        }

        /// <summary>
        /// POST /api/product-taxonomies
        /// Tạo mới Brand hoặc Category cho chi nhánh.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateTaxonomy([FromBody] CreateProductTaxonomyDto dto)
        {
            var branchId = GetBranchId();
            if (branchId == null)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Tên không được để trống." });

            if (string.IsNullOrWhiteSpace(dto.Type))
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Loại không được để trống (Brand hoặc Category)." });

            var normalizedType = char.ToUpper(dto.Type[0]) + dto.Type.Substring(1).ToLower();
            if (normalizedType != "Brand" && normalizedType != "Category")
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Loại phải là 'Brand' hoặc 'Category'." });

            // Check duplicate name for this branch and type
            var exists = await _context.ProductTaxonomies
                .AnyAsync(t => t.Name.ToLower() == dto.Name.Trim().ToLower()
                    && t.Type == normalizedType
                    && (t.BranchId == branchId || t.BranchId == null));

            if (exists)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Tên đã tồn tại." });

            var taxonomy = new ProductTaxonomy
            {
                TaxonomyId = Guid.NewGuid(),
                Name = dto.Name.Trim(),
                Type = normalizedType,
                BranchId = branchId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ProductTaxonomies.Add(taxonomy);
            await _context.SaveChangesAsync();

            var result = new ProductTaxonomyDto
            {
                TaxonomyId = taxonomy.TaxonomyId,
                Name = taxonomy.Name,
                Type = taxonomy.Type,
                BranchId = taxonomy.BranchId,
                CreatedAt = taxonomy.CreatedAt
            };

            return CreatedAtAction(nameof(GetTaxonomyById), new { id = taxonomy.TaxonomyId },
                new ApiResponse<ProductTaxonomyDto> { Success = true, Message = "Tạo thành công.", Data = result });
        }

        /// <summary>
        /// PUT /api/product-taxonomies/{id}
        /// Cập nhật Brand hoặc Category.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateTaxonomy(Guid id, [FromBody] UpdateProductTaxonomyDto dto)
        {
            var branchId = GetBranchId();
            if (branchId == null)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

            var taxonomy = await _context.ProductTaxonomies.FindAsync(id);
            if (taxonomy == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy taxonomy." });

            // Check permission: must own the taxonomy or it's a global one
            if (taxonomy.BranchId != null && taxonomy.BranchId != branchId)
                return Forbid();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Tên không được để trống." });

            // Check duplicate name for this branch and type (excluding current)
            var exists = await _context.ProductTaxonomies
                .AnyAsync(t => t.TaxonomyId != id
                    && t.Name.ToLower() == dto.Name.Trim().ToLower()
                    && t.Type == taxonomy.Type
                    && (t.BranchId == branchId || t.BranchId == null));

            if (exists)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Tên đã tồn tại." });

            taxonomy.Name = dto.Name.Trim();
            await _context.SaveChangesAsync();

            var result = new ProductTaxonomyDto
            {
                TaxonomyId = taxonomy.TaxonomyId,
                Name = taxonomy.Name,
                Type = taxonomy.Type,
                BranchId = taxonomy.BranchId,
                CreatedAt = taxonomy.CreatedAt
            };

            return Ok(new ApiResponse<ProductTaxonomyDto> { Success = true, Message = "Cập nhật thành công.", Data = result });
        }

        /// <summary>
        /// DELETE /api/product-taxonomies/{id}
        /// Xóa Brand hoặc Category.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteTaxonomy(Guid id)
        {
            var branchId = GetBranchId();
            if (branchId == null)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

            var taxonomy = await _context.ProductTaxonomies.FindAsync(id);
            if (taxonomy == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy taxonomy." });

            // Check permission
            if (taxonomy.BranchId != null && taxonomy.BranchId != branchId)
                return Forbid();

            // Check if taxonomy is being used by any product
            var isInUse = await _context.Products
                .AnyAsync(p => p.BrandTaxonomyId == id || p.CategoryTaxonomyId == id);

            if (isInUse)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Không thể xóa vì đang được sử dụng bởi sản phẩm." });

            _context.ProductTaxonomies.Remove(taxonomy);
            await _context.SaveChangesAsync();

            return Ok(new ApiResponse<object> { Success = true, Message = "Xóa thành công." });
        }
    }

    public class ProductTaxonomyDto
    {
        public Guid TaxonomyId { get; set; }
        public string Name { get; set; } = null!;
        public string Type { get; set; } = null!; // "Brand" or "Category"
        public Guid? BranchId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateProductTaxonomyDto
    {
        public string Name { get; set; } = null!;
        public string Type { get; set; } = null!; // "Brand" or "Category"
    }

    public class UpdateProductTaxonomyDto
    {
        public string Name { get; set; } = null!;
    }
}
