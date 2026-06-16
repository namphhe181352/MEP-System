using System;
using System.Collections.Generic;

namespace MEP.Data.Models;

public partial class StoreApproval
{
    public Guid ApprovalId { get; set; }
    public Guid UserId { get; set; }
    public string StoreName { get; set; } = null!;
    public string? BusinessLicense { get; set; }
    public string? TaxCode { get; set; }
    public string? StoreAddress { get; set; }
    public string? DocumentUrls { get; set; } // JSON array of file URLs
    public string Status { get; set; } = "PENDING"; // "PENDING" / "APPROVED" / "REJECTED"
    public string? RejectReason { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual User? ReviewedByUser { get; set; }
}
