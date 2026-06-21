using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.InventoryDTOs
{
    // ============================================================
    // QUERY PARAMETERS
    // ============================================================
    public class SupplierRatingQueryParameters
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public string? SearchTerm { get; set; }

        /// <summary>Lọc theo NCC cụ thể.</summary>
        public Guid? SupplierId { get; set; }

        /// <summary>Lọc theo điểm tối thiểu (1-5).</summary>
        public short? MinRating { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    // ============================================================
    // REQUEST - TẠO / SỬA ĐÁNH GIÁ
    // ============================================================
    public class SupplierRatingUpsertRequest
    {
        [Required(ErrorMessage = "Nhà cung cấp là bắt buộc")]
        public Guid SupplierId { get; set; }

        public Guid? PurchaseOrderId { get; set; }

        [Range(1, 5, ErrorMessage = "Điểm chất lượng phải từ 1 đến 5")]
        public short QualityScore { get; set; }

        [Range(1, 5, ErrorMessage = "Điểm giao hàng phải từ 1 đến 5")]
        public short DeliveryScore { get; set; }

        [Range(1, 5, ErrorMessage = "Điểm giá cả phải từ 1 đến 5")]
        public short PriceScore { get; set; }

        [Range(1, 5, ErrorMessage = "Điểm dịch vụ phải từ 1 đến 5")]
        public short ServiceScore { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }
    }

    // ============================================================
    // RESPONSE - MỘT LƯỢT ĐÁNH GIÁ
    // ============================================================
    public class SupplierRatingListItemDto
    {
        public Guid Id { get; set; }
        public Guid SupplierId { get; set; }
        public string SupplierCode { get; set; } = null!;
        public string SupplierName { get; set; } = null!;

        public Guid? PurchaseOrderId { get; set; }
        public string? PurchaseOrderCode { get; set; }

        public short QualityScore { get; set; }
        public short DeliveryScore { get; set; }
        public short PriceScore { get; set; }
        public short ServiceScore { get; set; }
        public decimal OverallRating { get; set; }
        public string? Comment { get; set; }

        public Guid? RatedBy { get; set; }
        public string? RatedByName { get; set; }
        public DateTime? RatedAt { get; set; }
    }

    public class SupplierRatingDetailDto
    {
        public Guid Id { get; set; }
        public Guid SupplierId { get; set; }
        public string SupplierName { get; set; } = null!;

        public Guid? PurchaseOrderId { get; set; }
        public string? PurchaseOrderCode { get; set; }
        public decimal? PurchaseOrderTotal { get; set; }

        public short QualityScore { get; set; }
        public short DeliveryScore { get; set; }
        public short PriceScore { get; set; }
        public short ServiceScore { get; set; }
        public decimal OverallRating { get; set; }
        public string? Comment { get; set; }

        public Guid? RatedBy { get; set; }
        public string? RatedByName { get; set; }
        public DateTime? RatedAt { get; set; }
    }

    public class SupplierRatingPagedResponse
    {
        public List<SupplierRatingListItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    // ============================================================
    // SUMMARY - TOP / BOTTOM NCC
    // ============================================================
    public class SupplierRatingSummaryDto
    {
        public int TotalRatings { get; set; }
        public decimal AverageOverallRating { get; set; }
        public int RatedSupplierCount { get; set; }

        /// <summary>Top 5 NCC có điểm cao nhất.</summary>
        public List<SupplierRatingTopDto> TopSuppliers { get; set; } = new();

        /// <summary>Top 5 NCC có điểm thấp nhất (>= 1 lượt đánh giá).</summary>
        public List<SupplierRatingTopDto> BottomSuppliers { get; set; } = new();
    }

    public class SupplierRatingTopDto
    {
        public Guid SupplierId { get; set; }
        public string SupplierName { get; set; } = null!;
        public string? GroupName { get; set; }
        public decimal AverageRating { get; set; }
        public int RatingCount { get; set; }
        public DateTime? LastRatedAt { get; set; }
    }

    // ============================================================
    // RESPONSE – PHÂN BỐ ĐIỂM 1-5 SAO
    // ============================================================
    public class SupplierRatingDistributionDto
    {
        /// <summary>Số lượt đánh giá 1 sao.</summary>
        public int Star1Count { get; set; }

        /// <summary>Số lượt đánh giá 2 sao.</summary>
        public int Star2Count { get; set; }

        /// <summary>Số lượt đánh giá 3 sao.</summary>
        public int Star3Count { get; set; }

        /// <summary>Số lượt đánh giá 4 sao.</summary>
        public int Star4Count { get; set; }

        /// <summary>Số lượt đánh giá 5 sao.</summary>
        public int Star5Count { get; set; }

        /// <summary>Tổng số lượt đánh giá.</summary>
        public int TotalCount { get; set; }

        /// <summary>Điểm trung bình toàn hệ thống.</summary>
        public decimal AverageOverall { get; set; }

        /// <summary>Tỷ lệ % mỗi mức sao (1-5).</summary>
        public List<decimal> StarPercentages { get; set; } = new();
    }

    // ============================================================
    // RESPONSE – TĂNG TRƯỞNG ĐÁNH GIÁ THEO THÁNG
    // ============================================================
    public class SupplierRatingGrowthPointDto
    {
        /// <summary>Năm-Tháng (VD: "2026-01").</summary>
        public string Period { get; set; } = null!;

        public int Year { get; set; }
        public int Month { get; set; }

        /// <summary>Số lượt đánh giá trong tháng.</summary>
        public int Count { get; set; }

        /// <summary>Điểm trung bình trong tháng.</summary>
        public decimal AverageRating { get; set; }
    }

    public class SupplierRatingGrowthDto
    {
        public List<SupplierRatingGrowthPointDto> MonthlyPoints { get; set; } = new();
        public int TotalRatings { get; set; }
        public decimal GrowthRatePercent { get; set; } // So với tháng trước
    }

    // ============================================================
    // RESPONSE – KIỂM TRA PO ĐÃ ĐƯỢC ĐÁNH GIÁ CHƯA
    // ============================================================
    public class SupplierRatingCheckDto
    {
        public Guid PurchaseOrderId { get; set; }
        public string? PurchaseOrderCode { get; set; }
        public bool AlreadyRated { get; set; }
        public Guid? ExistingRatingId { get; set; }
        public short? ExistingOverallRating { get; set; }
        public DateTime? ExistingRatedAt { get; set; }
    }

    // ============================================================
    // RESPONSE – LỊCH SỬ ĐÁNH GIÁ CỦA 1 USER
    // ============================================================
    public class SupplierRatingByUserDto
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = null!;
        public int TotalRatings { get; set; }
        public decimal? AverageOverall { get; set; }
        public DateTime? FirstRatedAt { get; set; }
        public DateTime? LastRatedAt { get; set; }
        public List<SupplierRatingListItemDto> Items { get; set; } = new();
    }

    // ============================================================
    // REQUEST – ĐÁNH GIÁ NHIỀU TIÊU CHÍ CÙNG LÚC (BULK)
    // ============================================================
    public class SupplierRatingBulkRequest
    {
        [Required(ErrorMessage = "Danh sách đánh giá là bắt buộc")]
        [MinLength(1, ErrorMessage = "Phải có ít nhất 1 đánh giá")]
        public List<SupplierRatingUpsertRequest> Ratings { get; set; } = new();
    }

    public class SupplierRatingBulkResultDto
    {
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<SupplierRatingDetailDto> CreatedRatings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }
}
