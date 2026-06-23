using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.InventoryDTOs;

public class CreateInventoryCheckDto
{
    [Required]
    public List<Guid> ProductIds { get; set; } = new();

    public string? Notes { get; set; }
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

public class InventoryCheckDetailDto
{
    public Guid DetailId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public int SystemQuantity { get; set; }
    public int ActualQuantity { get; set; }
    public int Discrepancy { get; set; }
}

public class InventoryCheckResponseDto
{
    public Guid TicketId { get; set; }
    public string TicketCode { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = null!;
    public Guid BranchId { get; set; }
    public Guid CreatedByUserId { get; set; }
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
}