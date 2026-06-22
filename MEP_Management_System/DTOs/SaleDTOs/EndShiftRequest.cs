using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class EndShiftRequest
{
    public decimal ActualCash { get; set; }
    public string? Note { get; set; }
}
