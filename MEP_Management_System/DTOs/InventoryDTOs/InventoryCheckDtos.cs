using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.InventoryDTOs;

public class CreateInventoryCheckDto
{
    public Guid? BranchId { get; set; }
    public string? Notes { get; set; }

    [Required]
    public List<InventoryCheckDetailCreateDto> Details { get; set; } = new();
}

public class InventoryCheckDetailCreateDto
{
    [Required]
    public Guid ProductId { get; set; }

    [Required]
    public decimal SystemQuantity { get; set; }

    [Required]
    public decimal ActualQuantity { get; set; }
}

public class InventoryCheckResponseDto
{
    public Guid TicketId { get; set; }
    public string TicketCode { get; set; } = null!;
    public Guid BranchId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = null!;
    public string? Notes { get; set; }
    public List<InventoryCheckDetailDto> Details { get; set; } = new();
}

public class InventoryCheckDetailDto
{
    public Guid DetailId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal ActualQuantity { get; set; }
    public decimal Discrepancy { get; set; }
}

public class InventoryCheckListItemDto
{
    public Guid TicketId { get; set; }
    public string TicketCode { get; set; } = null!;
    public Guid BranchId { get; set; }
    public string Status { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public int DetailCount { get; set; }
}
