using System;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class AddItemRequest
{
    [Required(ErrorMessage = "ProductId là bắt buộc.")]
    public Guid ProductId { get; set; }

    [Required(ErrorMessage = "Số lượng là bắt buộc.")]
    [Range(0.0001, double.MaxValue, ErrorMessage = "Số lượng phải lớn hơn 0.")]
    public decimal Quantity { get; set; } = 1;

    [Range(0, double.MaxValue, ErrorMessage = "Giá không được âm.")]
    public decimal? UnitPrice { get; set; }

    /// <summary>Đơn vị bán (ví dụ: "Cuộn", "Mét"). Null = BaseUnit.</summary>
    [StringLength(50)]
    public string? SelectedUnit { get; set; }
}
