namespace MEP.Admin.Api.DTOs;

public class StoreApprovalDto
{
    public Guid ApprovalId { get; set; }
    public string StoreName { get; set; } = null!;
    public string? BusinessLicense { get; set; }
    public string? TaxCode { get; set; }
    public string? StoreAddress { get; set; }
    public List<string> DocumentUrls { get; set; } = new();
    public string Status { get; set; } = null!;
    public string? RejectReason { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = null!;
    public string UserFullName { get; set; } = null!;
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
