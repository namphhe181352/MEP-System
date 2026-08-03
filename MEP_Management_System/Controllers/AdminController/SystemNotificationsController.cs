using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Services;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MEP_Management_System.Controllers.AdminController;

[ApiController]
[Route("api/admin/notifications")]
[Authorize(Roles = "Admin")]
[Tags("Admin (Admin)")]
public class SystemNotificationsController : ControllerBase
{
    private readonly MEPDatabaseContext _context;
    private readonly IMapper _mapper;
    private readonly INotificationSender _sender;

    public SystemNotificationsController(MEPDatabaseContext context, IMapper mapper, INotificationSender sender)
    {
        _context = context;
        _mapper = mapper;
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
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

        return Ok(new PageResultDto<SystemNotificationDto>
        {
            Items = _mapper.Map<List<SystemNotificationDto>>(items),
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = pageSize
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationRequest dto)
    {
        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var validTargets = new[] { "ALL", "OWNER", "STAFF", "COMMUNITY" };
        if (!validTargets.Contains(dto.Target.ToUpper()))
            return BadRequest(new { error = new { code = "MSG-400", message = "Target không hợp lệ." } });

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

        return CreatedAtAction(nameof(GetNotifications), new { id = notification.NotificationId },
            new { notificationId = notification.NotificationId, status = notification.Status });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateNotification(Guid id, [FromBody] UpdateNotificationRequest dto)
    {
        var notification = await _context.SystemNotifications.FindAsync(id);
        if (notification == null) return NotFound(new { error = new { code = "MSG-404", message = "Không tìm thấy." } });

        if (notification.Status != "DRAFT")
            return BadRequest(new { error = new { code = "MSG-400", message = "Chỉ edit được DRAFT." } });

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
        return Ok(new { message = "Đã cập nhật." });
    }

    [HttpPut("{id}/send")]
    public async Task<IActionResult> SendNotification(Guid id)
    {
        var notification = await _context.SystemNotifications.FindAsync(id);
        if (notification == null) return NotFound(new { error = new { code = "MSG-404", message = "Không tìm thấy." } });

        if (notification.Status == "SENT") return BadRequest(new { error = new { code = "MSG-409", message = "Đã gửi rồi." } });
        if (notification.Status == "CANCELLED") return BadRequest(new { error = new { code = "MSG-400", message = "Đã hủy." } });

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
        var adminId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var defaultBranchId = notification.BranchId ?? await _context.Branches.Where(b => b.IsActive == (short)1).Select(b => b.BranchId).FirstOrDefaultAsync();
        var fallbackBranchId = defaultBranchId != Guid.Empty ? defaultBranchId : Guid.NewGuid(); // Prevent exception if no branches exist

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

        return Ok(new { message = "Gửi thành công.", sentAt = now });
    }

    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelNotification(Guid id)
    {
        var notification = await _context.SystemNotifications.FindAsync(id);
        if (notification == null) return NotFound(new { error = new { code = "MSG-404", message = "Không tìm thấy." } });

        if (notification.Status == "SENT") return BadRequest(new { error = new { code = "MSG-400", message = "Đã gửi." } });
        if (notification.Status == "CANCELLED") return BadRequest(new { error = new { code = "MSG-409", message = "Đã hủy." } });

        notification.Status = "CANCELLED";
        await _context.SaveChangesAsync();
        return Ok(new { message = "Hủy thành công." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        var notification = await _context.SystemNotifications.FindAsync(id);
        if (notification == null) return NotFound(new { error = new { code = "MSG-404", message = "Không tìm thấy." } });

        if (notification.Status != "DRAFT" && notification.Status != "CANCELLED")
            return BadRequest(new { error = new { code = "MSG-400", message = "Chỉ xóa được DRAFT/CANCELLED." } });

        _context.SystemNotifications.Remove(notification);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Xóa thành công." });
    }
}
