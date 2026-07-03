using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MEP_Management_System.Authorization;

/// <summary>
/// Handler trung tâm của hệ thống RBAC.
/// Quy tắc:
///   1. Nếu User có Role = "Owner" -> Succeed ngay lập tức, KHÔNG check Permission.
///   2. Nếu không phải Owner -> kiểm tra Permission trong DB (UserPermission + RolePermission).
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly MEPDatabaseContext _dbContext;

    public PermissionAuthorizationHandler(MEPDatabaseContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.IsInRole(PermissionConstants.OWNER_ROLE))
        {
            // Owner được full quyền toàn bộ hệ thống Cửa hàng, NHƯNG tuyệt đối KHÔNG có quyền Admin (Trang quản trị cấp cao).
            if (!requirement.PermissionCode.StartsWith("ADMIN_"))
            {
                context.Succeed(requirement);
                return;
            }
        }

        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            context.Fail();
            return;
        }

        var hasDirectPermission = await _dbContext.UserPermissions
            .AsNoTracking()
            .AnyAsync(up => up.UserId == userId && up.Permission.PermissionCode == requirement.PermissionCode);

        if (hasDirectPermission)
        {
            context.Succeed(requirement);
            return;
        }

        var hasRolePermission = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .AnyAsync(u => u.Roles.Any(r => r.Permissions.Any(p => p.PermissionCode == requirement.PermissionCode)));

        if (hasRolePermission)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }
}