using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class ApplyPromoRequest
{
    [Required(ErrorMessage = "Mã khuyến mãi là bắt buộc.")]
    [StringLength(50, ErrorMessage = "Mã khuyến mãi không được vượt quá 50 ký tự.")]
    public string PromoCode { get; set; } = null!;
}
