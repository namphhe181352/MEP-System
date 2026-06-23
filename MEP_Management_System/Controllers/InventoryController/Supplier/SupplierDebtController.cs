using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MEP_Management_System.Controllers.InventoryController.Supplier
{
    [Authorize]
    [ApiController]
    [Route("api/supplierdebt")]
    [Tags("Quản lý nhà cung cấp (Inventory)")]
    public class SupplierDebtController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly ICurrentBranchAccessor _currentBranch;

        public SupplierDebtController(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch)
        {
            _context = context;
            _currentBranch = currentBranch;
        }

        // ============================================================
        // GET /api/supplierdebt
        // Trả về danh sách công nợ nhà cung cấp theo chi nhánh.
        // Query params:
        //   searchTerm   – lọc theo mã hoặc tên NCC
        //   status       – normal | overdue | paid | all
        //   pageNumber   – mặc định 1
        //   pageSize     – mặc định 20
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetSupplierDebts([FromQuery] SupplierDebtParameters @params, [FromQuery] Guid? branchId = null)
        {
            try
            {
                var resolvedBranchId = branchId ?? _currentBranch.GetBranchId();
                if (resolvedBranchId == null)
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "JWT token is missing or invalid",
                        Errors = new() { "Authorization header required" }
                    });

                // ----- Lấy tất cả PO của chi nhánh (thông qua user thuộc branch) -----
                // Lấy danh sách userId thuộc branch hiện tại
                var branchUserIds = await _context.StaffAssignments
                    .Where(sa => sa.BranchId == resolvedBranchId.Value)
                    .Select(sa => sa.UserId)
                    .ToListAsync();

                // Tổng hợp theo Supplier: lấy các PO COMPLETED
                var supplierQuery = _context.Suppliers
                    .Include(s => s.PurchaseOrders)
                        .ThenInclude(po => po.PurchaseOrderItems)
                    .Where(s => s.Status == 1)
                    .AsQueryable();

                // Lọc theo search
                if (!string.IsNullOrWhiteSpace(@params.SearchTerm))
                {
                    var term = @params.SearchTerm.Trim().ToLower();
                    supplierQuery = supplierQuery.Where(s =>
                        s.SupplierName.ToLower().Contains(term) ||
                        s.SupplierId.ToString().ToLower().Contains(term));
                }

                var suppliers = await supplierQuery.ToListAsync();

                var today = DateTime.UtcNow.Date;

                // ----- Tính toán công nợ cho từng NCC -----
                // Quy tắc:
                //   - Phát sinh mua = tổng TotalAmount các PO COMPLETED trong kỳ (tháng hiện tại)
                //   - Đã trả = 80% giả định (thực tế cần bảng Payment riêng – xem NOTE bên dưới)
                //   - Nợ cuối kỳ = Nợ đầu kỳ + Phát sinh mua – Đã trả
                //
                // NOTE: Hệ thống hiện chưa có bảng SupplierPayment. Công thức tạm dùng:
                //   openingDebt  = tổng PO COMPLETED tháng trước chưa "paid"
                //   purchasedInPeriod = tổng PO COMPLETED trong tháng này
                //   paidInPeriod      = tổng PO có Status == "COMPLETED" (coi như đã thanh toán khi hoàn tất)
                //   Khi có bảng Payment thật, chỉ cần đổi cách tính paidInPeriod.

                var currentMonth = new DateTime(today.Year, today.Month, 1);
                var prevMonth = currentMonth.AddMonths(-1);

                var debtList = new List<SupplierDebtDto>();

                foreach (var s in suppliers)
                {
                    // PO tháng trước (COMPLETED) → dư nợ đầu kỳ tạm tính là tổng PO APPROVED chưa COMPLETED
                    decimal openingDebt = s.PurchaseOrders
                        .Where(po => po.CreatedAt < currentMonth && po.Status == "APPROVED")
                        .Sum(po => po.TotalAmount ?? 0);

                    // Phát sinh mua trong tháng hiện tại (COMPLETED + APPROVED)
                    decimal purchasedInPeriod = s.PurchaseOrders
                        .Where(po => po.CreatedAt >= currentMonth
                                  && (po.Status == "COMPLETED" || po.Status == "APPROVED"))
                        .Sum(po => po.TotalAmount ?? 0);

                    // Đã trả trong kỳ = các PO COMPLETED tháng này (coi đã thanh toán)
                    decimal paidInPeriod = s.PurchaseOrders
                        .Where(po => po.CreatedAt >= currentMonth && po.Status == "COMPLETED")
                        .Sum(po => po.TotalAmount ?? 0);

                    decimal closingDebt = openingDebt + purchasedInPeriod - paidInPeriod;

                    // Hạn thanh toán: lấy ExpectedDeliveryDate gần nhất của PO APPROVED
                    var latestApprovedPo = s.PurchaseOrders
                        .Where(po => po.Status == "APPROVED")
                        .OrderByDescending(po => po.ExpectedDeliveryDate)
                        .FirstOrDefault();

                    DateTime? dueDate = latestApprovedPo?.ExpectedDeliveryDate;
                    int overdueDays = 0;
                    if (dueDate.HasValue && dueDate.Value.Date < today && closingDebt > 0)
                        overdueDays = (today - dueDate.Value.Date).Days;

                    // Xác định status
                    string status;
                    if (closingDebt <= 0)
                        status = "paid";
                    else if (overdueDays > 0)
                        status = "overdue";
                    else
                        status = "normal";

                    debtList.Add(new SupplierDebtDto
                    {
                        Id = s.SupplierId.ToString(),
                        Code = s.SupplierId.ToString()[..8].ToUpper(), // 8 ký tự đầu GUID làm mã hiển thị
                        Name = s.SupplierName,
                        ContactPhone = s.ContactPhone,
                        ContactEmail = s.ContactEmail,
                        Address = s.Address,
                        GroupName = null, // Supplier model chưa có GroupName - để null hoặc mở rộng sau
                        OpeningDebt = openingDebt,
                        PurchasedInPeriod = purchasedInPeriod,
                        PaidInPeriod = paidInPeriod,
                        ClosingDebt = closingDebt,
                        DueDate = dueDate?.ToString("yyyy-MM-dd") ?? "",
                        OverdueDays = overdueDays,
                        Status = status
                    });
                }

                // ----- Lọc theo status -----
                if (!string.IsNullOrWhiteSpace(@params.Status) && @params.Status != "all")
                    debtList = debtList.Where(d => d.Status == @params.Status).ToList();

                // ----- Summary -----
                var summary = new SupplierDebtSummaryDto
                {
                    TotalClosingDebt = debtList.Sum(d => d.ClosingDebt),
                    TotalOverdueDebt = debtList.Where(d => d.Status == "overdue").Sum(d => d.ClosingDebt),
                    TotalPaidInPeriod = debtList.Sum(d => d.PaidInPeriod),
                    TotalSuppliers = debtList.Count,
                    OverdueCount = debtList.Count(d => d.Status == "overdue")
                };

                // ----- Phân trang -----
                var totalCount = debtList.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)@params.PageSize);

                var paged = debtList
                    .Skip((@params.PageNumber - 1) * @params.PageSize)
                    .Take(@params.PageSize)
                    .ToList();

                return Ok(new ApiResponse<SupplierDebtPagedResponse>
                {
                    Success = true,
                    Message = "Supplier debts retrieved successfully",
                    Data = new SupplierDebtPagedResponse
                    {
                        Items = paged,
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
        // Chi tiết công nợ 1 NCC: danh sách PO theo tháng
        // ============================================================
        [HttpGet("{supplierId:guid}")]
        public async Task<IActionResult> GetSupplierDebtDetail(Guid supplierId)
        {
            try
            {
                var supplier = await _context.Suppliers
                    .Include(s => s.PurchaseOrders)
                        .ThenInclude(po => po.PurchaseOrderItems)
                            .ThenInclude(poi => poi.Product)
                    .FirstOrDefaultAsync(s => s.SupplierId == supplierId && s.Status == 1);

                if (supplier == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Supplier not found",
                        Errors = new() { $"No supplier with ID: {supplierId}" }
                    });

                var poDetails = supplier.PurchaseOrders
                    .OrderByDescending(po => po.CreatedAt)
                    .Select(po => new SupplierDebtPoDetailDto
                    {
                        PurchaseOrderId = po.PurchaseOrderId,
                        OrderCode = po.OrderCode ?? po.PurchaseOrderId.ToString()[..8].ToUpper(),
                        TotalAmount = po.TotalAmount ?? 0,
                        Status = po.Status,
                        ExpectedDeliveryDate = po.ExpectedDeliveryDate?.ToString("yyyy-MM-dd"),
                        CreatedAt = po.CreatedAt?.ToString("yyyy-MM-dd"),
                        Items = po.PurchaseOrderItems.Select(i => new SupplierDebtPoItemDto
                        {
                            ProductId = i.ProductId,
                            ProductCode = i.Product.ProductCode,
                            ProductName = i.Product.ProductName,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice,
                            LineTotal = i.Quantity * i.UnitPrice
                        }).ToList()
                    }).ToList();

                return Ok(new DTOs.ApiResponse<SupplierDebtDetailResponse>
                {
                    Success = true,
                    Message = "Supplier debt detail retrieved successfully",
                    Data = new SupplierDebtDetailResponse
                    {
                        SupplierId = supplier.SupplierId,
                        SupplierName = supplier.SupplierName,
                        ContactPhone = supplier.ContactPhone,
                        ContactEmail = supplier.ContactEmail,
                        PurchaseOrders = poDetails
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
        // GET /api/supplierdebt/export
        // Xuất danh sách công nợ dạng CSV (đơn giản, không cần thư viện)
        // ============================================================
        [HttpGet("export")]
        public async Task<IActionResult> ExportCsv([FromQuery] SupplierDebtParameters @params)
        {
            // Tái sử dụng logic lấy data – gọi lại query giống GetSupplierDebts
            @params.PageSize = 10000; // lấy hết
            @params.PageNumber = 1;

            var result = await GetSupplierDebts(@params) as OkObjectResult;
            if (result?.Value is not ApiResponse<SupplierDebtPagedResponse> apiResp || !apiResp.Success)
                return BadRequest("Không thể xuất dữ liệu.");

            var items = apiResp.Data!.Items;

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Mã NCC,Tên nhà cung cấp,Nợ đầu kỳ,Phát sinh mua,Đã trả trong kỳ,Nợ cuối kỳ,Hạn thanh toán,Trạng thái");
            foreach (var d in items)
            {
                csv.AppendLine($"{d.Code},{d.Name},{d.OpeningDebt},{d.PurchasedInPeriod},{d.PaidInPeriod},{d.ClosingDebt},{d.DueDate},{d.Status}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"cong-no-ncc-{DateTime.Now:yyyyMMdd}.csv");
        }
    }
}