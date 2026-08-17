using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.OwnerController;

/// <summary>
/// Controller quản lý nhân viên dành riêng cho Owner (Hệ thống đơn chi nhánh).
/// </summary>
[ApiController]
[Route("api/owner/staffs")]
[Tags("Quản lý Nhân viên (Owner)")]
[Authorize]
public class OwnerStaffController : ControllerBase
{
    private readonly IOwnerStaffService _staffService;
    private readonly ICurrentBranchAccessor _currentBranch;

    public OwnerStaffController(IOwnerStaffService staffService, ICurrentBranchAccessor currentBranch)
    {
        _staffService = staffService;
        _currentBranch = currentBranch;
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("userId")?.Value;
        if (string.IsNullOrWhiteSpace(claim) || !Guid.TryParse(claim, out var userId))
            return null;
        return userId;
    }

    private Guid? GetCurrentBranchId() => _currentBranch.GetBranchId();

    #region GET /api/owner/staffs

    /// <summary>
    /// Lấy danh sách nhân viên của chi nhánh hiện tại.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionConstants.STAFF_VIEW)]
    public async Task<IActionResult> GetStaffs(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] bool? isDeleted = false,
        [FromQuery] string? view = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không thể xác định người dùng từ token."
            });

        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh."
            });

        var (items, totalCount, pageNumber, pageSizeResult, totalPages, hasNext, hasPrev) =
            await _staffService.GetStaffsAsync(branchId.Value, ownerId.Value, search, isActive, isDeleted, view, page, pageSize);

        string viewMessage = string.Equals(view, "all", StringComparison.OrdinalIgnoreCase) ? "Danh sách tất cả nhân viên."
            : string.Equals(view, "hidden", StringComparison.OrdinalIgnoreCase) ? "Danh sách nhân viên đã khóa."
            : string.Equals(view, "deleted", StringComparison.OrdinalIgnoreCase) ? "Danh sách nhân viên đã xóa."
            : "Danh sách nhân viên đang hoạt động.";

        return Ok(new ApiResponse<PaginatedResponse<OwnerStaffDto>>
        {
            Success = true,
            Message = viewMessage,
            Data = new PaginatedResponse<OwnerStaffDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSizeResult,
                TotalPages = totalPages,
                HasNextPage = hasNext,
                HasPreviousPage = hasPrev
            }
        });
    }

    #endregion

    #region GET /api/owner/staffs/{id}/check-relations
    /// <summary>
    /// Kiểm tra quan hệ dữ liệu của nhân viên trước khi xóa.
    /// </summary>
    /// <remarks>
    /// NOTE: Owner luôn pass permission check (xem PermissionAuthorizationHandler).
    /// HasPermission đặt ở đây vì:
    /// 1. Audit trail - ghi log ai check relations
    /// 2. Intent documentation - thể hiện rõ đây là bước trước khi xóa
    /// 3. Security-in-depth - phòng trường hợp user không phải Owner gọi trực tiếp qua URL
    /// </remarks>
    [HttpGet("{id:guid}/check-relations")]
    [HasPermission(PermissionConstants.STAFF_DELETE)]
    public async Task<IActionResult> CheckStaffRelations(Guid id)
    {
        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh."
            });

        var result = await _staffService.CheckStaffRelationsAsync(id, branchId.Value);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = (result as dynamic)?.message ?? "",
            Data = result
        });
    }
    #endregion

    #region GET /api/owner/staffs/{id}/check-usage
    /// <summary>
    /// Kiểm tra xem nhân viên có đang liên kết với các bảng dữ liệu nào không.
    /// </summary>
    [HttpGet("{id:guid}/check-usage")]
    [HasPermission(PermissionConstants.STAFF_DELETE)]
    public async Task<IActionResult> CheckStaffUsage(Guid id)
    {
        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh."
            });

        var result = await _staffService.CheckStaffUsageAsync(id, branchId.Value);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = (result as dynamic)?.message ?? "",
            Data = result
        });
    }
    #endregion

    #region GET /api/owner/staffs/role-permissions

    /// <summary>
    /// Lấy danh sach quyen mac dinh cua cac Role SalesStaff & InventoryStaff.
    /// Dung de dong bo voi cau hinh tu Admin.
    /// </summary>
    [HttpGet("role-permissions")]
    [HasPermission(PermissionConstants.STAFF_VIEW)]
    public async Task<IActionResult> GetRolePermissions()
    {
        var result = await _staffService.GetRolePermissionsAsync();
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Danh sach quyen mac dinh cua cac Role.",
            Data = result
        });
    }

    #endregion

    #region GET /api/owner/staffs/available-permissions

    /// <summary>
    /// Lấy danh sách quyền hạn khả dụng từ DB.
    /// </summary>
    [HttpGet("available-permissions")]
    [HasPermission(PermissionConstants.STAFF_VIEW)]
    public async Task<IActionResult> GetAvailablePermissions()
    {
        var permissions = await _staffService.GetAvailablePermissionsAsync();
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Danh sách quyền hạn khả dụng trong hệ thống.",
            Data = permissions
        });
    }

    #endregion

    #region GET /api/owner/staffs/{id}

    /// <summary>
    /// Lấy chi tiết một nhân viên.
    /// </summary>
    [HttpGet("{id:guid}")]
    [HasPermission(PermissionConstants.STAFF_VIEW)]
    public async Task<IActionResult> GetStaffById(Guid id)
    {
        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh."
            });

        var staff = await _staffService.GetStaffByIdAsync(id, branchId.Value);
        if (staff == null)
            return NotFound(new ApiResponse<object>
            {
                Success = false,
                Message = "Nhân viên không tồn tại hoặc không thuộc chi nhánh này."
            });

        return Ok(new ApiResponse<OwnerStaffDto>
        {
            Success = true,
            Message = "Chi tiết nhân viên.",
            Data = staff
        });
    }

    #endregion

    #region POST /api/owner/staffs

    /// <summary>
    /// Tạo nhân viên mới - Tự động gán chi nhánh của Owner.
    /// </summary>
    [HttpPost]
    [HasPermission(PermissionConstants.STAFF_CREATE)]
    public async Task<IActionResult> CreateStaff([FromBody] CreateStaffWithPermissionRequest dto)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Message = "Không thể xác định người dùng từ token."
            });

        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue || branchId == Guid.Empty)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không xác định được chi nhánh hiện tại. Vui lòng chọn chi nhánh."
            });

        var created = await _staffService.CreateStaffAsync(dto, ownerId.Value, branchId.Value);
        return CreatedAtAction(nameof(GetStaffById), new { id = created.UserId },
            new ApiResponse<OwnerStaffDto>
            {
                Success = true,
                Message = "Tạo nhân viên thành công.",
                Data = created
            });
    }

    #endregion

    #region PUT /api/owner/staffs/{id}

    /// <summary>
    /// Cập nhật thông tin nhân viên (Không đổi chi nhánh).
    /// </summary>
    [HttpPut("{id:guid}")]
    [HasPermission(PermissionConstants.STAFF_UPDATE)]
    public async Task<IActionResult> UpdateStaff(Guid id, [FromBody] UpdateStaffPermissionRequest dto)
    {
        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không tìm thấy thông tin chi nhánh."
            });

        await _staffService.UpdateStaffAsync(id, dto, branchId.Value);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Cập nhật nhân viên thành công."
        });
    }

    #endregion

    #region PATCH /api/owner/staffs/{id}/toggle-status
    /// <summary>
    /// Thay đổi trạng thái hoạt động (Bật/Tắt) của tài khoản nhân viên.
    /// </summary>
    [HttpPatch("{id:guid}/toggle-status")]
    [HasPermission(PermissionConstants.STAFF_UPDATE)]
    public async Task<IActionResult> ToggleStaffStatus(Guid id)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không thể xác định người dùng từ token." });

        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Không tìm thấy thông tin chi nhánh." });

        await _staffService.ToggleStaffStatusAsync(id, ownerId.Value, branchId.Value);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Đã thay đổi trạng thái tài khoản nhân viên thành công."
        });
    }
    #endregion


    #region DELETE /api/owner/staffs/{id}
    /// <summary>
    /// Xóa mềm nhân viên (Soft Delete - chuyển Status = DELETED, IsActive = 0).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionConstants.STAFF_DELETE)]
    public async Task<IActionResult> DeleteStaff(Guid id)
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không thể xác định người dùng từ token." });

        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Không tìm thấy thông tin chi nhánh." });

        var (success, message, data) = await _staffService.DeleteStaffAsync(id, ownerId.Value, branchId.Value);
        return Ok(new ApiResponse<object>
        {
            Success = success,
            Message = message,
            Data = data
        });
    }
    #endregion

    #region GET /api/owner/staffs/deleted
    /// <summary>
    /// Lấy danh sách nhân viên đã xóa mềm của chi nhánh hiện tại.
    /// </summary>
    [HttpGet("deleted")]
    [HasPermission(PermissionConstants.STAFF_VIEW)]
    public async Task<IActionResult> GetDeletedStaffs()
    {
        var ownerId = GetCurrentUserId();
        if (!ownerId.HasValue)
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Không thể xác định người dùng từ token." });

        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Không tìm thấy thông tin chi nhánh." });

        var result = await _staffService.GetDeletedStaffsAsync(branchId.Value, ownerId.Value);
        return Ok(new ApiResponse<List<DeletedStaffDto>>
        {
            Success = true,
            Message = "Danh sách nhân viên đã xóa.",
            Data = result
        });
    }
    #endregion

    #region POST /api/owner/staffs/{id}/restore
    /// <summary>
    /// Khôi phục nhân viên đã xóa mềm.
    /// </summary>
    [HttpPost("{id:guid}/restore")]
    [HasPermission(PermissionConstants.STAFF_DELETE)]
    public async Task<IActionResult> RestoreStaff(Guid id)
    {
        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Không tìm thấy thông tin chi nhánh." });

        await _staffService.RestoreStaffAsync(id, branchId.Value);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Đã khôi phục nhân viên thành công."
        });
    }
    #endregion

    #region DELETE /api/owner/staffs/{id}/permanent
    /// <summary>
    /// Xóa vĩnh viễn nhân viên (Anonymization - giữ User record để bảo toàn FK).
    /// Sau khi anonymize, UserId vẫn giữ nguyên nên các hoá đơn/đơn hàng vẫn trỏ đúng.
    /// </summary>
    [HttpDelete("{id:guid}/permanent")]
    [HasPermission(PermissionConstants.STAFF_DELETE)]
    public async Task<IActionResult> PermanentDeleteStaff(Guid id)
    {
        var branchId = GetCurrentBranchId();
        if (!branchId.HasValue)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Không tìm thấy thông tin chi nhánh." });

        await _staffService.PermanentDeleteStaffAsync(id, branchId.Value);
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Đã xóa vĩnh viễn nhân viên. Dữ liệu cá nhân đã được ẩn danh, các hoá đơn vẫn được giữ nguyên."
        });
    }
    #endregion
}