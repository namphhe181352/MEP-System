using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.Models;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MEP_Management_System.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Tags("User Management")]
    public class UsersController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;

        public UsersController(MEPDatabaseContext context)
        {
            _context = context;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
        }

        /// <summary>
        /// Change default branch for current user
        /// PUT /api/users/me/default-branch?branchId=guid
        /// </summary>
        [HttpPut("me/default-branch")]
        public async Task<IActionResult> ChangeDefaultBranch([FromQuery] Guid branchId)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "User not authenticated" });

            var user = await _context.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound(new { message = "Không tìm thấy người dùng" });

            // Verify user has access to this branch
            var hasAccess = await _context.StaffAssignments
                .AnyAsync(sa => sa.UserId == userId.Value && sa.BranchId == branchId);

            if (!hasAccess)
                return StatusCode(403, new { message = "You don't have access to this branch" });

            user.DefaultBranchId = branchId;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Default branch changed", defaultBranchId = branchId });
        }

        /// <summary>
        /// Lấy danh sách thông báo hệ thống dành cho người dùng hiện tại
        /// GET /api/users/me/system-notifications
        /// </summary>
        [HttpGet("me/system-notifications")]
        public async Task<IActionResult> GetSystemNotifications([FromQuery] int days = 7)
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { message = "User not authenticated" });

            var roles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value.ToUpper()).ToList();
            var isOwner = roles.Contains("OWNER");
            var isStaff = roles.Contains("STAFF") || roles.Contains("SALESSTAFF") || roles.Contains("INVENTORYSTAFF");

            var validTargets = new List<string> { "ALL" };
            if (isOwner) validTargets.Add("OWNER");
            if (isStaff) validTargets.Add("STAFF");

            var cutoff = DateTime.UtcNow.AddDays(-days);

            var notifications = await _context.SystemNotifications
                .Where(n => n.Status == "SENT" && n.SentAt != null && n.SentAt >= cutoff)
                .Where(n => validTargets.Contains(n.Target.ToUpper()))
                .OrderByDescending(n => n.SentAt)
                .Select(n => new
                {
                    notificationId = n.NotificationId,
                    title = n.Title,
                    content = n.Content,
                    isUrgent = n.IsUrgent,
                    target = n.Target,
                    sentAt = n.SentAt,
                    type = "System"
                })
                .ToListAsync();

            return Ok(new { success = true, data = notifications });
        }
    }
}

