using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.InventoryDTOs
{
    public class OwnerBranchDto
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

    public class CreateOwnerBranchRequest
    {
        [Required(ErrorMessage = "Tên cửa hàng là bắt buộc.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên cửa hàng phải có từ 2 đến 100 ký tự.")]
        [RegularExpression(@"^[\p{L}0-9\s\.\-_&/]+$",
            ErrorMessage = "Tên cửa hàng chỉ được chứa chữ cái, số, khoảng trắng và các ký tự: . - _ & /")]
        public string BranchName { get; set; } = null!;
        public string? BranchCode { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Type { get; set; }
        // bỏ ManagerUserId — lấy từ JWT luôn
    }

    public class UpdateOwnerBranchRequest
    {
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Tên cửa hàng phải có từ 2 đến 100 ký tự.")]
        [RegularExpression(@"^[\p{L}0-9\s\.\-_&/]+$",
            ErrorMessage = "Tên cửa hàng chỉ được chứa chữ cái, số, khoảng trắng và các ký tự: . - _ & /")]
        public string? BranchName { get; set; }
        public string? BranchCode { get; set; }
        public string? Phone { get; set; }
        public string? City { get; set; }
        public string? Address { get; set; }
        public string? Type { get; set; }
        public Guid? ManagerUserId { get; set; }
        public short? IsActive { get; set; }
    }

    public class OwnerBranchHistoryDto
    {
        public Guid StockTicketId { get; set; }
        public string? TicketCode { get; set; }
        public string? TicketType { get; set; }
        public string? Status { get; set; }
        public string? Notes { get; set; }
        public DateTime? CreatedAt { get; set; }
        public Guid UserId { get; set; }
        public string? UserFullName { get; set; }
        public string? UserEmail { get; set; }
        public int ItemCount { get; set; }
    }

    public class BranchSettingsDto
    {
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = null!;
        public int? ReturnDaysAllowed { get; set; }
        public int? ExchangeDaysAllowed { get; set; }
    }

    public class UpdateBranchSettingsRequest
    {
        public int? ReturnDaysAllowed { get; set; }
        public int? ExchangeDaysAllowed { get; set; }
    }

    public class CategoryPolicyDto
    {
        /// <summary>ID của nhóm hàng (ProductTaxonomy.TaxonomyId). Null cho dữ liệu legacy.</summary>
        public Guid? CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;
        public int? ReturnDays { get; set; }
        public int? ExchangeDays { get; set; }
    }

    public class UpdateCategoryPoliciesRequest
    {
        public List<CategoryPolicyDto> Policies { get; set; } = new();
    }

    /// <summary>Chiết khấu trả hàng branch-level (%) — trừ vào tiền hoàn lại, áp dụng cho mọi nhóm hàng.</summary>
    public class UpdateReturnDiscountRequest
    {
        public decimal? ReturnDiscountPercent { get; set; }
    }
}
