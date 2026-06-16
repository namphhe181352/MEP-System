using System;
using System.Collections.Generic;

namespace MEP.Data.Models;

public partial class CommunityCategory
{
    public Guid CategoryId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsArchived { get; set; } // NEW: soft delete

    public int SortOrder { get; set; } // NEW: ordering

    public virtual ICollection<CommunityPost> CommunityPosts { get; set; } = new List<CommunityPost>();
}
