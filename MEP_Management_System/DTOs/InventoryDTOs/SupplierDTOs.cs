using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.InventoryDTOs
{
    // ============================================================
    // QUERY PARAMETERS
    // ============================================================
    public class SupplierQueryParameters
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public string? SearchTerm { get; set; }

        /// <summary>1 = đang hợp tác, 0 = ngừng, all = tất cả. Mặc định "all".</summary>
        public string? Status { get; set; } = "all";

        /// <summary>Lọc theo nhóm NCC (VLTho / PhuKien / VTHan...).</summary>
        public string? GroupName { get; set; }
    }

    // ============================================================
    // REQUEST - TẠO / SỬA
    // ============================================================
    public class SupplierUpsertRequest
    {
        [Required(ErrorMessage = "Tên nhà cung cấp là bắt buộc")]
        [StringLength(150, MinimumLength = 2)]
        public string SupplierName { get; set; } = null!;

        [StringLength(20)]
        public string? ContactPhone { get; set; }

        [StringLength(150)]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string? ContactEmail { get; set; }

        [StringLength(100)]
        public string? ContactPerson { get; set; }

        [StringLength(50)]
        public string? TaxCode { get; set; }

        [StringLength(50)]
        public string? GroupName { get; set; }

        [StringLength(50)]
        public string? BankAccount { get; set; }

        [StringLength(100)]
        public string? BankName { get; set; }

        [StringLength(255)]
        public string? Website { get; set; }

        [StringLength(255)]
        public string? Address { get; set; }

        public string? Notes { get; set; }

        /// <summary>1 = Active, 0 = Inactive. Mặc định 1.</summary>
        public short Status { get; set; } = 1;
    }

    // ============================================================
    // RESPONSE - MỘT DÒNG TRONG BẢNG
    // ============================================================
    public class SupplierListItemDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? ContactPerson { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? Address { get; set; }
        public string? GroupName { get; set; }
        public string? TaxCode { get; set; }
        public string Status { get; set; } = "active";
        public DateTime? CreatedAt { get; set; }

        // Công nợ & tổng mua (computed từ PO)
        public decimal CurrentDebt { get; set; }
        public decimal TotalPurchased { get; set; }

        // Đánh giá
        public decimal? AverageRating { get; set; }
        public int RatingCount { get; set; }
        public DateTime? LastRatedAt { get; set; }
    }

    public class SupplierDetailDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? ContactPerson { get; set; }
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? TaxCode { get; set; }
        public string? GroupName { get; set; }
        public string? BankAccount { get; set; }
        public string? BankName { get; set; }
        public string? Website { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        public string Status { get; set; } = "active";
        public DateTime? CreatedAt { get; set; }
        public decimal? AverageRating { get; set; }
        public int RatingCount { get; set; }
        public DateTime? LastRatedAt { get; set; }
    }

    public class SupplierPagedResponse
    {
        public List<SupplierListItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public List<string> DistinctGroups { get; set; } = new();
    }
}
