
// Request/Response for PayExchange and ConfirmExchange
namespace MEP_Management_System.DTOs.SaleDTOs;

public class PayExchangeRequest
{
    public string PaymentMethod { get; set; } = "Cash";
    public decimal? CashReceived { get; set; }
    public List<CombinedPaymentRequest.PaymentLine>? PaymentLines { get; set; }
}

public class ConfirmTransferRequest
{
    public Guid PaymentId { get; set; }
}

public class PayExchangeResultDto
{
    public List<object>? Payments { get; set; }
    public string? VietQRString { get; set; }
    public string? QRImageBase64 { get; set; }
}
