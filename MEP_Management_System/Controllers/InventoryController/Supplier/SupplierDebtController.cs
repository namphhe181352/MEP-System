using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MEP_Management_System.Controllers.InventoryController.Supplier
{
    [Authorize]
    [ApiController]
    [Route("api/supplierdebt")]
    [Tags("Quản lý nhà cung cấp (Inventory)")]
    public class SupplierDebtController : ControllerBase
    {
        private readonly ISupplierDebtService _supplierDebtService;
        private readonly ICurrentBranchAccessor _currentBranch;

        public SupplierDebtController(ISupplierDebtService supplierDebtService, ICurrentBranchAccessor currentBranch)
        {
            _supplierDebtService = supplierDebtService;
            _currentBranch = currentBranch;
        }

        /// <summary>
        /// Get branch ID from current user context.
        /// </summary>
        private Guid? GetBranchId()
        {
            return _currentBranch.GetBranchId();
        }

        /// <summary>
        /// Escape CSV value to handle commas and quotes.
        /// </summary>
        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        /// <summary>
        /// GET /api/supplierdebt
        /// Trả về danh sách công nợ NCC và khối KPI tổng quan.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetSupplierDebts([FromQuery] SupplierDebtParameters @params)
        {
            var branchId = GetBranchId();
            var result = await _supplierDebtService.GetSupplierDebtsAsync(@params, branchId, User);
            return Ok(new ApiResponse<SupplierDebtPagedResponse>
            {
                Success = true,
                Message = "Supplier debts retrieved successfully",
                Data = result
            });
        }

        /// <summary>
        /// GET /api/supplierdebt/{supplierId}
        /// Trả về chi tiết công nợ kèm lịch sử PO và danh sách chi tiết item.
        /// </summary>
        [HttpGet("{supplierId:guid}")]
        public async Task<IActionResult> GetSupplierDebtDetail(Guid supplierId)
        {
            var result = await _supplierDebtService.GetSupplierDebtDetailAsync(supplierId, User);
            if (result == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Supplier not found",
                    Errors = new() { $"No active supplier found with ID: {supplierId}" }
                });
            }

            return Ok(new ApiResponse<SupplierDebtDetailResponse>
            {
                Success = true,
                Message = "Supplier debt detail retrieved successfully",
                Data = result
            });
        }

        /// <summary>
        /// GET /api/supplierdebt/export
        /// Xuất toàn bộ danh sách công nợ dạng file CSV.
        /// </summary>
        [HttpGet("export")]
        [HasPermission(PermissionConstants.SUPPLIER_PAYMENT_VIEW)]
        public async Task<IActionResult> ExportCsv([FromQuery] SupplierDebtParameters @params)
        {
            // Override pagination to get all items
            @params.PageSize = int.MaxValue;
            @params.PageNumber = 1;

            var result = await _supplierDebtService.GetSupplierDebtsAsync(@params, GetBranchId(), User);
            
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Mã NCC,Tên nhà cung cấp,Nợ đầu kỳ,Phát sinh mua,Đã trả trong kỳ,Nợ cuối kỳ,Hạn thanh toán,Trạng thái");

            foreach (var d in result.Items)
            {
                csv.AppendLine($"{EscapeCsv(d.Code)},{EscapeCsv(d.Name)},{d.OpeningDebt},{d.PurchasedInPeriod},{d.PaidInPeriod},{d.ClosingDebt},{EscapeCsv(d.DueDate)},{EscapeCsv(d.Status)}");
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(csv.ToString()))
                .ToArray();

            return File(bytes, "text/csv", $"cong-no-ncc-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
    }
}
