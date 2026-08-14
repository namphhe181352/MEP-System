using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class ShiftSummaryDto
{
    public Guid ShiftId { get; set; }
    public string ShiftCode { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalCash { get; set; }
    public decimal TotalTransfer { get; set; }
    public decimal TotalDebt { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal? ActualCash { get; set; }
    public decimal? Variance { get; set; }
    public int TotalOrders { get; set; }
    public int ReturnCount { get; set; }
    public int TotalRefunds { get; set; }
    public Guid OpenedByUserId { get; set; }
    public string? OpenedByUserName { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? ClosedByUserName { get; set; }
    public string? ForceCloseReason { get; set; }
    public string? Note { get; set; }
    public List<PaymentMethodSummary> PaymentBreakdown { get; set; } = new();
    public List<UserSalesSummary> SalesByUser { get; set; } = new();
    /// <summary>Danh sách từng hành động trong ca (Bán/Hoàn/Đổi) — dùng cho Activity log.</summary>
    public List<ShiftActivityDto> Activities { get; set; } = new();

    public class PaymentMethodSummary
    {
        public string Method { get; set; } = null!;
        public decimal Total { get; set; }
        public int Count { get; set; }
    }

    public class UserSalesSummary
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = null!;
        public int InvoiceCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    /// <summary>1 hành động trong ca: bán / hoàn tiền / đổi hàng chênh lệch.</summary>
    public class ShiftActivityDto
    {
        /// <summary>Loại: Sale | Refund | Exchange.</summary>
        public string Type { get; set; } = null!;
        /// <summary>Mã phiếu (mã hóa đơn / mã trả hàng).</summary>
        public string? Code { get; set; }
        /// <summary>Số tiền có dấu: + cho bán/thu thêm, - cho hoàn tiền, 0 = đổi ngang giá.</summary>
        public decimal Amount { get; set; }
        /// <summary>Phương thức: Cash/Transfer.</summary>
        public string? Method { get; set; }
        /// <summary>Tên khách (nếu có).</summary>
        public string? CustomerName { get; set; }
        /// <summary>NV thực hiện.</summary>
        public string? UserName { get; set; }
        /// <summary>Mô tả thêm (vd đổi hàng: "SP A → SP B").</summary>
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
