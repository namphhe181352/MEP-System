using System.ComponentModel.DataAnnotations;

namespace MEP_Management_System.DTOs.InventoryDTOs
{
    // ============================================================
    // QUERY PARAMETERS
    // ============================================================
    public class SupplierDebtParameters
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        /// <summary>Tìm theo mã hoặc tên NCC.</summary>
        public string? SearchTerm { get; set; }

        /// <summary>normal | overdue | paid | all (mặc định all)</summary>
        public string? Status { get; set; } = "all";
    }

    public class SupplierDebtDto
    {
        public string Id { get; set; } = null!;
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public string? Address { get; set; }

        /// <summary>Nhóm NCC – chưa có trong model, để null khi chưa mở rộng.</summary>
        public string? GroupName { get; set; }

        public decimal OpeningDebt { get; set; }
        public decimal PurchasedInPeriod { get; set; }
        public decimal PaidInPeriod { get; set; }
        public decimal ClosingDebt { get; set; }

        /// <summary>Định dạng yyyy-MM-dd hoặc rỗng.</summary>
        public string DueDate { get; set; } = "";

        public int OverdueDays { get; set; }

        /// <summary>normal | overdue | paid</summary>
        public string Status { get; set; } = "normal";
    }

    // ============================================================
    // RESPONSE – KPI TỔNG QUAN (gửi kèm trong response chính)
    // ============================================================
    public class SupplierDebtSummaryDto
    {
        public decimal TotalClosingDebt { get; set; }
        public decimal TotalOverdueDebt { get; set; }
        public decimal TotalPaidInPeriod { get; set; }
        public int TotalSuppliers { get; set; }
        public int OverdueCount { get; set; }
    }

    // ============================================================
    // RESPONSE – WRAPPER PHÂN TRANG (kèm summary)
    // ============================================================
    public class SupplierDebtPagedResponse
    {
        public List<SupplierDebtDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        public SupplierDebtSummaryDto Summary { get; set; } = new();
    }

    // ============================================================
    // RESPONSE – CHI TIẾT CÔNG NỢ 1 NCC (GET /{supplierId})
    // ============================================================
    public class SupplierDebtDetailResponse
    {
        public Guid SupplierId { get; set; }
        public string SupplierName { get; set; } = null!;
        public string? ContactPhone { get; set; }
        public string? ContactEmail { get; set; }
        public List<SupplierDebtPoDetailDto> PurchaseOrders { get; set; } = new();
    }

    public class SupplierDebtPoDetailDto
    {
        public Guid PurchaseOrderId { get; set; }
        public string OrderCode { get; set; } = null!;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = null!;
        public string? ExpectedDeliveryDate { get; set; }
        public string? CreatedAt { get; set; }
        public List<SupplierDebtPoItemDto> Items { get; set; } = new();
    }

    public class SupplierDebtPoItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductCode { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }


    public class CreateSupplierPaymentDto
    {
        [Required(ErrorMessage = "Vui lòng chọn nhà cung cấp.")]
        public Guid SupplierId { get; set; }

        public Guid? PurchaseOrderId { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Số tiền thanh toán phải lớn hơn 0.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán.")]
        [RegularExpression("^(Cash|Transfer|Credit)$", ErrorMessage = "Phương thức thanh toán phải là: Cash, Transfer hoặc Credit.")]
        public string PaymentMethod { get; set; } = null!;

        public string? ReferenceCode { get; set; }

        public string? Note { get; set; }
    }

    public class UpdateSupplierPaymentDto
    {
        public string? Note { get; set; }
    }

    public class SupplierPaymentResponseDto
    {
        public Guid PaymentId { get; set; }
        public Guid SupplierId { get; set; }
        public string SupplierName { get; set; } = null!;
        public string? OrderCode { get; set; }
        public decimal Amount { get; set; }
        public string PaymentDate { get; set; } = null!;
        public string PaymentMethod { get; set; } = null!;
        public string? ReferenceCode { get; set; }
        public string? Note { get; set; }
        public string RecordedByName { get; set; } = null!;
        public string Status { get; set; } = null!; // "ACTIVE" | "CANCELLED"
        public Guid? CreatedBy { get; set; }
    }
}