using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class CreatePaymentRequest
{
    [Required(ErrorMessage = "Phương thức thanh toán là bắt buộc.")]
    public string Method { get; set; } = null!;

    [Required(ErrorMessage = "Số tiền là bắt buộc.")]
    [Range(0, double.MaxValue, ErrorMessage = "Số tiền không được âm.")]
    public decimal Amount { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Tiền mặt nhận được không được âm.")]
    public decimal? CashReceived { get; set; }

    public List<PaymentLine>? PaymentLines { get; set; }
}

public class PaymentLine
{
    [Required(ErrorMessage = "Phương thức thanh toán là bắt buộc.")]
    public string Method { get; set; } = null!;

    [Required(ErrorMessage = "Số tiền là bắt buộc.")]
    [Range(0, double.MaxValue, ErrorMessage = "Số tiền không được âm.")]
    public decimal Amount { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Tiền mặt nhận được không được âm.")]
    public decimal? CashReceived { get; set; }
}
