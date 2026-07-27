using System;
using System.Collections.Generic;

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
}
