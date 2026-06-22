using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class CustomerDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Group { get; set; }
    public string? Notes { get; set; }
    public int LoyaltyPoints { get; set; }
    public decimal? DebtLimit { get; set; }
    public decimal? CurrentDebt { get; set; }
    public decimal? TotalSpent { get; set; }
    public int? OrderCount { get; set; }
    public DateTime? LastVisit { get; set; }
    public string? MembershipTier { get; set; }
    public short Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
