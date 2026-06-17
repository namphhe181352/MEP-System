using System;
using System.Collections.Generic;

namespace MEP.Data.Models;

public partial class SystemLog
{
    public Guid LogId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Tên hành động: CREATE_PRODUCT / UPDATE_STOCK / LOGIN / ...</summary>
    public string Action { get; set; } = null!;

    /// <summary>Tên bảng bị tác động (audit trail).</summary>
    public string TableName { get; set; } = null!;

    /// <summary>Loại entity (VD: Product, Invoice) - cho phép query nhanh hơn.</summary>
    public string? EntityType { get; set; }

    /// <summary>ID của row bị tác động.</summary>
    public Guid? EntityId { get; set; }

    public string? Description { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? Level { get; set; } // INFO / WARN / ERROR

    public string? Source { get; set; } // AuthService, Database, NotificationWorker, ...

    public DateTime? Timestamp { get; set; }

    public virtual User User { get; set; } = null!;
}
