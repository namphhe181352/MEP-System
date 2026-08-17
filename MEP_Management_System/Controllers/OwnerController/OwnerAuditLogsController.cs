using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    private readonly IOwnerAuditLogService _auditLogService;
    private readonly ICurrentBranchAccessor _currentBranch;

    public OwnerAuditLogsController(IOwnerAuditLogService auditLogService, ICurrentBranchAccessor currentBranch)
    {
        _auditLogService = auditLogService;
        _currentBranch = currentBranch;
    }

    private Guid? GetOwnerUserId()
    {
        var v = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(v, out var g) ? g : null;
    }

    private Guid? GetBranchId() => _currentBranch.GetBranchId();

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
        [FromQuery] string? userName,
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

        var branchId = GetBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh."
            });

        var result = await _auditLogService.GetLogsAsync(
            branchId.Value, action, userId, level, entityType,
            fromDate, toDate, userName, page, pageSize);

        return Ok(result);
    }

    /// <summary>
    /// Lấy chi tiết 1 audit log.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetLog(Guid id)
    {
        var branchId = GetBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh."
            });

        var log = await _auditLogService.GetLogByIdAsync(id, branchId.Value);
        if (log == null)
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy log hoặc log không thuộc chi nhánh của bạn."
            });

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Lấy log thành công.",
            Data = log
        });
    }

    /// <summary>
    /// Lấy danh sách user (thuộc chi nhánh của Owner) cho dropdown filter audit log.
    /// Không filter IsActive ngoại trừ DELETED.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetAuditLogUsers()
    {
        var branchId = GetBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh."
            });

        var users = await _auditLogService.GetAuditLogUsersAsync(branchId.Value);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Danh sách user cho audit log.",
            Data = users
        });
    }
}
