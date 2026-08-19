using MEP_Management_System.Exceptions;
using System.Security.Claims;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/shifts")]
[Authorize]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    private Guid? GetBranchId()
    {
        var val = User.FindFirst("branchId")?.Value;
        return string.IsNullOrEmpty(val) ? null : Guid.Parse(val);
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? throw new Exception("userId"));

    private string? GetUserName() => User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value;

    [HttpPost("start")]
    [HasPermission(PermissionConstants.SHIFT_CREATE)]
    public async Task<ActionResult<ShiftDto>> StartShift([FromBody] CreateShiftRequest request)
    {
        var userId = GetUserId();
        var branchId = GetBranchId();
        if (branchId == null) return BadRequest(ApiErrorResponse.Create("MSG-76", "Tài khoản chưa được phân công chi nhánh. Vui lòng liên hệ chủ cửa hàng."));
        try
        {
            var dto = await _shiftService.StartShiftAsync(request, userId, branchId.Value, GetUserName());
            return Ok(dto);
        }
        catch (BusinessException ex) { return Conflict(ApiErrorResponse.Create("MSG-76", ex.Message)); }
    }

    [HttpPost("{shiftId:guid}/end")]
    [HasPermission(PermissionConstants.SHIFT_UPDATE)]
    public async Task<ActionResult<ShiftDto>> EndShift(Guid shiftId, [FromBody] EndShiftRequest request)
    {
        var userId = GetUserId();
        var branchId = GetBranchId();
        if (branchId == null) return BadRequest(ApiErrorResponse.Create("MSG-76", "Tài khoản chưa được phân công chi nhánh."));
        try
        {
            var dto = await _shiftService.EndShiftAsync(shiftId, request, userId, branchId.Value, GetUserName());
            if (dto == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Shift not found"));
            return Ok(dto);
        }
        catch (BusinessException ex) { return BadRequest(ApiErrorResponse.Create("MSG-76", ex.Message)); }
    }

    [HttpGet("{shiftId:guid}/summary")]
    public async Task<ActionResult<ShiftSummaryDto>> GetShiftSummary(Guid shiftId)
    {
        var branchId = GetBranchId();
        if (branchId == null) return BadRequest(ApiErrorResponse.Create("MSG-76", "Tài khoản chưa được phân công chi nhánh."));
        var summary = await _shiftService.GetShiftSummaryAsync(shiftId, branchId.Value);
        if (summary == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Shift not found"));
        return Ok(summary);
    }

    [HttpGet]
    public async Task<ActionResult<PageResultDto<ShiftDto>>> GetShifts(
        [FromQuery] string? search,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] string? status, [FromQuery] Guid? filterUserId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var branchId = GetBranchId();
        if (branchId == null) return Ok(new PageResultDto<ShiftDto> { Items = new(), TotalCount = 0 });
        var result = await _shiftService.GetShiftsAsync(search, from, to, status, filterUserId, branchId.Value, page, pageSize);
        return Ok(result);
    }
}
