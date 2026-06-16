using System;
using System.Collections.Generic;

namespace MEP.Data.Models;

public partial class Customer
{
    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = null!;

    /// <summary>Số điện thoại KH (UNIQUE theo SRS BR-82).</summary>
    public string? PhoneNumber { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public int? LoyaltyPoints { get; set; }

    /// <summary>Hạn mức nợ tối đa (theo UC Configure Customer credit limit).</summary>
    public decimal? DebtLimit { get; set; }

    /// <summary>Dư nợ hiện tại (AP/AR tracking).</summary>
    public decimal? CurrentDebt { get; set; }

    /// <summary>Trạng thái: 1=Active, 0=Blocked. Mặc định 1.</summary>
    public short? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
