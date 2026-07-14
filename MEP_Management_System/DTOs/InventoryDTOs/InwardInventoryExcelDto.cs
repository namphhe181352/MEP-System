using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace MEP_Management_System.DTOs.InventoryDTOs
{
    #region Excel Import DTOs

    /// <summary>
    /// Request upload file Excel nhập kho hàng loạt.
    /// </summary>
    public class InwardInventoryExcelUploadDto
    {
        /// <summary>ID chi nhánh (chỉ Owner/Admin mới truyền được, Staff lấy từ token)</summary>
        public Guid? BranchId { get; set; }

        /// <summary>ID nhà cung cấp (tùy chọn)</summary>
        public Guid? SupplierId { get; set; }

        /// <summary>Loại nhập kho: Purchase, CustomerReturn, BalanceAdjust</summary>
        public InwardInventoryType InwardType { get; set; } = InwardInventoryType.Purchase;

        /// <summary>Ghi chú chung cho phiếu nhập</summary>
        public string? Note { get; set; }

        /// <summary>File Excel cần import</summary>
        public IFormFile? File { get; set; }
    }

    /// <summary>
    /// Dòng dữ liệu đã được parse từ Excel.
    /// </summary>
    public class ExcelInwardItemDto
    {
        /// <summary>Số dòng trong file Excel (1-based, bắt đầu từ dòng tiêu đề = 1)</summary>
        public int RowNumber { get; set; }

        /// <summary>Mã sản phẩm</summary>
        public string MaSanPham { get; set; } = null!;

        /// <summary>Tên sản phẩm (đối chiếu)</summary>
        public string? TenSanPham { get; set; }

        /// <summary>Số lượng nhập</summary>
        public decimal SoLuong { get; set; }

        /// <summary>Đơn giá nhập</summary>
        public decimal DonGiaNhap { get; set; }

        /// <summary>Ghi chú dòng</summary>
        public string? GhiChu { get; set; }

        /// <summary>ID sản phẩm đã được resolve từ DB</summary>
        public Guid? ResolvedProductId { get; set; }

        /// <summary>ID BranchProduct đã được resolve từ DB</summary>
        public Guid? ResolvedBranchProductId { get; set; }
    }

    /// <summary>
    /// Lỗi validation của một dòng Excel.
    /// </summary>
    public class ExcelValidationErrorDto
    {
        /// <summary>Số dòng trong Excel</summary>
        public int RowNumber { get; set; }

        /// <summary>Mã sản phẩm (nếu có)</summary>
        public string? MaSanPham { get; set; }

        /// <summary>Nội dung lỗi</summary>
        public string ErrorMessage { get; set; } = null!;
    }

    /// <summary>
    /// Kết quả preview sau khi parse & validate Excel (Bước 1).
    /// </summary>
    public class InwardInventoryExcelPreviewDto
    {
        /// <summary>Tên file gốc</summary>
        public string FileName { get; set; } = null!;

        /// <summary>Tổng số dòng dữ liệu (không tính header)</summary>
        public int TotalRows { get; set; }

        /// <summary>Danh sách dòng hợp lệ</summary>
        public List<ExcelInwardItemDto> ValidRows { get; set; } = new();

        /// <summary>Danh sách lỗi</summary>
        public List<ExcelValidationErrorDto> Errors { get; set; } = new();

        /// <summary>ID tạm để sử dụng ở bước commit</summary>
        public string PreviewToken { get; set; } = null!;

        /// <summary>Có lỗi hay không</summary>
        public bool HasErrors => Errors.Count > 0;
    }

    /// <summary>
    /// Request commit sau khi xác nhận preview (Bước 2).
    /// </summary>
    public class InwardInventoryExcelCommitDto
    {
        /// <summary>PreviewToken từ kết quả bước 1</summary>
        public string PreviewToken { get; set; } = null!;
    }

    /// <summary>
    /// Kết quả sau khi commit thành công.
    /// </summary>
    public class InwardInventoryExcelResultDto
    {
        /// <summary>ID phiếu nhập</summary>
        public Guid TicketId { get; set; }

        /// <summary>Mã phiếu</summary>
        public string TicketCode { get; set; } = null!;

        /// <summary>ID chi nhánh</summary>
        public Guid BranchId { get; set; }

        /// <summary>Tên chi nhánh</summary>
        public string? BranchName { get; set; }

        /// <summary>Số dòng đã nhập thành công</summary>
        public int SuccessRowCount { get; set; }

        /// <summary>Tổng giá trị nhập</summary>
        public decimal TotalValue { get; set; }

        /// <summary>Thời điểm tạo</summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Model lưu tạm dữ liệu preview trong memory (hoặc có thể persist xuống DB nếu cần).
    /// </summary>
    public class InwardInventoryExcelPreview
    {
        public string PreviewToken { get; set; } = null!;
        public Guid UserId { get; set; }
        public string FileName { get; set; } = null!;
        public Guid BranchId { get; set; }
        public Guid? SupplierId { get; set; }
        public InwardInventoryType InwardType { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ExcelInwardItemDto> ValidRows { get; set; } = new();
    }

    #endregion
}
