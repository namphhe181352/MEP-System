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
    /// Optional: Assign a specific user to perform the inventory check
    /// If null, any user with STOCK_CHECK_CREATE permission can fill
    /// </summary>
    public Guid? AssigneeUserId { get; set; }
}

public class UpdateInventoryCheckDto
{
    /// <summary>
    /// Updated notes for the inventory check ticket
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Products to add to the inventory check (only in Draft status)
    /// </summary>
    public List<Guid>? AddProductIds { get; set; }

    /// <summary>
    /// Product IDs to remove from the inventory check (only in Draft status)
    /// </summary>
    public List<Guid>? RemoveProductIds { get; set; }

    /// <summary>
    /// Reassign the inventory check to a different user
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

    // Audit trail fields
    public Guid? FilledByUserId { get; set; }
    public DateTime? FilledAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledReason { get; set; }

    // Assignee
    public Guid? AssigneeUserId { get; set; }

    // Linked StockTicket after approval
    public Guid? LinkedStockTicketId { get; set; }

    public string? Notes { get; set; }
    public List<InventoryCheckDetailDto> Details { get; set; } = new();
}

public class InventoryCheckListItemDto
{
    public Guid TicketId { get; set; }
    public string TicketCode { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = null!;
    public int DetailCount { get; set; }
    public Guid BranchId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? AssigneeUserId { get; set; }
}