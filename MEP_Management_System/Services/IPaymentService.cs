using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;

namespace MEP_Management_System.Services;

public interface IPaymentService
{
    Task<PaymentDto> CreatePaymentAsync(Guid invoiceId, CreatePaymentRequest request, Guid userId, Guid branchId);
    Task<QrCodeResultDto> GetQRCodeAsync(Guid paymentId);
    Task<PaymentDto> ConfirmTransferAsync(Guid paymentId, Guid userId, Guid branchId);
    Task<PaymentStatusDto> GetPaymentStatusAsync(Guid paymentId);
    Task<bool> CancelPaymentAsync(Guid paymentId, Guid userId);
}

public class QrCodeResultDto
{
    public string VietQRString { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
}

public class PaymentStatusDto
{
    public string? Status { get; set; }
}
