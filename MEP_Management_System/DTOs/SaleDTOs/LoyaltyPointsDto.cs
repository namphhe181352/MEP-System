using System;
using System.Collections.Generic;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class LoyaltyPointsDto
{
    public Guid CustomerId { get; set; }
    public int CurrentPoints { get; set; }
    public string? MembershipTier { get; set; }
    public decimal? TotalSpent { get; set; }
    public int? OrderCount { get; set; }
    public DateTime? LastVisit { get; set; }
    public List<LoyaltyHistoryDto> RecentTransactions { get; set; } = new();

    public class LoyaltyHistoryDto
    {
        public Guid TransactionId { get; set; }
        public int Points { get; set; }
        public string TransactionType { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
