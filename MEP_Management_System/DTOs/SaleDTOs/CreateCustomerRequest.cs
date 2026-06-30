using System;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class CreateCustomerRequest
{
    [Required(ErrorMessage = "Customer name is required")]
    public string CustomerName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    /// <summary>Individual | Enterprise | Agent | Contractor</summary>
    public string? Group { get; set; }
    public string? Notes { get; set; }
    public decimal? DebtLimit { get; set; }
}
