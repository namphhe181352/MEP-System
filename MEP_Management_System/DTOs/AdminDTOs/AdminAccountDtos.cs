using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.AdminDTOs;

/// <summary>
/// Response DTO chi tiết cho Admin xem một tài khoản.
/// Áp dụng cho Owner + Staff (không dùng cho Admin).
/// </summary>
public class AdminAccountDetailDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public short? IsActive { get; set; }
    public string? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? SuspendedUntil { get; set; }
    public bool IsVerified { get; set; }
    public string? SubscriptionPlan { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? DefaultBranchId { get; set; }
    public List<AdminRoleDto> Roles { get; set; } = new();
    public List<string> PermissionCodes { get; set; } = new();
}

/// <summary>
/// Request tạo Owner mới — Admin phụ trách.
/// </summary>
public class AdminCreateOwnerRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string FullName { get; set; } = null!;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    /// <summary>
    /// BranchId gán cho Owner. Nếu null, tự tạo Branch mới.
    /// </summary>
    public Guid? BranchId { get; set; }

    /// <summary>
    /// Tên branch khi tự tạo (chỉ dùng khi BranchId == null). Mặc định: "{FullName} Store".
    /// </summary>
    public string? BranchName { get; set; }

    /// <summary>
    /// BASIC | PREMIUM | ENTERPRISE
    /// </summary>
    public string? SubscriptionPlan { get; set; }
}

/// <summary>
/// Request tạo Staff mới — Admin phụ trách.
/// Email phải có domain @system.local (BR-44).
/// </summary>
public class AdminCreateStaffRequest
{
    [Required]
    public string Username { get; set; } = null!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string FullName { get; set; } = null!;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    /// <summary>
    /// BranchId gán cho Staff. Nếu null, lấy branch active đầu tiên.
    /// </summary>
    public Guid? BranchId { get; set; }

    /// <summary>
    /// Tên role: SalesStaff | InventoryStaff | Staff
    /// </summary>
    [Required]
    public string RoleName { get; set; } = null!;

    /// <summary>
    /// Mã permission trực tiếp cho user. Nếu null thì dùng permission mặc định của role.
    /// </summary>
    public List<string>? PermissionCodes { get; set; }
}

/// <summary>
/// Request cập nhật thông tin tài khoản.
/// </summary>
public class AdminUpdateAccountRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? SubscriptionPlan { get; set; }
}

/// <summary>
/// Request gán roles cho user.
/// </summary>
public class AdminAssignRolesRequest
{
    [Required]
    public List<Guid> RoleIds { get; set; } = new();
}

/// <summary>
/// Request gán permissions trực tiếp cho user.
/// </summary>
public class AdminAssignPermissionsRequest
{
    [Required]
    public List<string> PermissionCodes { get; set; } = new();
}

/// <summary>
/// Request gán/đổi chi nhánh cho user.
/// </summary>
public class AdminAssignBranchRequest
{
    [Required]
    public Guid BranchId { get; set; }
}

/// <summary>
/// Request đổi trạng thái hoạt động.
/// </summary>
public class AdminToggleStatusRequest
{
    [Required]
    public bool IsActive { get; set; }
}

/// <summary>
/// Request reset mật khẩu.
/// </summary>
public class AdminResetPasswordRequest
{
    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string NewPassword { get; set; } = null!;
}

/// <summary>
/// Response check-relations trước khi xóa user.
/// </summary>
public class AdminAccountRelationsDto
{
    public bool HasRelations { get; set; }
    public int InvoiceCount { get; set; }
    public int OrderCount { get; set; }
    public int ReturnOrderCount { get; set; }
    public int PurchaseOrderCount { get; set; }
    public int ShiftCount { get; set; }
    public int StockTicketCreatedCount { get; set; }
    public int StockTicketConfirmedCount { get; set; }
    public int StockTicketCancelledCount { get; set; }
    public string Message { get; set; } = "";
}
