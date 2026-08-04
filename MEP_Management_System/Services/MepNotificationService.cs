using Microsoft.AspNetCore.SignalR;
using MEP_Management_System.Hubs;
using MEP_Management_System.DTOs.AdminDTOs;

namespace MEP_Management_System.Services;

/// <summary>
/// Scoped service that wraps SignalR HubContext for typed, group-based notifications.
/// All methods are fire-and-forget — callers do NOT await them.
/// </summary>
public class MepNotificationService
{
    private readonly IHubContext<MepHub> _mepHub;
    private readonly IHubContext<PosHub> _posHub;

    public MepNotificationService(
        IHubContext<MepHub> mepHub,
        IHubContext<PosHub> posHub)
    {
        _mepHub = mepHub;
        _posHub = posHub;
    }

    // ─── Admin / Store Approval ────────────────────────────────────────────────

    /// <summary>
    /// Broadcasts a new store-approval request to all connected Admin clients.
    /// </summary>
    public async Task NotifyAdminNewApproval(StoreApprovalDto approval)
    {
        await _mepHub.Clients.Group("admin").SendAsync("StoreApprovalPending", approval);
    }

    /// <summary>
    /// Sends the approval/rejection decision to the store owner.
    /// </summary>
    public async Task NotifyOwnerApprovalDecision(Guid ownerId, string decision, string message)
    {
        await _mepHub.Clients.Group($"user_{ownerId}")
            .SendAsync("StoreApprovalDecision", new
            {
                OwnerId = ownerId,
                Decision = decision,   // "APPROVED" | "REJECTED"
                Message = message,
                Timestamp = DateTime.UtcNow
            });
    }

    /// <summary>
    /// Broadcasts a system-wide announcement to all connected clients (all groups).
    /// </summary>
    public async Task NotifySystemAnnouncement(object announcement)
    {
        await _mepHub.Clients.All.SendAsync("SystemNotification", announcement);
    }

    // ─── Admin / Branch Management ────────────────────────────────────────────

    /// <summary>
    /// Notifies a branch owner when admin performs a management action on their branch
    /// (update / soft delete / restore / hard delete).
    /// </summary>
    /// <param name="ownerId">Manager (owner) user id; null = broadcast to admin group only</param>
    /// <param name="branchId">Branch affected</param>
    /// <param name="action">e.g. UPDATE_BRANCH, SOFT_DELETE_BRANCH, RESTORE_BRANCH, HARD_DELETE_BRANCH</param>
    /// <param name="branchName">Snapshot of branch name (still available even after hard delete)</param>
    /// <param name="message">Human-readable Vietnamese message</param>
    public async Task NotifyBranchChange(Guid? ownerId, Guid branchId, string action, string branchName, string message)
    {
        var payload = new
        {
            BranchId = branchId,
            BranchName = branchName,
            Action = action,
            Message = message,
            Timestamp = DateTime.UtcNow
        };

        // Always echo to admin group so admins can see what they just did
        await _mepHub.Clients.Group("admin").SendAsync("BranchChange", payload);

        // Send to the specific owner if available
        if (ownerId.HasValue && ownerId.Value != Guid.Empty)
        {
            await _mepHub.Clients.Group($"user_{ownerId.Value}")
                .SendAsync("BranchChange", payload);
        }
    }

    // ─── POS / Invoice ────────────────────────────────────────────────────────

    /// <summary>
    /// Notifies all branch staff when a new draft invoice is created.
    /// </summary>
    public async Task NotifyBranchInvoiceCreated(Guid branchId, object invoiceSummary)
    {
        await _posHub.Clients.Group($"branch_{branchId}")
            .SendAsync("InvoiceCreated", invoiceSummary);
    }

    /// <summary>
    /// Notifies all branch staff when an invoice is finalized and stock is deducted.
    /// Includes updated stock quantities so the POS UI can refresh stock display.
    /// </summary>
    public async Task NotifyBranchInvoiceFinalized(Guid branchId, Guid invoiceId, List<object> stockUpdates)
    {
        await _posHub.Clients.Group($"branch_{branchId}")
            .SendAsync("InvoiceFinalized", new
            {
                InvoiceId = invoiceId,
                StockUpdates = stockUpdates,
                FinalizedAt = DateTime.UtcNow
            });
    }

    // ─── POS / Payment ────────────────────────────────────────────────────────

    /// <summary>
    /// Notifies all branch staff when a transfer payment has been confirmed
    /// (staff manually verified the bank account).
    /// </summary>
    public async Task NotifyBranchPaymentConfirmed(Guid branchId, Guid paymentId)
    {
        await _posHub.Clients.Group($"branch_{branchId}")
            .SendAsync("PaymentConfirmed", new
            {
                PaymentId = paymentId,
                ConfirmedAt = DateTime.UtcNow
            });
    }

    // ─── POS / Shift ──────────────────────────────────────────────────────────

    /// <summary>
    /// Notifies all branch staff when a new shift is opened.
    /// </summary>
    public async Task NotifyBranchShiftOpened(Guid branchId, object shiftSummary)
    {
        await _posHub.Clients.Group($"branch_{branchId}")
            .SendAsync("ShiftOpened", shiftSummary);
    }

    /// <summary>
    /// Notifies the branch group when a shift is closed with full summary.
    /// </summary>
    public async Task NotifyBranchShiftClosed(Guid branchId, string shiftId, object shiftSummary)
    {
        await _posHub.Clients.Group($"branch_{branchId}")
            .SendAsync("ShiftClosed", new
            {
                ShiftId = shiftId,
                Summary = shiftSummary,
                ClosedAt = DateTime.UtcNow
            });
    }
}
