namespace MEP_Management_System.Models;

public partial class LoyaltyTransaction
{
    public Guid TransactionId { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>Positive = earn, Negative = redeem</summary>
    public int Points { get; set; }

    /// <summary>EARN | REDEEM | ADJUST | EXPIRE</summary>
    public string TransactionType { get; set; } = null!;

    /// <summary>Order | Refund | Manual | Expiry</summary>
    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Customer Customer { get; set; } = null!;
}
