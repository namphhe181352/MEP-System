using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MEP_Management_System.Services.UserServices;

/// <summary>
/// Interface cho UserService - xử lý logic nghiệp vụ liên quan đến Users.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Đổi default branch cho user hiện tại.
    /// </summary>
    Task<(bool Success, string Message, Guid? DefaultBranchId)> ChangeDefaultBranchAsync(Guid userId, Guid branchId);

    /// <summary>
    /// Lấy danh sách thông báo hệ thống cho user.
    /// </summary>
    Task<List<SystemNotificationDto>> GetSystemNotificationsAsync(Guid userId, bool isOwner, bool isStaff, int days = 7);
}

/// <summary>
/// DTO cho thông báo hệ thống.
/// </summary>
public class SystemNotificationDto
{
    public Guid NotificationId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public bool IsUrgent { get; set; }
    public string Target { get; set; } = null!;
    public DateTime? SentAt { get; set; }
    public string Type { get; set; } = "System";
}
