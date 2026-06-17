using AutoMapper;
using MEP.Admin.Api.DTOs;
using MEP.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MEP.Admin.Api.Controllers;

[ApiController]
[Route("api/admin/violation-reports")]
[Authorize(Roles = "Admin")]
public class ViolationReportsController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly IMapper _mapper;

    public ViolationReportsController(MEPDatabaseContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    // ==========================================
    // 1. GET /api/admin/violation-reports - DANH SÁCH BÁO CÁO VI PHẠM
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> GetReports(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _context.ViolationReports
            .Include(r => r.User)
            .Include(r => r.Post)
            .Include(r => r.Comment)
            .Include(r => r.ResolvedByUser)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status.ToUpper());

        var totalCount = await query.CountAsync();

        var reports = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = _mapper.Map<List<ViolationReportDto>>(reports);

        return Ok(new PageResultDto<ViolationReportDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    // ==========================================
    // 2. GET /api/admin/violation-reports/{id} - CHI TIẾT BÁO CÁO
    // ==========================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetReport(Guid id)
    {
        var report = await _context.ViolationReports
            .Include(r => r.User)
            .Include(r => r.Post)
            .Include(r => r.Comment)
            .Include(r => r.ResolvedByUser)
            .FirstOrDefaultAsync(r => r.ReportId == id);

        if (report == null)
            return NotFound(new { error = new { code = "MSG-404", message = "Báo cáo không tồn tại." } });

        return Ok(_mapper.Map<ViolationReportDto>(report));
    }

    // ==========================================
    // 3. PUT /api/admin/violation-reports/{id}/resolve - XỬ LÝ BÁO CÁO
    //    action: "hide_post" | "suspend_3_days" | "ban_permanent" | "dismiss"
    // ==========================================
    [HttpPut("{id}/resolve")]
    public async Task<IActionResult> ResolveReport(Guid id, [FromBody] ResolveReportRequest dto)
    {
        var report = await _context.ViolationReports
            .Include(r => r.Post)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.ReportId == id);

        if (report == null)
            return NotFound(new { error = new { code = "MSG-404", message = "Báo cáo không tồn tại." } });

        if (report.Status == "RESOLVED")
            return BadRequest(new { error = new { code = "MSG-409", message = "Báo cáo này đã được xử lý rồi." } });

        var adminIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(adminIdStr, out var adminId))
            return Unauthorized();

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        switch (dto.Action.ToLower())
        {
            case "hide_post":
                // Ẩn bài viết
                if (report.PostId.HasValue)
                {
                    var post = report.Post ?? await _context.CommunityPosts.FindAsync(report.PostId.Value);
                    if (post != null) post.Status = "HIDDEN";
                }
                break;

            case "suspend_3_days":
                // Khóa tài khoản 3 ngày — suspend the VIOLATOR (post author), not the reporter
                var violatorId = report.Post?.UserId;
                var suspendUser = violatorId.HasValue
                    ? await _context.Users.FindAsync(violatorId.Value)
                    : null;
                if (suspendUser != null)
                {
                    suspendUser.IsActive = 0;
                    suspendUser.SuspendedUntil = now.AddDays(3);
                }
                break;

            case "ban_permanent":
                // Khóa tài khoản vĩnh viễn — ban the VIOLATOR (post author), not the reporter
                var violatorIdBan = report.Post?.UserId;
                var banUser = violatorIdBan.HasValue
                    ? await _context.Users.FindAsync(violatorIdBan.Value)
                    : null;
                if (banUser != null)
                {
                    banUser.IsActive = 0;
                    banUser.Status = "BANNED";
                    banUser.SuspendedUntil = null;
                }
                break;

            case "dismiss":
                // Bỏ qua, chỉ cập nhật trạng thái
                break;

            default:
                return BadRequest(new { error = new { code = "MSG-400", message = "Action không hợp lệ. Dùng: hide_post, suspend_3_days, ban_permanent, dismiss." } });
        }

        // Cập nhật report
        report.Status = dto.Action.ToLower() == "dismiss" ? "DISMISSED" : "RESOLVED";
        report.ResolvedBy = adminId;
        report.ResolvedAt = now;
        report.AdminNote = dto.AdminNote;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Đã xử lý báo cáo vi phạm." });
    }
}
