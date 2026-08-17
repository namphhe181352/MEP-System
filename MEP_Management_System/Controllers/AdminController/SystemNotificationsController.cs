using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MEP_Management_System.Controllers.AdminController;

[ApiController]
[Route("api/admin/notifications")]
[Authorize(Roles = "Admin")]
[Tags("Admin (Admin)")]
public class SystemNotificationsController : ControllerBase
{
    private readonly ISystemNotificationService _service;

    public SystemNotificationsController(ISystemNotificationService service)
    {
        _service = service;
    }

    private Guid GetCurrentUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _service.GetNotificationsAsync(status, page, pageSize);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationRequest dto)
    {
        var validTargets = new[] { "ALL", "OWNER", "STAFF", "COMMUNITY" };
        if (!validTargets.Contains(dto.Target.ToUpper()))
            return BadRequest(new { error = new { code = "MSG-400", message = "Target không hợp lệ." } });
        var notification = await _service.CreateNotificationAsync(dto, GetCurrentUserId());
        return CreatedAtAction(nameof(GetNotifications), new { id = notification.NotificationId },
            new { notificationId = notification.NotificationId, status = notification.Status });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateNotification(Guid id, [FromBody] UpdateNotificationRequest dto)
    {
        var result = await _service.UpdateNotificationAsync(id, dto);
        if (result == null) return NotFound(new { error = new { code = "MSG-404", message = "Không tìm thấy." } });
        return Ok(new { message = "Đã cập nhật." });
    }

    [HttpPut("{id}/send")]
    public async Task<IActionResult> SendNotification(Guid id)
    {
        var result = await _service.SendNotificationAsync(id, GetCurrentUserId());
        if (result == null) return NotFound(new { error = new { code = "MSG-404", message = "Không tìm thấy." } });
        return Ok(new { message = "Gửi thành công.", sentAt = result.SentAt });
    }

    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelNotification(Guid id)
    {
        var result = await _service.CancelNotificationAsync(id);
        if (result == null) return NotFound(new { error = new { code = "MSG-404", message = "Không tìm thấy." } });
        return Ok(new { message = "Hủy thành công." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        var deleted = await _service.DeleteNotificationAsync(id);
        if (!deleted) return NotFound(new { error = new { code = "MSG-404", message = "Không tìm thấy." } });
        return Ok(new { message = "Xóa thành công." });
    }
}
