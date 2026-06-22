using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class CreateInvoiceRequest
{
    public Guid? CustomerId { get; set; }
    public string? Note { get; set; }
}
