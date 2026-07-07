using System;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class CreateReturnOrderRequest
{
    [Required(ErrorMessage = "InvoiceId là bắt buộc.")]
    public Guid InvoiceId { get; set; }

    [Required(ErrorMessage = "Phương thức hoàn tiền là bắt buộc.")]
    [RegularExpression(@"^(CASH|TRANSFER|EXCHANGE)$",
        ErrorMessage = "Phương thức hoàn tiền không hợp lệ. Các giá trị hợp lệ: CASH, TRANSFER, EXCHANGE.")]
    public string RefundMethod { get; set; } = "CASH";

    [Required(ErrorMessage = "Loại đổi trả là bắt buộc.")]
    [RegularExpression(@"^(REFUND|EXCHANGE)$",
        ErrorMessage = "Loại đổi trả không hợp lệ. Các giá trị hợp lệ: REFUND, EXCHANGE.")]
    public string ReturnType { get; set; } = "REFUND";

    [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
    public string? Note { get; set; }
}
