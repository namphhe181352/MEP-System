using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.OwnerController;

/// <summary>
/// Audit log cho Owner - xem "ai chỉnh sửa cái gì" trong chi nhánh của mình.
/// Chỉ trả về logs của các user (staff) thuộc cùng branchId với caller.
/// </summary>
[ApiController]
[Route("api/owner/audit-logs")]
[Tags("Audit Log (Owner)")]
[Authorize(Roles = PermissionConstants.OWNER_ROLE)]
public class OwnerAuditLogsController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly IMapper _mapper;
    private readonly ICurrentBranchAccessor _currentBranch;

    public OwnerAuditLogsController(MEPDatabaseContext context, IMapper mapper, ICurrentBranchAccessor currentBranch)
    {
        _context = context;
        _mapper = mapper;
        _currentBranch = currentBranch;
    }

    private Guid? GetOwnerUserId()
    {
        var v = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(v, out var g) ? g : null;
    }

    /// <summary>
    /// Lấy danh sách audit logs trong chi nhánh của Owner.
    /// Filter: chỉ logs có UserId thuộc về StaffAssignment(BranchId = current owner's branch).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? action,
        [FromQuery] Guid? userId,
        [FromQuery] string? level,
        [FromQuery] string? entityType,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var ownerId = GetOwnerUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không thể xác định người dùng từ token."
            });

        var branchId = _currentBranch.GetBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh."
            });

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        // Lấy danh sách UserId thuộc chi nhánh này (gồm cả Owner)
        var userIdsInBranch = await _context.StaffAssignments
            .Where(sa => sa.BranchId == branchId.Value)
            .Select(sa => sa.UserId)
            .ToListAsync();

        if (!userIdsInBranch.Any())
        {
            return Ok(new PageResultDto<SystemLogDto>
            {
                Items = new System.Collections.Generic.List<SystemLogDto>(),
                TotalCount = 0,
                PageNumber = page,
                PageSize = pageSize
            });
        }

        var query = _context.SystemLogs
            .Include(l => l.User)
            .Where(l => userIdsInBranch.Contains(l.UserId))
            .AsQueryable();

        if (!string.IsNullOrEmpty(action))
            query = query.Where(l => l.Action.Contains(action));

        if (userId.HasValue)
            query = query.Where(l => l.UserId == userId.Value);

        if (!string.IsNullOrEmpty(level))
            query = query.Where(l => l.Level == level.ToUpper());

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(l => l.EntityType == entityType);

        if (fromDate.HasValue)
        {
            var from = DateTime.SpecifyKind(fromDate.Value, DateTimeKind.Unspecified);
            query = query.Where(l => l.Timestamp >= from);
        }

        if (toDate.HasValue)
        {
            var to = DateTime.SpecifyKind(toDate.Value, DateTimeKind.Unspecified);
            query = query.Where(l => l.Timestamp <= to);
        }

        var totalCount = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PageResultDto<SystemLogDto>
        {
            Items = _mapper.Map<System.Collections.Generic.List<SystemLogDto>>(logs),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Lấy chi tiết 1 audit log.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetLog(Guid id)
    {
        var branchId = _currentBranch.GetBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh."
            });

        var userIdsInBranch = await _context.StaffAssignments
            .Where(sa => sa.BranchId == branchId.Value)
            .Select(sa => sa.UserId)
            .ToListAsync();

        var log = await _context.SystemLogs
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.LogId == id && userIdsInBranch.Contains(l.UserId));

        if (log == null)
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy log hoặc log không thuộc chi nhánh của bạn."
            });

        return Ok(new ApiResponse<SystemLogDto>
        {
            Success = true,
            Message = "Lấy log thành công.",
            Data = _mapper.Map<SystemLogDto>(log)
        });
    }
}
