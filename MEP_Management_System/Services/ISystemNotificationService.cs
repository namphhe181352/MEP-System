using MEP_Management_System.Exceptions;
using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services;

public interface ISystemNotificationService
{
    Task<PageResultDto<SystemNotificationDto>> GetNotificationsAsync(string? status, int page, int pageSize);
    Task<SystemNotificationDto?> GetNotificationByIdAsync(Guid id);
    Task<SystemNotification> CreateNotificationAsync(CreateNotificationRequest dto, Guid adminId);
    Task<SystemNotificationDto?> UpdateNotificationAsync(Guid id, UpdateNotificationRequest dto);
    Task<SystemNotificationDto?> SendNotificationAsync(Guid id, Guid adminId);
    Task<SystemNotificationDto?> CancelNotificationAsync(Guid id);
    Task<bool> DeleteNotificationAsync(Guid id);
}

public class SystemNotificationService : ISystemNotificationService
{
    private readonly MEPDatabaseContext _context;
    private readonly IMapper _mapper;
    private readonly INotificationSender _sender;

    public SystemNotificationService(MEPDatabaseContext context, IMapper mapper, INotificationSender sender)
    {
        _context = context;
        _mapper = mapper;
        _sender = sender;
    }

    public async Task<PageResultDto<SystemNotificationDto>> GetNotificationsAsync(string? status, int page, int pageSize)
    {
        var query = _context.SystemNotifications
            .Include(n => n.CreatedByUser)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(n => n.Status == status.ToUpper());

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PageResultDto<SystemNotificationDto>
        {
            Items = _mapper.Map<List<SystemNotificationDto>>(items),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        };
    }

    public async Task<SystemNotificationDto?> GetNotificationByIdAsync(Guid id)
    {
        var notification = await _context.SystemNotifications
            .Include(n => n.CreatedByUser)
            .FirstOrDefaultAsync(n => n.NotificationId == id);
        return notification == null ? null : _mapper.Map<SystemNotificationDto>(notification);
    }

    public async Task<SystemNotification> CreateNotificationAsync(CreateNotificationRequest dto, Guid adminId)
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var notification = new SystemNotification
        {
            NotificationId = Guid.NewGuid(),
            Title = dto.Title,
            Content = dto.Content,
            Target = dto.Target.ToUpper(),
            BranchId = dto.BranchId,
            IsUrgent = dto.IsUrgent,
            ScheduledAt = dto.ScheduledAt.HasValue
                ? DateTime.SpecifyKind(dto.ScheduledAt.Value, DateTimeKind.Unspecified)
                : null,
            Status = dto.ScheduledAt.HasValue ? "SCHEDULED" : "DRAFT",
            CreatedBy = adminId,
            CreatedAt = now
        };

        _context.SystemNotifications.Add(notification);
        await _context.SaveChangesAsync();

        return notification;
    }

    public async Task<SystemNotificationDto?> UpdateNotificationAsync(Guid id, UpdateNotificationRequest dto)
    {
        var notification = await _context.SystemNotifications.FindAsync(id);
        if (notification == null) return null;

        if (notification.Status != "DRAFT")
            throw new BusinessException("Chỉ edit được DRAFT.");

        if (dto.Title != null) notification.Title = dto.Title;
        if (dto.Content != null) notification.Content = dto.Content;
        if (dto.Target != null) notification.Target = dto.Target.ToUpper();
        if (dto.IsUrgent.HasValue) notification.IsUrgent = dto.IsUrgent.Value;
        if (dto.BranchId != null) notification.BranchId = dto.BranchId;
        if (dto.ScheduledAt.HasValue)
        {
            notification.ScheduledAt = DateTime.SpecifyKind(dto.ScheduledAt.Value, DateTimeKind.Unspecified);
            notification.Status = "SCHEDULED";
        }

        await _context.SaveChangesAsync();
        return _mapper.Map<SystemNotificationDto>(notification);
    }

    public async Task<SystemNotificationDto?> SendNotificationAsync(Guid id, Guid adminId)
    {
        var notification = await _context.SystemNotifications.FindAsync(id);
        if (notification == null) return null;

        if (notification.Status == "SENT")
            throw new BusinessException("Đã gửi rồi.");
        if (notification.Status == "CANCELLED")
            throw new BusinessException("Đã hủy.");

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        notification.Status = "SENT";
        notification.SentAt = now;

        var query = _context.Users.Where(u => u.IsActive == (short)1);
        if (notification.Target == "OWNER")
        {
            query = query.Where(u => u.Roles.Any(r => r.RoleName == "Owner"));
        }
        else if (notification.Target == "STAFF")
        {
            query = query.Where(u => u.Roles.Any(r => r.RoleName == "Sales Staff" || r.RoleName == "Inventory Staff" || r.RoleName == "SalesStaff" || r.RoleName == "InventoryStaff" || r.RoleName == "Staff"));
        }

        if (notification.BranchId.HasValue && notification.BranchId.Value != Guid.Empty)
        {
            query = query.Where(u => u.StaffAssignments.Any(sa => sa.BranchId == notification.BranchId.Value));
        }

        var userIds = await query.Select(u => u.UserId).ToListAsync();
        var defaultBranchId = notification.BranchId ?? await _context.Branches.Where(b => b.IsActive == (short)1).Select(b => b.BranchId).FirstOrDefaultAsync();
        var fallbackBranchId = defaultBranchId != Guid.Empty ? defaultBranchId : Guid.NewGuid();

        var userNotifications = userIds.Select(userId => new Notification
        {
            NotificationId = Guid.NewGuid(),
            Type = NotificationType.Announcement,
            RecipientUserId = userId,
            BranchId = fallbackBranchId,
            Message = $"[{(notification.IsUrgent ? "KHẨN " : "")}{notification.Title}] {notification.Content}",
            IsRead = false,
            TriggeredByUserId = adminId,
            CreatedAt = now
        }).ToList();

        _context.Notifications.AddRange(userNotifications);
        await _context.SaveChangesAsync();

        await _sender.SendAsync(notification.Target, notification.Title, notification.Content, notification.IsUrgent);

        return _mapper.Map<SystemNotificationDto>(notification);
    }

    public async Task<SystemNotificationDto?> CancelNotificationAsync(Guid id)
    {
        var notification = await _context.SystemNotifications.FindAsync(id);
        if (notification == null) return null;

        if (notification.Status == "SENT")
            throw new BusinessException("Đã gửi.");
        if (notification.Status == "CANCELLED")
            throw new BusinessException("Đã hủy.");

        notification.Status = "CANCELLED";
        await _context.SaveChangesAsync();
        return _mapper.Map<SystemNotificationDto>(notification);
    }

    public async Task<bool> DeleteNotificationAsync(Guid id)
    {
        var notification = await _context.SystemNotifications.FindAsync(id);
        if (notification == null) return false;

        if (notification.Status != "DRAFT" && notification.Status != "CANCELLED")
            throw new BusinessException("Chỉ xóa được DRAFT/CANCELLED.");

        _context.SystemNotifications.Remove(notification);
        await _context.SaveChangesAsync();
        return true;
    }
}
