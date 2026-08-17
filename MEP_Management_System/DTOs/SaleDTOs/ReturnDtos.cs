
// Request/Response for PayExchange and ConfirmExchange
namespace MEP_Management_System.DTOs.SaleDTOs;

public class PayExchangeRequest
{
    public string? Method { get; set; }
    public decimal? CashReceived { get; set; }
    public List<CombinedPaymentRequest.PaymentLine>? PaymentLines { get; set; }
}

public class ConfirmTransferRequest
{
    public Guid? PaymentId { get; set; }
}

public class PayExchangeResultDto
{
    public List<object>? Payments { get; set; }
    public string? VietQRString { get; set; }
    public string? QRImageBase64 { get; set; }

    // --- Payload cho controller dựng response (giữ NGUYÊN response shape cũ) ---
    public ReturnOrderDto? ReturnOrder { get; set; }
    public string? Method { get; set; }
    public decimal Amount { get; set; }
    public Guid ReturnOrderId { get; set; }
    public Guid? PaymentId { get; set; }
    public string? TransactionContent { get; set; }
}
