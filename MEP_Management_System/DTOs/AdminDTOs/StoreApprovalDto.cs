namespace MEP_Management_System.DTOs.AdminDTOs;

public class StoreApprovalDto
{
    public Guid ApprovalId { get; set; }
    public Guid UserId { get; set; }
    public string StoreName { get; set; } = null!;
    public string? BusinessLicense { get; set; }
    public string? TaxCode { get; set; }
    public string? StoreAddress { get; set; }
    public string? DocumentUrls { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? RejectReason { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public string OwnerEmail { get; set; } = null!;
    public string OwnerName { get; set; } = null!;
    public string? ReviewerName { get; set; }
}

public class ApproveStoreRequest
{
    public string? Notes { get; set; }
}

public class RejectStoreRequest
{
    public string Reason { get; set; } = null!;
}
