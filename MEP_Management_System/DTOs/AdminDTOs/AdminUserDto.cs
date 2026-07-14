using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.AdminDTOs
{
    public class AdminUserDto
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public short? IsActive { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public List<AdminRoleDto> Roles { get; set; } = new List<AdminRoleDto>();
    }

    public class AdminUserDetailDto : AdminUserDto
    {
        public bool IsVerified { get; set; }
        public DateTime? SuspendedUntil { get; set; }
        public string? SubscriptionPlan { get; set; }
    }

    public class AdminUserCreateDto
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

        public List<Guid>? RoleIds { get; set; }
    }

    public class AdminUserUpdateDto
    {
        [Required]
        public string FullName { get; set; } = null!;
        
        public string? PhoneNumber { get; set; }
    }

    public class AdminUserRoleUpdateDto
    {
        [Required]
        public List<Guid> RoleIds { get; set; } = new List<Guid>();
    }

    public class AdminUserStatusUpdateDto
    {
        [Required]
        public bool IsActive { get; set; }
    }
    
    public class AdminUserResetPasswordDto
    {
        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string NewPassword { get; set; } = null!;
    }

    public class AdminRoleDto
    {
        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = null!;
        public string? Description { get; set; }
    }

    public class AdminPermissionDto
    {
        public Guid PermissionId { get; set; }
        public string PermissionCode { get; set; } = null!;
        public string PermissionName { get; set; } = null!;
    }

    public class AdminRolePermissionUpdateDto
    {
        [Required]
        public List<Guid> PermissionIds { get; set; } = new List<Guid>();
    }
}
