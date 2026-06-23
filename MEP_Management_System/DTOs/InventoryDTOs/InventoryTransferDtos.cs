using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.InventoryDTOs;

public class CreateInventoryTransferDto
{
    [Required]
    public Guid FromBranchId { get; set; }

    [Required]
    public Guid ToBranchId { get; set; }

    public string? Notes { get; set; }

    [Required]
    public List<InventoryTransferDetailCreateDto> Details { get; set; } = new();
}

public class InventoryTransferDetailCreateDto
{
    [Required]
    public Guid ProductId { get; set; }
    [Required]
    public int TransferQuantity { get; set; }
}

public class InventoryTransferResponseDto
{
    public Guid TransferId { get; set; }
    public string TransferCode { get; set; } = null!;
    public Guid FromBranchId { get; set; }
    public Guid ToBranchId { get; set; }
    public string Status { get; set; } = null!;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }
    public List<InventoryTransferDetailDto> Details { get; set; } = new();
}

public class InventoryTransferDetailDto
{
    public Guid DetailId { get; set; }
    public Guid ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public int TransferQuantity { get; set; }
}

public class InventoryTransferListItemDto
{
    public Guid TransferId { get; set; }
    public string TransferCode { get; set; } = null!;
    public Guid FromBranchId { get; set; }
    public Guid ToBranchId { get; set; }
    public string Status { get; set; } = null!;
    public int DetailCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
