using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MEP_Management_System.Hubs;

/// <summary>
/// General-purpose hub for admin and cross-branch notifications.
/// Groups: admin, branch_{branchId}, user_{userId}
/// </summary>
[Authorize]
public class MepHub : Hub
{
    /// <summary>
    /// Maps the SignalR connection to the "admin" group so admins receive
    /// all store-approval and system-announcement events.
    /// </summary>
    public async Task JoinAdminGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "admin");
    }

    /// <summary>
    /// Maps the SignalR connection to a branch-scoped group.
    /// </summary>
    public async Task JoinBranchGroup(string branchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"branch_{branchId}");
    }

    /// <summary>
    /// Maps the SignalR connection to a user-scoped group.
    /// Used for notifying a specific owner when their store is approved / rejected.
    /// </summary>
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
    }
}
