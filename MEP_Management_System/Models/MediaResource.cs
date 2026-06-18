using System;
using System.Collections.Generic;

namespace MEP_Management_System.Models;

public partial class MediaResource
{
    public Guid MediaId { get; set; }

    public Guid? ProductId { get; set; }

    public Guid? PostId { get; set; }

    public string MediaUrl { get; set; } = null!;

    public string MediaType { get; set; } = null!;

    public short? IsPrimary { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual CommunityPost? Post { get; set; }

    public virtual Product? Product { get; set; }
}
