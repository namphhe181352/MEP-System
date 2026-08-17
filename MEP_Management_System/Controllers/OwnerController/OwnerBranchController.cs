using System.Security.Claims;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Services.OwnerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.OwnerController;

[ApiController]
[Route("api/owner/branches")]
[Authorize(Roles = "Owner")]
[Tags("Quản lý chi nhánh (Owner)")]
public class OwnerBranchController : ControllerBase
{
    private readonly IOwnerBranchService _ownerBranchService;

    public OwnerBranchController(IOwnerBranchService ownerBranchService)
    {
        _ownerBranchService = ownerBranchService;
    }

    private Guid GetOwnerUserIdFromClaim()
    {
        var claimValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(claimValue) || !Guid.TryParse(claimValue, out var userId))
            throw new UnauthorizedAccessException("User claim is missing or invalid.");
        return userId;
    }

    /// <summary>
    /// GET /api/owner/branches
    /// Returns the branch that belongs to the current Owner.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBranch()
    {
        var ownerUserId = GetOwnerUserIdFromClaim();
        var result = await _ownerBranchService.GetBranchesAsync(ownerUserId);
        return Ok(new ApiResponse<List<OwnerBranchDto>>
        {
            Success = true,
            Message = "Danh sách chi nhánh.",
            Data = result
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBranchById(Guid id)
    {
        var ownerUserId = GetOwnerUserIdFromClaim();
        var result = await _ownerBranchService.GetBranchByIdAsync(ownerUserId, id);
        return Ok(new ApiResponse<OwnerBranchDto> { Success = true, Message = "Chi tiết chi nhánh.", Data = result });
    }

    [HttpPost]
    public async Task<IActionResult> CreateBranch([FromBody] CreateOwnerBranchRequest dto)
    {
        var ownerUserId = GetOwnerUserIdFromClaim();
        var result = await _ownerBranchService.CreateBranchAsync(ownerUserId, dto);
        return CreatedAtAction(nameof(GetBranchById), new { id = result.BranchId },
            new ApiResponse<OwnerBranchDto>
            {
                Success = true,
                Message = "Chi nhánh mới đã được tạo.",
                Data = result
            });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateBranch(Guid id, [FromBody] UpdateOwnerBranchRequest dto)
    {
        var ownerUserId = GetOwnerUserIdFromClaim();
        var result = await _ownerBranchService.UpdateBranchAsync(ownerUserId, id, dto);
        return Ok(new ApiResponse<OwnerBranchDto> { Success = true, Message = "Cập nhật chi nhánh thành công.", Data = result });
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetBranchHistory(Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null)
    {
        var ownerUserId = GetOwnerUserIdFromClaim();
        var result = await _ownerBranchService.GetBranchHistoryAsync(ownerUserId, id, page, pageSize, type, status);
        return Ok(new ApiResponse<PaginatedResponse<OwnerBranchHistoryDto>>
        {
            Success = true,
            Message = "Lịch sử chi nhánh.",
            Data = result
        });
    }

    /// <summary>
    /// GET /api/owner/branches/{id}/settings
    /// Returns return/exchange policy settings for the branch.
    /// </summary>
    [HttpGet("{id:guid}/settings")]
    public async Task<IActionResult> GetBranchSettings(Guid id)
    {
        var ownerUserId = GetOwnerUserIdFromClaim();
        var result = await _ownerBranchService.GetBranchSettingsAsync(ownerUserId, id);
        return Ok(new ApiResponse<BranchSettingsDto>
        {
            Success = true,
            Message = "Cài đặt chi nhánh.",
            Data = result
        });
    }

    /// <summary>
    /// PUT /api/owner/branches/{id}/settings
    /// Updates return/exchange policy settings for the branch.
    /// </summary>
    [HttpPut("{id:guid}/settings")]
    public async Task<IActionResult> UpdateBranchSettings(Guid id, [FromBody] UpdateBranchSettingsRequest dto)
    {
        var ownerUserId = GetOwnerUserIdFromClaim();
        var result = await _ownerBranchService.UpdateBranchSettingsAsync(ownerUserId, id, dto);
        return Ok(new ApiResponse<BranchSettingsDto>
        {
            Success = true,
            Message = "Cập nhật cài đặt chi nhánh thành công.",
            Data = result
        });
    }

    /// <summary>
    /// GET /api/owner/branches/{id}/return-policies/categories
    /// Lấy cài đặt đổi trả theo nhóm hàng.
    /// </summary>
    [HttpGet("{id:guid}/return-policies/categories")]
    public async Task<IActionResult> GetCategoryPolicies(Guid id)
    {
        var ownerUserId = GetOwnerUserIdFromClaim();
        var result = await _ownerBranchService.GetCategoryPoliciesAsync(ownerUserId, id);
        return Ok(new ApiResponse<List<CategoryPolicyDto>>
        {
            Success = true,
            Message = "Thành công",
            Data = result
        });
    }

    /// <summary>
    /// PUT /api/owner/branches/{id}/return-policies/categories
    /// Cập nhật cài đặt đổi trả theo nhóm hàng.
    /// </summary>
    [HttpPut("{id:guid}/return-policies/categories")]
    public async Task<IActionResult> UpdateCategoryPolicies(Guid id, [FromBody] UpdateCategoryPoliciesRequest request)
    {
        var ownerUserId = GetOwnerUserIdFromClaim();
        await _ownerBranchService.UpdateCategoryPoliciesAsync(ownerUserId, id, request);
        return Ok(new ApiResponse<object> { Success = true, Message = "Cập nhật thành công." });
    }

    /// <summary>
    /// GET /api/owner/branches/{id}/return-policies/discount
    /// Lấy chiết khấu trả hàng branch-level (áp dụng cho mọi nhóm hàng).
    /// </summary>
    [HttpGet("{id:guid}/return-policies/discount")]
    public async Task<IActionResult> GetReturnDiscount(Guid id)
    {
        var ownerUserId = GetOwnerUserIdFromClaim();
        var result = await _ownerBranchService.GetReturnDiscountAsync(ownerUserId, id);
        return Ok(new ApiResponse<object> { Success = true, Data = result });
    }

    /// <summary>
    /// PUT /api/owner/branches/{id}/return-policies/discount
    /// Cập nhật chiết khấu trả hàng branch-level. Null/0 = không chiết khấu.
    /// </summary>
    [HttpPut("{id:guid}/return-policies/discount")]
    public async Task<IActionResult> UpdateReturnDiscount(Guid id, [FromBody] UpdateReturnDiscountRequest request)
    {
        var ownerUserId = GetOwnerUserIdFromClaim();
        var result = await _ownerBranchService.UpdateReturnDiscountAsync(ownerUserId, id, request);
        return Ok(new ApiResponse<object> { Success = true, Message = "Cập nhật chiết khấu trả hàng thành công.", Data = result });
    }
}
