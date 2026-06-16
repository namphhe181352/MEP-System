using System;
using System.Collections.Generic;

namespace MEP.Data.Models;

public partial class ViolationReport
{
    public Guid ReportId { get; set; }

    public Guid? PostId { get; set; }

    public Guid? CommentId { get; set; }

    /// <summary>User đã gửi report (theo SRS mô tả "reporter_id").</summary>
    public Guid UserId { get; set; }

    public string Reason { get; set; } = null!;

    /// <summary>Trạng thái: PENDING / REVIEWED / RESOLVED / REJECTED.</summary>
    public string? Status { get; set; }

    /// <summary>Admin xử lý report (theo UC Handle Violation Report).</summary>
    public Guid? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? AdminNote { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Comment? Comment { get; set; }

    public virtual CommunityPost? Post { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual User? ResolvedByUser { get; set; }
}
