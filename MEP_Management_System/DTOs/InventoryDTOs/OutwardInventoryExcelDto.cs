using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace MEP_Management_System.DTOs.InventoryDTOs
{
    public class OutwardInventoryExcelUploadDto
    {
        public IFormFile? File { get; set; }
        public Guid? BranchId { get; set; }
        public Guid? TargetBranchId { get; set; } // for TRANSFER
        public string? Note { get; set; }
    }

    public class OutwardExcelRowDto
    {
        public int RowNumber { get; set; }
        public string MaSanPham { get; set; } = null!;
        public string? TenSanPham { get; set; }
        public Guid? ProductId { get; set; }
        public int SoLuong { get; set; }
        public decimal DonGiaXuat { get; set; }
        public string LyDo { get; set; } = null!;
        public string TicketType { get; set; } = null!; // RETURN_SUPPLIER | WRITE_OFF | TRANSFER
        public string? GhiChu { get; set; }
    }

    public class OutwardExcelErrorDto
    {
        public int RowNumber { get; set; }
        public string MaSanPham { get; set; } = null!;
        public string ErrorMessage { get; set; } = null!;
    }

    public class OutwardExcelGroupDto
    {
        public string TicketType { get; set; } = null!;
        public string LyDo { get; set; } = null!;
        public int ItemCount { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class OutwardExcelPreviewDto
    {
        public List<OutwardExcelRowDto> ValidRows { get; set; } = new();
        public List<OutwardExcelErrorDto> Errors { get; set; } = new();
        public List<OutwardExcelGroupDto> Groups { get; set; } = new();
        public bool HasErrors { get; set; }
    }

    public class OutwardExcelCommitDto
    {
        public Guid? BranchId { get; set; }
        public Guid? TargetBranchId { get; set; }
        public List<OutwardExcelRowDto> Rows { get; set; } = new();
    }

    public class OutwardExcelCreatedTicketDto
    {
        public Guid TicketId { get; set; }
        public string TicketCode { get; set; } = null!;
        public string TicketType { get; set; } = null!;
        public int ItemCount { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class OutwardExcelResultDto
    {
        public List<OutwardExcelCreatedTicketDto> CreatedTickets { get; set; } = new();
        public int TotalTickets { get; set; }
        public decimal TotalValue { get; set; }
    }
}
