using System.Collections.Generic;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class CombinedPaymentRequest
{
    public List<PaymentLine> Lines { get; set; } = new();

    public class PaymentLine
    {
        public string Method { get; set; } = null!;
        public decimal Amount { get; set; }
        public decimal? CashReceived { get; set; }
    }
}
