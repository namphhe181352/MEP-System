using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.InventoryDTOs;

public class CreateInventoryCheckDto
{
    [Required]
    public List<Guid> ProductIds { get; set; } = new();

    public string? Notes { get; set; }

    /// <summary>
    /// Optional: Assign a specific user to perform the inventory check.
    /// OWNER can assign to anyone.
    /// STAFF: If provided and different from CreatedByUserId, will be rejected (override to CreatedByUserId).
    /// If null, auto-assign to CreatedByUserId for Staff, or leave unassigned for Owner.
    /// </summary>
    public Guid? AssigneeUserId { get; set; }
}

public class UpdateInventoryCheckDto
{
    public string? Notes { get; set; }

    public List<Guid>? AddProductIds { get; set; }

    public List<Guid>? RemoveProductIds { get; set; }

    /// <summary>
    /// Reassign the inventory check to a different user.
    /// OWNER: can reassign to anyone.
    /// STAFF: cannot reassign to someone else (will be rejected).
    /// </summary>
    public Guid? AssigneeUserId { get; set; }
}

public class FillActualQuantityDto
{
    [Required]
    public List<InventoryCheckDetailUpdateDto> Details { get; set; } = new();
}

public class InventoryCheckDetailUpdateDto
{
    public Guid DetailId { get; set; }
    public int ActualQuantity { get; set; }
}

public class InventoryCheckReasonDto
{
    [Required]
    public List<InventoryCheckReasonItemDto> Details { get; set; } = new();
}

public class InventoryCheckReasonItemDto
{
    public Guid DetailId { get; set; }
    public string? ReasonNote { get; set; }
}

public class InventoryCheckDetailDto
{
    public Guid DetailId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public int SystemQuantity { get; set; }
    public int CurrentActualStock { get; set; }
    public int ActualQuantity { get; set; }
    public int Discrepancy { get; set; }
    public bool IsCounted { get; set; }
    public string? ReasonNote { get; set; }
    public Guid? CountedByUserId { get; set; }
    public DateTime? CountedAt { get; set; }
}

public class InventoryCheckResponseDto
{
    public Guid TicketId { get; set; }
    public string TicketCode { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = null!;
    public Guid BranchId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }

    public Guid? FilledByUserId { get; set; }
    public string? FilledByUserName { get; set; }
    public DateTime? FilledAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByUserName { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid? CancelledByUserId { get; set; }
    public string? CancelledByUserName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledReason { get; set; }

    public Guid? AssigneeUserId { get; set; }
    public string? AssigneeUserName { get; set; }

    public Guid? LinkedStockTicketId { get; set; }

    public string? Notes { get; set; }

    // Recount info
    public int RecountNumber { get; set; }
    public DateTime? RecountRequestedAt { get; set; }
    public Guid? RecountRequestedByUserId { get; set; }
    public string? RecountRequestedByUserName { get; set; }
    public string? RecountReason { get; set; }

    public List<InventoryCheckDetailDto> Details { get; set; } = new();
}

public class InventoryCheckListItemDto
{
    public Guid TicketId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty; // Draft, WaitingForApproval, Completed, Cancelled
    public Guid BranchId { get; set; }

    public Guid CreatedByUserId { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;

    public Guid? AssigneeUserId { get; set; }
    public string? AssigneeUserName { get; set; }

    public int TotalProducts { get; set; }

    public int RecountNumber { get; set; }
    public string? RecountReason { get; set; }
}

// Cancel DTO
public class CancelInventoryCheckDto
{
    public string? Reason { get; set; }
}

// Request Recount DTO - bắt buộc có lý do
public class RequestRecountDto
{
    [Required(ErrorMessage = "Lý do đếm lại là bắt buộc.")]
    public string Reason { get; set; } = null!;
}

// DTO cho Reject/RequestRecount - để audit
public class RejectRecountAuditDto
{
    public Guid? RejectedByUserId { get; set; }
    public string? RejectedByUserName { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectReason { get; set; }
}

#region Notification DTOs

public class NotificationDto
{
    public Guid NotificationId { get; set; }
    public string Type { get; set; } = null!;
    public string TypeDisplay { get; set; } = null!;
    public Guid? RecipientUserId { get; set; }
    public string? RecipientUserName { get; set; }
    public Guid BranchId { get; set; }
    public Guid? InventoryCheckId { get; set; }
    public string? InventoryCheckCode { get; set; }
    public Guid TriggeredByUserId { get; set; }
    public string? TriggeredByUserName { get; set; }
    public string Message { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationListDto
{
    public List<NotificationDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}

public class MarkNotificationsReadDto
{
    public List<Guid>? NotificationIds { get; set; }

    /// <summary>
    /// Nếu true, đánh dấu TẤT CẢ notification của user là đã đọc.
    /// </summary>
    public bool MarkAllAsRead { get; set; } = false;
}

#endregion
