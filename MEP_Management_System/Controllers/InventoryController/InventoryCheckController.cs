using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Services;
using MEP_Management_System.Services.InventoryServices;
using Microsoft.AspNetCore.Mvc;
using CancelTicketDto = MEP_Management_System.DTOs.InventoryDTOs.CancelInventoryCheckDto;

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Kiem Ke Kho (Check Inventory)")]
    public class InventoryCheckController : ControllerBase
    {
        private readonly IInventoryCheckService _inventoryCheckService;
        private readonly ICurrentBranchAccessor _currentBranch;

        public InventoryCheckController(IInventoryCheckService inventoryCheckService, ICurrentBranchAccessor currentBranch)
        {
            _inventoryCheckService = inventoryCheckService;
            _currentBranch = currentBranch;
        }

        private bool IsOwner() => User.IsInRole(PermissionConstants.OWNER_ROLE);
        private Guid? GetCurrentBranchId() => _currentBranch.GetBranchId();

        [HttpGet]
        public async Task<IActionResult> GetInventoryChecks(
            [FromQuery] string? ticketCode,
            [FromQuery] string? status,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var result = await _inventoryCheckService.GetInventoryChecksAsync(
                User, GetCurrentBranchId(), ticketCode, status, fromDate, toDate, pageNumber, pageSize);
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetInventoryCheckById(Guid id)
        {
            var result = await _inventoryCheckService.GetInventoryCheckByIdAsync(User, id, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateInventoryCheck([FromBody] InventoryCheckCreateDto dto)
        {
            var result = await _inventoryCheckService.CreateInventoryCheckAsync(User, dto, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateInventoryCheck(Guid id, [FromBody] InventoryCheckUpdateDto dto)
        {
            var result = await _inventoryCheckService.UpdateInventoryCheckAsync(User, id, dto, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPost("{id:guid}/fill-actual")]
        public async Task<IActionResult> FillActualQuantities(Guid id, [FromBody] FillActualQuantitiesDto dto)
        {
            var result = await _inventoryCheckService.FillActualQuantitiesAsync(User, id, dto, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPost("{id:guid}/approve")]
        public async Task<IActionResult> ApproveInventoryCheck(Guid id)
        {
            var result = await _inventoryCheckService.ApproveInventoryCheckAsync(User, id, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPost("{id:guid}/reject")]
        public async Task<IActionResult> RejectOrRequestRecount(Guid id, [FromBody] RejectOrRecountDto dto)
        {
            var result = await _inventoryCheckService.RejectOrRequestRecountAsync(User, id, dto, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> CancelInventoryCheck(Guid id, [FromBody] CancelTicketDto dto)
        {
            var result = await _inventoryCheckService.CancelInventoryCheckAsync(User, id, dto, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPut("{id:guid}/discrepancy-reasons")]
        public async Task<IActionResult> UpdateDiscrepancyReasons(Guid id, [FromBody] UpdateDiscrepancyReasonsDto dto)
        {
            var result = await _inventoryCheckService.UpdateDiscrepancyReasonsAsync(User, id, dto, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpGet("{id:guid}/print")]
        public async Task<IActionResult> Print(Guid id)
        {
            var html = await _inventoryCheckService.GetPrintHtmlAsync(User, id, GetCurrentBranchId());
            return Content(html, "text/html");
        }

        [HttpGet("counters")]
        public async Task<IActionResult> GetCounters()
        {
            var result = await _inventoryCheckService.GetCountersAsync(User, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications()
        {
            var result = await _inventoryCheckService.GetNotificationsAsync(User, GetCurrentBranchId());
            return Ok(result);
        }

        [HttpPost("notifications/mark-read")]
        public async Task<IActionResult> MarkNotificationsAsRead([FromBody] List<Guid>? notificationIds)
        {
            var result = await _inventoryCheckService.MarkNotificationsAsReadAsync(User, notificationIds, GetCurrentBranchId());
            return Ok(result);
        }
    }
}
