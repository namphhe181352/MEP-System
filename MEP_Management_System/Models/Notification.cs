using System;

namespace MEP_Management_System.Models;

/// <summary>
/// Bảng thông báo người dùng trong hệ thống
/// </summary>
public class Notification
{
    public Guid NotificationId { get; set; }

    /// <summary>
    /// Loại thông báo: Assigned, Submitted, Approved, RecountRequested, Created
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// ID người nhận thông báo. Null = broadcast cho cả chi nhánh
    /// </summary>
    public Guid? RecipientUserId { get; set; }

    /// <summary>
    /// ID chi nhánh liên quan
    /// </summary>
    public Guid BranchId { get; set; }

    /// <summary>
    /// ID phiếu kiểm kê liên quan (nếu có)
    /// </summary>
    public Guid? InventoryCheckId { get; set; }

    /// <summary>
    /// ID người tạo ra thông báo này
    /// </summary>
    public Guid TriggeredByUserId { get; set; }

    /// <summary>
    /// Nội dung thông báo
    /// </summary>
    public string Message { get; set; } = null!;

    /// <summary>
    /// Đã đọc chưa
    /// </summary>
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// Thời gian tạo
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual User? RecipientUser { get; set; }
    public virtual User? TriggeredByUser { get; set; }
    public virtual InventoryCheckTicket? InventoryCheck { get; set; }
    public virtual Branch Branch { get; set; } = null!;
}

/// <summary>
/// Enum loại thông báo
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Thông báo khi được giao kiểm kê
    /// </summary>
    Assigned = 1,

    /// <summary>
    /// Thông báo khi phiếu được gửi duyệt (submit/fill)
    /// </summary>
    Submitted = 2,

    /// <summary>
    /// Thông báo khi phiếu được duyệt (approve)
    /// </summary>
    Approved = 3,

    /// <summary>
    /// Thông báo khi yêu cầu đếm lại (reject/request recount)
    /// </summary>
    RecountRequested = 4,

    /// <summary>
    /// Thông báo khi phiếu được tạo mới
    /// </summary>
    Created = 5
}
