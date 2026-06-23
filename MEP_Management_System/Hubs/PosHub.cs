using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MEP_Management_System.Hubs;

/// <summary>
/// POS-specific hub for real-time sales events.
/// Groups: shift_{shiftId}, branch_{branchId}
/// </summary>
[Authorize]
public class PosHub : Hub
{
    /// <summary>
    /// Maps the SignalR connection to a shift-scoped group.
    /// Used for shift-open / shift-close events that only the cashier needs.
    /// </summary>
    public async Task JoinShiftGroup(string shiftId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"shift_{shiftId}");
    }

    /// <summary>
    /// Maps the SignalR connection to a branch-scoped group.
    /// All staff at the same branch receive invoice and payment events.
    /// </summary>
    public async Task JoinBranchGroup(string branchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"branch_{branchId}");
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
