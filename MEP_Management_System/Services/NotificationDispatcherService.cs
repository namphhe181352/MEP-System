using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MEP_Management_System.Hubs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MEP_Management_System.Services;

/// <summary>
/// Background service tự động gửi SystemNotifications khi đến giờ ScheduledAt.
/// Chạy mỗi 60 giây, quét SystemNotifications WHERE Status = 'SCHEDULED' AND ScheduledAt &lt;= now,
/// sau đó tạo Notification rows cho từng user theo Target và broadcast qua SignalR.
/// </summary>
public class NotificationDispatcherService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationDispatcherService> _logger;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(60);

    public NotificationDispatcherService(
        IServiceProvider serviceProvider,
        ILogger<NotificationDispatcherService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationDispatcherService started. Scan every {Interval}", ScanInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchScheduledNotificationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in NotificationDispatcherService");
            }

            try
            {
                await Task.Delay(ScanInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task DispatchScheduledNotificationsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MEPDatabaseContext>();
        var mepHub = scope.ServiceProvider.GetRequiredService<IHubContext<MepHub>>();

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var pending = await context.SystemNotifications
            .Where(n => n.Status == "SCHEDULED"
                && n.ScheduledAt != null
                && n.ScheduledAt <= now)
            .ToListAsync(stoppingToken);

        if (pending.Count == 0) return;

        _logger.LogInformation("Dispatching {Count} scheduled notifications", pending.Count);

        foreach (var sysNotif in pending)
        {
            try
            {
                await DispatchOneAsync(context, mepHub, sysNotif, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch notification {Id}", sysNotif.NotificationId);
            }
        }
    }

    private async Task DispatchOneAsync(
        MEPDatabaseContext context,
        IHubContext<MepHub> mepHub,
        SystemNotification sysNotif,
        CancellationToken stoppingToken)
    {
        // Xác định danh sách user nhận
        var recipients = await ResolveRecipientsAsync(context, sysNotif, stoppingToken);

        // Tạo Notification row cho từng user
        foreach (var userId in recipients)
        {
            var notif = new Notification
            {
                NotificationId = Guid.NewGuid(),
                Type = NotificationType.Announcement,
                RecipientUserId = userId,
                BranchId = sysNotif.BranchId ?? Guid.Empty,
                ReferenceId = sysNotif.NotificationId,
                ReferenceType = "SystemNotification",
                TriggeredByUserId = sysNotif.CreatedBy,
                Message = $"[{sysNotif.Title}] {sysNotif.Content}",
                IsRead = false,
                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };
            context.Notifications.Add(notif);

            // Broadcast cho user đang online
            await mepHub.Clients.Group($"user_{userId}").SendAsync("SystemNotification", new
            {
                Id = sysNotif.NotificationId,
                Title = sysNotif.Title,
                Content = sysNotif.Content,
                IsUrgent = sysNotif.IsUrgent,
                Timestamp = DateTime.UtcNow
            }, stoppingToken);
        }

        // Cũng broadcast cho group admin để log/dashboard nếu cần
        await mepHub.Clients.Group("admin").SendAsync("SystemNotification", new
        {
            Id = sysNotif.NotificationId,
            Title = sysNotif.Title,
            Content = sysNotif.Content,
            IsUrgent = sysNotif.IsUrgent,
            Timestamp = DateTime.UtcNow
        }, stoppingToken);

        // Đánh dấu đã gửi
        sysNotif.Status = "SENT";
        sysNotif.SentAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await context.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("Dispatched notification {Id} to {Count} recipients", sysNotif.NotificationId, recipients.Count);
    }

    private static async Task<List<Guid>> ResolveRecipientsAsync(
        MEPDatabaseContext context,
        SystemNotification sysNotif,
        CancellationToken stoppingToken)
    {
        var result = new HashSet<Guid>();

        // Lọc theo Target
        var usersQuery = context.Users.AsNoTracking().Where(u => u.IsActive == 1);

        switch (sysNotif.Target)
        {
            case "OWNER":
                // Owner role
                var ownerIds = await context.Roles
                    .Where(r => r.RoleName == "Owner")
                    .SelectMany(r => r.Users)
                    .Where(u => u.IsActive == 1)
                    .Select(u => u.UserId)
                    .ToListAsync(stoppingToken);
                foreach (var id in ownerIds) result.Add(id);
                break;

            case "STAFF":
                // Tất cả staff (không bao gồm Admin)
                var staffIds = await context.Roles
                    .Where(r => r.RoleName != "Admin")
                    .SelectMany(r => r.Users)
                    .Where(u => u.IsActive == 1)
                    .Select(u => u.UserId)
                    .ToListAsync(stoppingToken);
                foreach (var id in staffIds) result.Add(id);
                break;

            case "ALL":
            default:
                // Tất cả user active
                var allIds = await usersQuery.Select(u => u.UserId).ToListAsync(stoppingToken);
                foreach (var id in allIds) result.Add(id);
                break;
        }

        // Nếu có BranchId → lọc thêm theo StaffAssignments
        if (sysNotif.BranchId.HasValue && sysNotif.BranchId.Value != Guid.Empty)
        {
            var branchId = sysNotif.BranchId.Value;
            var userIdsInBranch = await context.StaffAssignments
                .Where(sa => sa.BranchId == branchId)
                .Select(sa => sa.UserId)
                .ToListAsync(stoppingToken);
            var branchSet = new HashSet<Guid>(userIdsInBranch);
            result.IntersectWith(branchSet);
        }

        return result.ToList();
    }
}
