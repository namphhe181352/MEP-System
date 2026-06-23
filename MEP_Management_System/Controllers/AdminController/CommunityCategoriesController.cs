using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers;

[ApiController]
[Route("api/admin/community-categories")]
[Authorize(Roles = "Admin")]
[Tags("Admin (Admin)")]
public class CommunityCategoriesController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly IMapper _mapper;

    public CommunityCategoriesController(MEPDatabaseContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    // ==========================================
    // 1. GET /api/admin/community-categories - DANH SÁCH DANH MỤC
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> GetCategories([FromQuery] bool includeArchived = false)
    {
        var query = _context.CommunityCategories
            .Include(c => c.Parent)
            .Include(c => c.CommunityPosts)
            .AsQueryable();

        if (!includeArchived)
            query = query.Where(c => !c.IsArchived);

        var categories = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.CategoryName)
            .ToListAsync();

        return Ok(_mapper.Map<List<CommunityCategoryDto>>(categories));
    }

    // ==========================================
    // 2. POST /api/admin/community-categories - TẠO DANH MỤC MỚI
    // ==========================================
    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest dto)
    {
        // Kiểm tra tên trùng
        var nameExists = await _context.CommunityCategories
            .AnyAsync(c => c.CategoryName == dto.CategoryName && !c.IsArchived);
        if (nameExists)
            return Conflict(new { error = new { code = "MSG-409", message = "Tên danh mục đã tồn tại." } });

        // Kiểm tra parentId hợp lệ
        if (dto.ParentId.HasValue)
        {
            var parentExists = await _context.CommunityCategories
                .AnyAsync(c => c.CategoryId == dto.ParentId.Value && !c.IsArchived);
            if (!parentExists)
                return BadRequest(new { error = new { code = "MSG-404", message = "Danh mục cha không tồn tại." } });
        }

        var slug = GenerateSlug(dto.CategoryName);

        // Đảm bảo slug là duy nhất
        var slugExists = await _context.CommunityCategories.AnyAsync(c => c.Slug == slug);
        if (slugExists) slug = $"{slug}-{DateTime.UtcNow.Ticks}";

        var category = new CommunityCategory
        {
            CategoryId = Guid.NewGuid(),
            CategoryName = dto.CategoryName,
            Description = dto.Description,
            SortOrder = dto.SortOrder,
            ParentId = dto.ParentId,
            Slug = slug,
            IsArchived = false
        };

        _context.CommunityCategories.Add(category);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCategories), new { id = category.CategoryId },
            new { categoryId = category.CategoryId, slug = category.Slug });
    }

    // ==========================================
    // 3. PUT /api/admin/community-categories/{id} - CẬP NHẬT DANH MỤC
    // ==========================================
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest dto)
    {
        var category = await _context.CommunityCategories.FindAsync(id);
        if (category == null || category.IsArchived)
            return NotFound(new { error = new { code = "MSG-404", message = "Danh mục không tồn tại." } });

        // Kiểm tra parentId hợp lệ (tránh self-reference)
        if (dto.ParentId.HasValue)
        {
            if (dto.ParentId.Value == id)
                return BadRequest(new { error = new { code = "MSG-400", message = "Danh mục không thể là cha của chính nó." } });

            var parentExists = await _context.CommunityCategories
                .AnyAsync(c => c.CategoryId == dto.ParentId.Value && !c.IsArchived);
            if (!parentExists)
                return BadRequest(new { error = new { code = "MSG-404", message = "Danh mục cha không tồn tại." } });
        }

        if (!string.IsNullOrEmpty(dto.CategoryName) && dto.CategoryName != category.CategoryName)
        {
            var nameExists = await _context.CommunityCategories
                .AnyAsync(c => c.CategoryName == dto.CategoryName && c.CategoryId != id && !c.IsArchived);
            if (nameExists)
                return Conflict(new { error = new { code = "MSG-409", message = "Tên danh mục đã tồn tại." } });

            category.CategoryName = dto.CategoryName;
            category.Slug = GenerateSlug(dto.CategoryName); // auto-update slug when name changes
        }

        if (dto.Description != null) category.Description = dto.Description;
        if (dto.ParentId.HasValue) category.ParentId = dto.ParentId;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã cập nhật danh mục." });
    }

    // ==========================================
    // 4. DELETE /api/admin/community-categories/{id} - XÓA MỀM DANH MỤC
    // ==========================================
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var category = await _context.CommunityCategories
            .Include(c => c.CommunityPosts)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category == null || category.IsArchived)
            return NotFound(new { error = new { code = "MSG-404", message = "Danh mục không tồn tại." } });

        // Chỉ xóa mềm nếu không còn bài đăng đang ACTIVE
        var hasActivePosts = category.CommunityPosts.Any(p => p.Status == "ACTIVE");
        if (hasActivePosts)
            return BadRequest(new { error = new { code = "MSG-400", message = "Danh mục vẫn còn bài đăng đang hoạt động, không thể xóa." } });

        category.IsArchived = true;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã xóa danh mục (ẩn)." });
    }

    // ==========================================
    // 5. PUT /api/admin/community-categories/{id}/reorder - SẮP XẾP LẠI
    // ==========================================
    [HttpPut("{id}/reorder")]
    public async Task<IActionResult> ReorderCategory(Guid id, [FromBody] ReorderCategoryRequest dto)
    {
        var category = await _context.CommunityCategories.FindAsync(id);
        if (category == null || category.IsArchived)
            return NotFound(new { error = new { code = "MSG-404", message = "Danh mục không tồn tại." } });

        category.SortOrder = dto.NewOrder;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã cập nhật thứ tự danh mục.", newOrder = dto.NewOrder });
    }

    // ==========================================
    // HELPER: Sinh slug từ tên tiếng Việt
    // ==========================================
    private static string GenerateSlug(string name)
    {
        // Bước 1: Chuyển thường
        var slug = name.ToLower().Trim();

        // Bước 2: Thay thế dấu cách bằng dấu gạch ngang
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "-");

        // Bước 3: Xóa ký tự đặc biệt (giữ lại chữ cái, số và dấu gạch ngang)
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");

        // Bước 4: Xóa dấu gạch ngang trùng lặp
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");

        return slug.Trim('-');
    }
}
