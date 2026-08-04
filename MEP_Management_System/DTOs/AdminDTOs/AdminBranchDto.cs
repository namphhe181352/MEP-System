using System;
using System.ComponentModel.DataAnnotations;

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
        [Required(ErrorMessage = "Tên cửa hàng là bắt buộc.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên cửa hàng phải có từ 2 đến 100 ký tự.")]
        [RegularExpression(@"^[\p{L}0-9\s\.\-_&/]+$",
            ErrorMessage = "Tên cửa hàng chỉ được chứa chữ cái, số, khoảng trắng và các ký tự: . - _ & /")]
        public string BranchName { get; set; } = null!;

        [StringLength(20, ErrorMessage = "Mã chi nhánh không được vượt quá 20 ký tự.")]
        public string? BranchCode { get; set; }

        [Required(ErrorMessage = "Số điện thoại là bắt buộc.")]
        [RegularExpression(@"^(03|05|07|08|09)\d{8}$", ErrorMessage = "Số điện thoại phải là số Việt Nam hợp lệ (10 số, bắt đầu bằng 03/05/07/08/09).")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Thành phố là bắt buộc.")]
        [StringLength(100, ErrorMessage = "Thành phố không được vượt quá 100 ký tự.")]
        public string? City { get; set; }

        [Required(ErrorMessage = "Địa chỉ là bắt buộc.")]
        [MinLength(5, ErrorMessage = "Địa chỉ phải có ít nhất 5 ký tự.")]
        [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự.")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Loại cửa hàng là bắt buộc.")]
        [StringLength(50, ErrorMessage = "Loại cửa hàng không được vượt quá 50 ký tự.")]
        public string? Type { get; set; }

        [Required(ErrorMessage = "Chủ cửa hàng là bắt buộc.")]
        public Guid? ManagerUserId { get; set; }
    }

    public class UpdateAdminBranchRequest
    {
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên cửa hàng phải có từ 2 đến 100 ký tự.")]
        [RegularExpression(@"^[\p{L}0-9\s\.\-_&/]+$",
            ErrorMessage = "Tên cửa hàng chỉ được chứa chữ cái, số, khoảng trắng và các ký tự: . - _ & /")]
        public string? BranchName { get; set; }

        [StringLength(20, ErrorMessage = "Mã chi nhánh không được vượt quá 20 ký tự.")]
        public string? BranchCode { get; set; }

        [RegularExpression(@"^(03|05|07|08|09)\d{8}$", ErrorMessage = "Số điện thoại phải là số Việt Nam hợp lệ (10 số, bắt đầu bằng 03/05/07/08/09).")]
        public string? Phone { get; set; }

        [StringLength(100, ErrorMessage = "Thành phố không được vượt quá 100 ký tự.")]
        public string? City { get; set; }

        [MinLength(5, ErrorMessage = "Địa chỉ phải có ít nhất 5 ký tự.")]
        [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự.")]
        public string? Address { get; set; }

        [StringLength(50, ErrorMessage = "Loại cửa hàng không được vượt quá 50 ký tự.")]
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
