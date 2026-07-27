using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.InventoryDTOs;

namespace MEP_Management_System.Services
{
    public interface ISupplierDebtService
    {
        Task<SupplierDebtDetailResponse?> GetSupplierDebtDetailAsync(Guid supplierId, ClaimsPrincipal currentUser);

        /// <summary>
        /// Tính công nợ hiện tại của 1 NCC tại thời điểm asOfDate (mặc định UtcNow).
        /// Formula: POs(APPROVED|COMPLETED) + StockTickets(PURCHASE, COMPLETED) - SupplierPayments(IsDeleted=0)
        /// </summary>
        Task<SupplierDebtSummary> CalculateCurrentDebtAsync(Guid supplierId, DateTime? asOfDate = null);

        /// <summary>
        /// Tính công nợ cho nhiều NCC, trả về dictionary Guid -> debt summary.
        /// </summary>
        Task<System.Collections.Generic.Dictionary<Guid, SupplierDebtSummary>> CalculateCurrentDebtBatchAsync(System.Collections.Generic.IEnumerable<Guid> supplierIds, DateTime? asOfDate = null);
    }

    public class SupplierDebtSummary
    {
        /// <summary>Dư nợ hiện tại, luôn >= 0. Khi trả thừa → = 0 (xem OverpaidAmount).</summary>
        public decimal CurrentDebt { get; set; }
        public decimal TotalPurchased { get; set; }
        public decimal TotalPaid { get; set; }
        /// <summary>Số tiền trả thừa (>0 khi TotalPaid &gt; TotalPurchased). FE dùng để hiển thị "Trả thừa X".</summary>
        public decimal OverpaidAmount { get; set; }
        public string Status { get; set; } = "Còn nợ"; // "Còn nợ" | "Hết nợ" | "Trả thừa"
    }
}
