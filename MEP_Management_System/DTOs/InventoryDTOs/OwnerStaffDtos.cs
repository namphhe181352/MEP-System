using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.InventoryDTOs;

/// <summary>
/// DTO trả về thông tin nhân viên (KHÔNG bao gồm PasswordHash)
/// </summary>
public class OwnerStaffDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public short? IsActive { get; set; }
    public List<string> PermissionCodes { get; set; } = new();
    public bool HasCustomPermissions { get; set; }
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// Request gán chi nhánh cho nhân viên
/// </summary>
public class AssignStaffBranchRequest
{
    public Guid BranchId { get; set; }
}

/// <summary>
/// Request tạo nhân viên mới với permissions
/// </summary>
public class CreateStaffWithPermissionRequest
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public Guid? BranchId { get; set; }
    public string? DefaultRoleType { get; set; }
    public List<string>? CustomPermissionCodes { get; set; }
}

/// <summary>
/// Request cập nhật nhân viên
/// </summary>
public class UpdateStaffPermissionRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid? BranchId { get; set; }
    public List<string>? PermissionCodes { get; set; }
    public short? IsActive { get; set; }
    public string? Password { get; set; }
}

/// <summary>
/// DTO cho danh sách staff đã xóa
/// </summary>
public class DeletedStaffDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int DaysUntilPermanentDelete { get; set; }
}
