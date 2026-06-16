using System;
using System.Collections.Generic;

namespace MEP.Data.Models;

public partial class CommunityPost
{
    public Guid PostId { get; set; }

    public Guid CategoryId { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    /// <summary>Trạng thái: ACTIVE / LOCKED / HIDDEN.</summary>
    public string? Status { get; set; }

    /// <summary>Đã ghim lên đầu category (theo UC Pin Post).</summary>
    public short? IsPinned { get; set; }

    public int? ViewCount { get; set; }

    /// <summary>Số lượng tối thiểu để mở Group Buying (theo UC Group Buying).</summary>
    public int? GroupBuyingTarget { get; set; }

    /// <summary>Đơn giá Group Buying (nếu là bài gom đơn).</summary>
    public decimal? GroupBuyingPrice { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual CommunityCategory Category { get; set; } = null!;

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual ICollection<MediaResource> MediaResources { get; set; } = new List<MediaResource>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<ViolationReport> ViolationReports { get; set; } = new List<ViolationReport>();
}
