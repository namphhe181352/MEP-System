using System;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class CreateShiftRequest
{
    [Range(0, double.MaxValue, ErrorMessage = "Số dư đầu ca không được âm.")]
    public decimal OpeningBalance { get; set; }
}
