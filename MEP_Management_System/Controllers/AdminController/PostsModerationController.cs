using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers;

[ApiController]
[Route("api/admin/posts")]
[Authorize(Roles = "Admin")]
public class PostsModerationController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly IMapper _mapper;

    public PostsModerationController(MEPDatabaseContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    // ==========================================
    // 1. GET /api/admin/posts - DANH SÁCH BÀI ĐĂNG
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> GetPosts(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _context.CommunityPosts
            .Include(p => p.Category)
            .Include(p => p.User)
            .Include(p => p.Comments)
            .Include(p => p.ViolationReports)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status == status.ToUpper());

        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.Title.Contains(search) || p.User.FullName.Contains(search));

        var totalCount = await query.CountAsync();

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = _mapper.Map<List<PostDto>>(posts);

        return Ok(new PageResultDto<PostDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    // ==========================================
    // 2. PUT /api/admin/posts/{id}/lock - KHÓA BÀI ĐĂNG
    // ==========================================
    [HttpPut("{id}/lock")]
    public async Task<IActionResult> LockPost(Guid id)
    {
        var post = await _context.CommunityPosts.FindAsync(id);
        if (post == null)
            return NotFound(new { error = new { code = "MSG-404", message = "Bài đăng không tồn tại." } });

        post.Status = "LOCKED";
        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã khóa bài đăng." });
    }

    // ==========================================
    // 3. PUT /api/admin/posts/{id}/unlock - MỞ KHÓA BÀI ĐĂNG
    // ==========================================
    [HttpPut("{id}/unlock")]
    public async Task<IActionResult> UnlockPost(Guid id)
    {
        var post = await _context.CommunityPosts.FindAsync(id);
        if (post == null)
            return NotFound(new { error = new { code = "MSG-404", message = "Bài đăng không tồn tại." } });

        post.Status = "ACTIVE";
        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã mở khóa bài đăng." });
    }

    // ==========================================
    // 4. PUT /api/admin/posts/{id}/pin - GHIM BÀI ĐĂNG
    // ==========================================
    [HttpPut("{id}/pin")]
    public async Task<IActionResult> PinPost(Guid id)
    {
        var post = await _context.CommunityPosts.FindAsync(id);
        if (post == null)
            return NotFound(new { error = new { code = "MSG-404", message = "Bài đăng không tồn tại." } });

        post.IsPinned = 1;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã ghim bài đăng." });
    }

    // ==========================================
    // 5. PUT /api/admin/posts/{id}/unpin - BỎ GHIM BÀI ĐĂNG
    // ==========================================
    [HttpPut("{id}/unpin")]
    public async Task<IActionResult> UnpinPost(Guid id)
    {
        var post = await _context.CommunityPosts.FindAsync(id);
        if (post == null)
            return NotFound(new { error = new { code = "MSG-404", message = "Bài đăng không tồn tại." } });

        post.IsPinned = 0;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã bỏ ghim bài đăng." });
    }

    // ==========================================
    // 6. PUT /api/admin/posts/{id}/hide - ẨN BÀI ĐĂNG (soft delete)
    // ==========================================
    [HttpPut("{id}/hide")]
    public async Task<IActionResult> HidePost(Guid id)
    {
        var post = await _context.CommunityPosts.FindAsync(id);
        if (post == null)
            return NotFound(new { error = new { code = "MSG-404", message = "Bài đăng không tồn tại." } });

        post.Status = "HIDDEN";
        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã ẩn bài đăng." });
    }
}
