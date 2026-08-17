using System;

namespace MEP_Management_System.Models;

/// <summary>File đính kèm của phiếu xuất kho nội bộ.</summary>
public partial class GoodsIssueAttachment
{
    public Guid GoodsIssueAttachmentId { get; set; }

    public Guid GoodsIssueId { get; set; }

    public string FileName { get; set; } = "";

    public string? ContentType { get; set; }

    public long? Size { get; set; }

    /// <summary>Đường dẫn/URL tới file đã lưu.</summary>
    public string MediaUrl { get; set; } = "";

    public DateTime? UploadedAt { get; set; }

    public Guid? UploadedBy { get; set; }

    public virtual GoodsIssue GoodsIssue { get; set; } = null!;
}