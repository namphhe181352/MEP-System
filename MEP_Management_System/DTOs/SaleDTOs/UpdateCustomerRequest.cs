using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class UpdateCustomerRequest
{
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Group { get; set; }
    public string? Notes { get; set; }
    public decimal? DebtLimit { get; set; }
    public short? Status { get; set; }
}
