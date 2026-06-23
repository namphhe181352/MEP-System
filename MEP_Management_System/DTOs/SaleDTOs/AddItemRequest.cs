using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class AddItemRequest
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
}
