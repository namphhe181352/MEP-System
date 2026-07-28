using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.InventoryController.Supplier
{
    [Authorize]
    [ApiController]
    [Route("api/supplierdebt")]
    [Tags("Quản lý nhà cung cấp (Inventory)")]
    public class SupplierDebtController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly ISupplierDebtService _supplierDebtService;

        public SupplierDebtController(MEPDatabaseContext context, ISupplierDebtService supplierDebtService)
        {
            _context = context;
            _supplierDebtService = supplierDebtService;
        }

        // 🌟 TỐI ƯU 3: Helper escape dấu phẩy và nháy kép trong CSV, tránh lỗi lệch cột dữ liệu
        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        // ============================================================
        // GET /api/supplierdebt
        // Trả về danh sách công nợ NCC và khối KPI tổng quan (Single-Store)
        // ============================================================
        [HttpGet]
        [HasPermission(PermissionConstants.SUPPLIER_PAYMENT_VIEW)]
        public async Task<IActionResult> GetSupplierDebts([FromQuery] SupplierDebtParameters @params)
        {
            try
            {
                // 🌟 TỐI ƯU 1: Bỏ .AsQueryable() vì truy vấn được thực thi ngay sau block filter này
                var supplierQuery = _context.Suppliers
                    .Include(s => s.PurchaseOrders)
                    .Include(s => s.StockTickets.Where(t => t.TicketType == "PURCHASE" && t.Status == "COMPLETED"))
                    // Tích hợp thêm dữ liệu phiếu chi thực tế để tính toán công nợ chính xác
                    .Include(s => s.SupplierPayments.Where(p => p.IsDeleted == 0))
                    .Where(s => s.Status == 1);

                // 🌟 TỐI ƯU 2: Search theo 8 ký tự đầu của GUID (khớp hoàn toàn với mã Code hiển thị ở Front-end)
                if (!string.IsNullOrWhiteSpace(@params.SearchTerm))
                {
                    var term = @params.SearchTerm.Trim().ToLower();
                    supplierQuery = supplierQuery.Where(s =>
                        s.SupplierName.ToLower().Contains(term) ||
                        s.SupplierId.ToString().Substring(0, 8).ToLower().Contains(term));
                }

                var suppliers = await supplierQuery.ToListAsync();
                var today = DateTime.UtcNow.Date;
                var currentMonth = new DateTime(today.Year, today.Month, 1);

                var debtList = new List<SupplierDebtDto>();

                foreach (var s in suppliers)
                {
                    /* * 🌟 TỐI ƯU 5: ĐÃ THAY THẾ LOGIC GIẢ LẬP BẰNG DÒNG TIỀN THỰC TẾ (SupplierPayments)
                     * - Phát sinh nhập hàng (Purchased): Tổng tiền các PO APPROVED hoặc COMPLETED.
                     * - Thực tế đã thanh toán (Paid): Tổng số tiền nằm trong các phiếu chi SupplierPayments (IsDeleted == 0).
                     */

                    // 1. Nợ đầu kỳ: (Tổng mua tháng trước) - (Tổng chi tháng trước)
                    decimal openingPurchased = s.PurchaseOrders
                        .Where(po => po.CreatedAt < currentMonth && (po.Status == "APPROVED" || po.Status == "COMPLETED"))
                        .Sum(po => po.TotalAmount ?? 0);

                    decimal openingStockTicketDebt = s.StockTickets
                        .Where(t => t.CompletedAt.HasValue && t.CompletedAt.Value < currentMonth)
                        .Sum(t => t.TotalAmount);

                    decimal openingPaid = s.SupplierPayments
                        .Where(p => p.PaymentDate < currentMonth)
                        .Sum(p => p.Amount);

                    decimal openingDebt = openingPurchased + openingStockTicketDebt - openingPaid;

                    // 2. Phát sinh mua trong kỳ (Tháng hiện tại)
                    decimal purchasedInPeriod = s.PurchaseOrders
                        .Where(po => po.CreatedAt >= currentMonth
                                  && (po.Status == "COMPLETED" || po.Status == "APPROVED"))
                        .Sum(po => po.TotalAmount ?? 0);

                    decimal stockTicketPurchasedInPeriod = s.StockTickets
                        .Where(t => t.CompletedAt.HasValue && t.CompletedAt.Value >= currentMonth)
                        .Sum(t => t.TotalAmount);

                    purchasedInPeriod += stockTicketPurchasedInPeriod;

                    // 3. Thực tế đã trả trong kỳ (Tháng hiện tại - lấy từ bảng SupplierPayment thay vì giả lập PO)
                    decimal paidInPeriod = s.SupplierPayments
                        .Where(p => p.PaymentDate >= currentMonth)
                        .Sum(p => p.Amount);

                    // 4. Dư nợ cuối kỳ = Nợ đầu kỳ + Phát sinh mua - Thực trả trong kỳ
                    // Clamp về 0 — công nợ không thể âm. Trả thừa → closingDebt = 0, status = "Trả thừa"
                    decimal rawClosing = openingDebt + purchasedInPeriod - paidInPeriod;
                    decimal closingDebt = rawClosing < 0 ? 0 : rawClosing;
                    decimal overpaidAmount = rawClosing < 0 ? -rawClosing : 0;

                    // Tính hạn thanh toán dựa trên đơn hàng APPROVED có lịch giao gần nhất
                    var latestApprovedPo = s.PurchaseOrders
                        .Where(po => po.Status == "APPROVED")
                        .OrderByDescending(po => po.ExpectedDeliveryDate)
                        .FirstOrDefault();

                    DateTime? dueDate = latestApprovedPo?.ExpectedDeliveryDate;
                    int overdueDays = 0;
                    if (dueDate.HasValue && dueDate.Value.Date < today && closingDebt > 0)
                    {
                        overdueDays = (today - dueDate.Value.Date).Days;
                    }

                    // Phân định Trạng thái công nợ (tiếng Việt: "Còn nợ" | "Hết nợ" | "Trả thừa" | "Quá hạn")
                    string calcStatus;
                    if (overpaidAmount > 0) calcStatus = "Trả thừa";
                    else if (closingDebt <= 0) calcStatus = "Hết nợ";
                    else if (overdueDays > 0) calcStatus = "Quá hạn";
                    else calcStatus = "Còn nợ";

                    debtList.Add(new SupplierDebtDto
                    {
                        Id = s.SupplierId.ToString(),
                        Code = s.SupplierId.ToString()[..8].ToUpper(),
                        Name = s.SupplierName,
                        ContactPhone = s.ContactPhone,
                        ContactEmail = s.ContactEmail,
                        Address = s.Address,
                        GroupName = s.GroupName,
                        OpeningDebt = openingDebt,
                        PurchasedInPeriod = purchasedInPeriod,
                        PaidInPeriod = paidInPeriod,
                        ClosingDebt = closingDebt,
                        OverpaidAmount = overpaidAmount,
                        DueDate = dueDate?.ToString("yyyy-MM-dd") ?? "",
                        OverdueDays = overdueDays,
                        Status = calcStatus
                    });
                }

                // Lọc theo bộ lọc trạng thái công nợ (tiếng Việt) nếu Client truyền vào
                if (!string.IsNullOrWhiteSpace(@params.Status) && @params.Status != "all")
                {
                    debtList = debtList.Where(d => d.Status == @params.Status).ToList();
                }

                // Tính toán khối SupplierDebtSummaryDto theo cấu trúc file DTO
                var summary = new SupplierDebtSummaryDto
                {
                    TotalClosingDebt = debtList.Sum(d => d.ClosingDebt),
                    TotalOverdueDebt = debtList.Where(d => d.Status == "Quá hạn").Sum(d => d.ClosingDebt),
                    TotalPaidInPeriod = debtList.Sum(d => d.PaidInPeriod),
                    TotalPurchasedInPeriod = debtList.Sum(d => d.PurchasedInPeriod),
                    TotalSuppliers = debtList.Count,
                    OverdueCount = debtList.Count(d => d.Status == "Quá hạn")
                };

                // Phân trang danh sách đã xử lý
                var totalCount = debtList.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)@params.PageSize);

                var pagedItems = debtList
                    .Skip((@params.PageNumber - 1) * @params.PageSize)
                    .Take(@params.PageSize)
                    .ToList();

                return Ok(new ApiResponse<SupplierDebtPagedResponse>
                {
                    Success = true,
                    Message = "Supplier debts retrieved successfully",
                    Data = new SupplierDebtPagedResponse
                    {
                        Items = pagedItems,
                        TotalCount = totalCount,
                        PageNumber = @params.PageNumber,
                        PageSize = @params.PageSize,
                        TotalPages = totalPages,
                        HasNextPage = @params.PageNumber < totalPages,
                        HasPreviousPage = @params.PageNumber > 1,
                        Summary = summary
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // GET /api/supplierdebt/{supplierId}
        // Trả về chi tiết công nợ kèm lịch sử PO và danh sách chi tiết item
        // ============================================================
        [HttpGet("{supplierId:guid}")]
        [HasPermission(PermissionConstants.SUPPLIER_PAYMENT_VIEW)]
        public async Task<IActionResult> GetSupplierDebtDetail(Guid supplierId)
        {
            try
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
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error",
                    Errors = new() { ex.Message }
                });
            }
        }

        // ============================================================
        // GET /api/supplierdebt/export
        // Xuất toàn bộ danh sách công nợ dạng file CSV (Không phân trang)
        // ============================================================
        [HttpGet("export")]
        [HasPermission(PermissionConstants.SUPPLIER_PAYMENT_VIEW)]
        public async Task<IActionResult> ExportCsv([FromQuery] SupplierDebtParameters @params)
        {
            // 🌟 TỐI ƯU 6: Thay vì gán cứng kích thước 10000, dùng int.MaxValue để lấy TOÀN BỘ NCC khi export báo cáo
            @params.PageSize = int.MaxValue;
            @params.PageNumber = 1;

            var result = await GetSupplierDebts(@params) as OkObjectResult;
            if (result?.Value is not ApiResponse<SupplierDebtPagedResponse> apiResp || !apiResp.Success)
            {
                return BadRequest("Không thể xuất dữ liệu báo cáo công nợ.");
            }

            var items = apiResp.Data!.Items;

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Mã NCC,Tên nhà cung cấp,Nợ đầu kỳ,Phát sinh mua,Đã trả trong kỳ,Nợ cuối kỳ,Hạn thanh toán,Trạng thái");

            foreach (var d in items)
            {
                // 🌟 TỐI ƯU 3: Bọc hàm EscapeCsv() cho các cột dữ liệu text đề phòng trường hợp chứa dấu phẩy
                csv.AppendLine($"{EscapeCsv(d.Code)},{EscapeCsv(d.Name)},{d.OpeningDebt},{d.PurchasedInPeriod},{d.PaidInPeriod},{d.ClosingDebt},{EscapeCsv(d.DueDate)},{EscapeCsv(d.Status)}");
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(csv.ToString()))
                .ToArray();

            // 🌟 TỐI ƯU 4: Đồng bộ múi giờ sang DateTime.UtcNow cho toàn bộ project
            return File(bytes, "text/csv", $"cong-no-ncc-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
    }
}