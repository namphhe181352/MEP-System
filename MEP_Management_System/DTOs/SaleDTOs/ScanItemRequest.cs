using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class ScanItemRequest
{
    [Required(ErrorMessage = "Barcode là bắt buộc.")]
    [StringLength(50, ErrorMessage = "Barcode không được vượt quá 50 ký tự.")]
    public string Barcode { get; set; } = null!;

    [Range(0.0001, double.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0.")]
    public decimal Quantity { get; set; } = 1;
}
