using System;

namespace MEP_Management_System.DTOs.AdminDTOs
{
    public class AdminBranchDto
    {
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = null!;
        public string? BranchCode { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Type { get; set; }
        public Guid? ManagerUserId { get; set; }
        public string? ManagerFullName { get; set; }
        public string? ManagerEmail { get; set; }
        public short IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateAdminBranchRequest
    {
        public string BranchName { get; set; } = null!;
        public string? BranchCode { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Type { get; set; }
        public Guid? ManagerUserId { get; set; }
    }

    public class UpdateAdminBranchRequest
    {
        public string? BranchName { get; set; }
        public string? BranchCode { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Type { get; set; }
        public Guid? ManagerUserId { get; set; }
        public short? IsActive { get; set; }
    }

    public class AssignUserToBranchRequest
    {
        public Guid UserId { get; set; }
        public bool IsManager { get; set; }
    }

    public class BranchUserDto
    {
        public Guid StaffAssignmentId { get; set; }
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public short IsManager { get; set; }
        public DateTime? AssignedAt { get; set; }
    }
}
