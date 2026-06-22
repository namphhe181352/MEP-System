using System;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class CashPaymentRequest
{
    public decimal Amount { get; set; }
    public decimal? CashReceived { get; set; }
}
