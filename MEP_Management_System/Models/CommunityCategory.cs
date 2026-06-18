using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class CommunityCategory
{
    public Guid CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsArchived { get; set; } // NEW: soft delete

    public int SortOrder { get; set; } // NEW: ordering

    public string? Slug { get; set; } // NEW: URL-friendly, unique

    public Guid? ParentId { get; set; } // NEW: hierarchical category tree

    public virtual CommunityCategory? Parent { get; set; }

    public virtual ICollection<CommunityCategory> Children { get; set; } = new List<CommunityCategory>();

    public virtual ICollection<CommunityPost> CommunityPosts { get; set; } = new List<CommunityPost>();
}
