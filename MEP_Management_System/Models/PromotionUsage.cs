namespace MEP_Management_System.Models;

public partial class PromotionUsage
{
    public Guid UsageId { get; set; }

    public Guid PromotionId { get; set; }

    public Guid? InvoiceId { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid? UserId { get; set; }

    public decimal DiscountAmount { get; set; }

    public DateTime UsedAt { get; set; } = DateTime.UtcNow;

    public virtual Promotion Promotion { get; set; } = null!;

    public virtual Invoice? Invoice { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual User? User { get; set; }
}
