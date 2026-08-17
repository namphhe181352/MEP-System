using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MEP_Management_System.Services.UserServices;
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
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
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

            var result = await _userService.ChangeDefaultBranchAsync(userId.Value, branchId);

            return Ok(new { success = result.Success, message = result.Message, defaultBranchId = result.DefaultBranchId });
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

            var notifications = await _userService.GetSystemNotificationsAsync(userId.Value, isOwner, isStaff, days);

            return Ok(new { success = true, data = notifications });
        }
    }
}
