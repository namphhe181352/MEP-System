namespace MEP_Management_System.DTOs.InventoryDTOs
{
    public class InventorySummaryRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public Guid? BranchId { get; set; } // Null đại diện cho "Tất cả kho"
        public bool AggregateBranches { get; set; } = true; // Cộng gộp các kho
        public bool OnlyHasTransactions { get; set; } = false; // Chỉ hàng có phát sinh
    }

    public class InventorySummaryResponse
    {
        public List<InventorySummaryItemDto> Items { get; set; } = new();
        public decimal TotalOpeningValue { get; set; }
        public decimal TotalInValue { get; set; }
        public decimal TotalOutValue { get; set; }
        public decimal TotalClosingValue { get; set; }
    }

    public class InventorySummaryItemDto
    {
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty; // ĐVT (Kg, Cây, Tấm...)

        // Tồn đầu kỳ
        public decimal OpeningQuantity { get; set; }
        public decimal OpeningValue { get; set; }

        // Nhập kho
        public decimal InQuantity { get; set; }
        public decimal InValue { get; set; }

        // Xuất kho
        public decimal OutQuantity { get; set; }
        public decimal OutValue { get; set; }

        // Tồn cuối kỳ
        public decimal ClosingQuantity { get; set; }
        public decimal ClosingValue { get; set; }
    }
}
