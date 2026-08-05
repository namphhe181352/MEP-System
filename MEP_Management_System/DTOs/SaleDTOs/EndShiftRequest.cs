using System;
using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.SaleDTOs;

public class EndShiftRequest
{
    [Range(0, double.MaxValue, ErrorMessage = "Tiền mặt thực tế không được âm.")]
    public decimal ActualCash { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
    public string? Note { get; set; }

    /// <summary>Force close - dành cho Manager/Owner khi người mở ca không có mặt</summary>
    public bool ForceClose { get; set; }

    /// <summary>Lý do force close</summary>
    [StringLength(500)]
    public string? ForceCloseReason { get; set; }
}
