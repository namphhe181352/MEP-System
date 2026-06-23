using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class ShiftDto
{
    public Guid ShiftId { get; set; }
    public string ShiftCode { get; set; } = null!;
    public Guid UserId { get; set; }
    public string? UserName { get; set; }
    public Guid BranchId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? ActualCash { get; set; }
    public decimal? Variance { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public string? Note { get; set; }
}
