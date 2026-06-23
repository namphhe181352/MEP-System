
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Services.InventoryService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.InventoryController
{
    [Authorize] // Bảo mật bằng JWT token như mục 9 quy định
    [ApiController]
    [Route("api/inventory")]
    [Tags("Quản lý Kho (Inventory)")]
    public class InventorySummaryController : ControllerBase
    {
        private readonly IInventoryReportService _reportService;

        public InventorySummaryController(IInventoryReportService reportService)
        {
            _reportService = reportService;
        }

        /// <summary>
        /// API Lấy dữ liệu báo cáo Tổng hợp tồn kho định kỳ
        /// </summary>
        [HttpPost("summary-report")]
        public async Task<IActionResult> GetSummaryReport([FromBody] InventorySummaryRequest request)
        {
            if (request.FromDate > request.ToDate)
            {
                return BadRequest(new { message = "Từ ngày không thể lớn hơn Đến ngày." });
            }

            try
            {
                var result = await _reportService.GetInventorySummaryAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                // Thích hợp để tích hợp ghi log qua SystemLog sau này
                return StatusCode(500, new { message = "Lỗi hệ thống khi tải báo cáo tồn kho.", detail = ex.Message });
            }
        }
    }
}