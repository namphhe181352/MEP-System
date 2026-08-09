using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.InventoryDTOs
{
    #region Expense Voucher DTOs

    /// <summary>
    /// Request body cho API tạo phiếu chi tiền (chỉ chi phí nội bộ - KHÔNG thanh toán NCC).
    /// Để thanh toán NCC, dùng API /api/supplierpayments.
    /// </summary>
    public class ExpenseVoucherCreateDto
    {
        /// <summary>Nhóm chi phí (bắt buộc).</summary>
        public Guid CategoryId { get; set; }

        /// <summary>Số tiền chi (phải > 0).</summary>
        public decimal Amount { get; set; }

        /// <summary>Lý do chi (bắt buộc).</summary>
        public string Reason { get; set; } = null!;

        /// <summary>Ghi chú thêm (nullable).</summary>
        public string? Note { get; set; }
    }

    /// <summary>
    /// Response đầy đủ cho phiếu chi tiền (bao gồm audit trail).
    /// </summary>
    public class ExpenseVoucherResponseDto
    {
        public Guid VoucherId { get; set; }
        public string VoucherCode { get; set; } = null!;
        public Guid BranchId { get; set; }
        public string? BranchName { get; set; }

        public Guid? CategoryId { get; set; }
        public string? CategoryName { get; set; }

        public Guid? SupplierId { get; set; }
        public string? SupplierName { get; set; }

        public decimal Amount { get; set; }
        public string Reason { get; set; } = null!;
        public string? Note { get; set; }

        public string Status { get; set; } = "PENDING";

        // Audit trail
        public Guid CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? CreatedAt { get; set; }

        public Guid? ConfirmedBy { get; set; }
        public string? ConfirmedByName { get; set; }
        public DateTime? CompletedAt { get; set; }

        public Guid? CancelledBy { get; set; }
        public string? CancelledByName { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancelReason { get; set; }

        /// <summary>Frontend dùng để ẩn/hiện nút Confirm.</summary>
        public bool CanConfirm { get; set; }

        /// <summary>Frontend dùng để ẩn/hiện nút Cancel.</summary>
        public bool CanCancel { get; set; }
    }

    /// <summary>
    /// Query parameters cho GET list phiếu chi.
    /// </summary>
    public class ExpenseVoucherParameters
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public Guid? CategoryId { get; set; }
        public Guid? SupplierId { get; set; }
        public string? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Sort { get; set; }
        public string? Order { get; set; }
    }

    #endregion

    #region Expense Category DTOs

    /// <summary>
    /// DTO cho danh sách nhóm chi phí (kèm số phiếu đang dùng).
    /// </summary>
    public class ExpenseCategoryDto
    {
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;
        public short IsActive { get; set; }
        public int VoucherCount { get; set; }
        public int PendingCount { get; set; }
    }

    /// <summary>
    /// Request body cho tạo/sửa nhóm chi phí.
    /// </summary>
    public class ExpenseCategoryUpsertDto
    {
        public string CategoryName { get; set; } = null!;
    }

    #endregion

    #region Cancel Expense Voucher

    /// <summary>
    /// Request body cho API cập nhật phiếu chi (chỉ PENDING).
    /// </summary>
    public class UpdateExpenseVoucherDto
    {
        public string? Reason { get; set; }
        public decimal? Amount { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>
    /// Request body cho API hủy phiếu chi.
    /// </summary>
    public class CancelExpenseVoucherDto
    {
        /// <summary>Lý do hủy phiếu (bắt buộc).</summary>
        public string CancelReason { get; set; } = null!;
    }

    #endregion
}
