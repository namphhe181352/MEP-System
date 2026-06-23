using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class UpdateInvoiceItemRequest
{
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
}
