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
    }
}

