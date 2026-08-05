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
    public int TotalRefunds { get; set; }
    public Guid OpenedByUserId { get; set; }
    public string? OpenedByUserName { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? ClosedByUserName { get; set; }
    public string? ForceCloseReason { get; set; }
    public List<PaymentMethodSummary> PaymentBreakdown { get; set; } = new();
    public List<UserSalesSummary> SalesByUser { get; set; } = new();

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
}
