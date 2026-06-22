using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class DebtPaymentRequest
{
    public Guid CustomerId { get; set; }
    public decimal Amount { get; set; }
}
