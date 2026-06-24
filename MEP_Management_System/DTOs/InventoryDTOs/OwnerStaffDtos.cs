using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.InventoryDTOs;

public class OwnerStaffDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public short? IsActive { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? PasswordHash { get; set; }
    public List<string> PermissionCodes { get; set; } = new();
}
public class AssignStaffBranchRequest
{
    public Guid BranchId { get; set; }
}
public class CreateStaffWithPermissionRequest
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public Guid? BranchId { get; set; }  // optional
    public string? DefaultRoleType { get; set; }
    public List<string>? CustomPermissionCodes { get; set; }
}

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
