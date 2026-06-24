using System.Security.Claims;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.InventoryController.Supplier;

public static class SupplierOwnerHelper
{
    /// <summary>
    /// Lấy OwnerId từ JWT để filter tenant.
    /// - Owner: lấy ClaimTypes.NameIdentifier.
    /// - Staff: truy ngược StaffAssignment -> Branch.ManagerUserId (Owner).
    /// </summary>
    public static async Task<Guid> GetOwnerIdAsync(this HttpContext httpContext, MEPDatabaseContext db)
    {
        var user = httpContext.User;
        if (user == null || user.Identity?.IsAuthenticated != true) return Guid.Empty;

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return Guid.Empty;

        if (user.IsInRole("Owner")) return userId;

        var ownerId = await db.StaffAssignments
            .Where(sa => sa.UserId == userId && sa.Branch != null)
            .Select(sa => sa.Branch!.ManagerUserId)
            .FirstOrDefaultAsync();

        return ownerId ?? Guid.Empty;
    }
}
