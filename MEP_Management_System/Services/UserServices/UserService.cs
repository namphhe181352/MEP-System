using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MEP_Management_System.Models;
using MEP_Management_System.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.UserServices;

/// <summary>
/// Service xử lý logic nghiệp vụ liên quan đến Users.
/// </summary>
public class UserService : IUserService
{
    private readonly MEPDatabaseContext _context;

    public UserService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string Message, Guid? DefaultBranchId)> ChangeDefaultBranchAsync(Guid userId, Guid branchId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            throw new KeyNotFoundException("Không tìm thấy người dùng");

        // Verify user has access to this branch
        var hasAccess = await _context.StaffAssignments
            .AnyAsync(sa => sa.UserId == userId && sa.BranchId == branchId);

        if (!hasAccess)
            throw new BusinessException("Bạn không có quyền truy cập chi nhánh này");

        user.DefaultBranchId = branchId;
        await _context.SaveChangesAsync();

        return (true, "Default branch changed", branchId);
    }

    public async Task<List<SystemNotificationDto>> GetSystemNotificationsAsync(Guid userId, bool isOwner, bool isStaff, int days = 7)
    {
        var validTargets = new List<string> { "ALL" };
        if (isOwner) validTargets.Add("OWNER");
        if (isStaff) validTargets.Add("STAFF");

        var cutoff = DateTime.UtcNow.AddDays(-days);

        var notifications = await _context.SystemNotifications
            .Where(n => n.Status == "SENT" && n.SentAt != null && n.SentAt >= cutoff)
            .Where(n => validTargets.Contains(n.Target.ToUpper()))
            .OrderByDescending(n => n.SentAt)
            .Select(n => new SystemNotificationDto
            {
                NotificationId = n.NotificationId,
                Title = n.Title,
                Content = n.Content,
                IsUrgent = n.IsUrgent,
                Target = n.Target,
                SentAt = n.SentAt,
                Type = "System"
            })
            .ToListAsync();

        return notifications;
    }
}
