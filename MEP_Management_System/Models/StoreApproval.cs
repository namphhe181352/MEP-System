using System;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.Models;

/// <summary>
/// Yêu cầu đăng ký / mở cửa hàng (Partner Owner & giấy tờ ĐKKD) chờ Admin xét duyệt.
/// </summary>
public partial class StoreApproval
{
    [Key]
    public Guid ApprovalId { get; set; }

    /// <summary>Owner User phụ trách cửa hàng này.</summary>
    public Guid UserId { get; set; }

    public string StoreName { get; set; } = "";

    public string? BusinessLicense { get; set; }

    public string? TaxCode { get; set; }

    public string? StoreAddress { get; set; }

    /// <summary>JSON array hoặc CSV chứa URL các giấy tờ đính kèm.</summary>
    public string? DocumentUrls { get; set; }

    /// <summary>PENDING / APPROVED / REJECTED.</summary>
    public string Status { get; set; } = "PENDING";

    /// <summary>Lý do từ chối (bắt buộc khi REJECTED).</summary>
    public string? RejectReason { get; set; }

    /// <summary>Ghi chú duyệt (khi APPROVED).</summary>
    public string? Notes { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}