using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using MEP_Management_System.Services.InventoryService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductTaxonomiesController : ControllerBase
    {
        private readonly IProductTaxonomyService _taxonomyService;
        private readonly Services.ICurrentBranchAccessor _branchAccessor;

        public ProductTaxonomiesController(IProductTaxonomyService taxonomyService, Services.ICurrentBranchAccessor branchAccessor)
        {
            _taxonomyService = taxonomyService;
            _branchAccessor = branchAccessor;
        }

        private Guid? GetBranchId() => _branchAccessor.GetBranchId();

        [HttpGet]
        public async Task<IActionResult> GetTaxonomies([FromQuery] string? type)
        {
            var branchId = GetBranchId();
            if (branchId == null)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

            var items = await _taxonomyService.GetTaxonomiesAsync(branchId, type);

            return Ok(new ApiResponse<List<ProductTaxonomy>>
            {
                Success = true,
                Data = items
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetTaxonomyById(Guid id)
        {
            var taxonomy = await _taxonomyService.GetTaxonomyByIdAsync(id);
            if (taxonomy == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy taxonomy." });

            return Ok(new ApiResponse<ProductTaxonomy> { Success = true, Data = taxonomy });
        }

        [HttpPost]
        public async Task<IActionResult> CreateTaxonomy([FromBody] CreateProductTaxonomyDto dto)
        {
            var branchId = GetBranchId();
            if (branchId == null)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

            var taxonomy = await _taxonomyService.CreateTaxonomyAsync(dto, branchId);

            return CreatedAtAction(nameof(GetTaxonomyById), new { id = taxonomy.TaxonomyId },
                new ApiResponse<ProductTaxonomy> { Success = true, Message = "Tạo thành công.", Data = taxonomy });
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateTaxonomy(Guid id, [FromBody] UpdateProductTaxonomyDto dto)
        {
            var branchId = GetBranchId();
            if (branchId == null)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

            var taxonomy = await _taxonomyService.UpdateTaxonomyAsync(id, dto, branchId);
            if (taxonomy == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "Không tìm thấy taxonomy." });

            return Ok(new ApiResponse<ProductTaxonomy> { Success = true, Message = "Cập nhật thành công.", Data = taxonomy });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteTaxonomy(Guid id)
        {
            var branchId = GetBranchId();
            if (branchId == null)
                return BadRequest(new ApiResponse<object> { Success = false, Message = "Không xác định được chi nhánh." });

            await _taxonomyService.DeleteTaxonomyAsync(id, branchId);

            return Ok(new ApiResponse<object> { Success = true, Message = "Xóa thành công." });
        }
    }
}
