using MEP_Management_System.Exceptions;
using System;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/warranty")]
[Authorize]
[Tags("Quản lý bảo hành (Warranty)")]
public class WarrantyController : ControllerBase
{
    private readonly IWarrantyService _warrantyService;
    private readonly ICurrentBranchAccessor _currentBranch;

    public WarrantyController(IWarrantyService warrantyService, ICurrentBranchAccessor currentBranch)
    {
        _warrantyService = warrantyService;
        _currentBranch = currentBranch;
    }

    private Guid GetBranchId() => _currentBranch?.GetBranchId() ?? throw new Exception("branchId not found");

    private Guid GetUserId()
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        return Guid.Parse(userIdStr ?? throw new Exception("userId not found in token"));
    }

    [HttpGet("supplier-batches")]
    public async Task<ActionResult<List<SupplierWarrantyBatchDto>>> GetSupplierWarrantyBatches(
        [FromQuery] Guid? supplierId, [FromQuery] string? search, [FromQuery] string? status, [FromQuery] Guid? branchId)
    {
        var branchIdValue = branchId ?? _currentBranch?.GetBranchId();
        if (branchIdValue == null) return BadRequest(new { message = "Không xác định được chi nhánh." });
        var result = await _warrantyService.GetSupplierWarrantyBatchesAsync(supplierId, search, status, branchIdValue);
        return Ok(result);
    }

    [HttpGet("{productId:guid}/suggested-suppliers")]
    public async Task<ActionResult<List<SuggestedSupplierDto>>> GetSuggestedSuppliers(Guid productId)
    {
        var result = await _warrantyService.GetSuggestedSuppliersAsync(productId, GetBranchId());
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<WarrantyClaimResultDto>> CreateClaim(
        [FromQuery] Guid productId, [FromQuery] Guid ticketItemId, [FromBody] CreateWarrantyClaimRequest request)
    {
        try
        {
            var result = await _warrantyService.CreateClaimAsync(productId, ticketItemId, request, GetUserId(), GetBranchId());
            return Ok(result);
        }
        catch (BusinessException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("claims")]
    public async Task<ActionResult<List<WarrantyClaimDto>>> GetClaims([FromQuery] Guid? productId, [FromQuery] string? status)
    {
        var result = await _warrantyService.GetClaimsAsync(productId, status, GetBranchId());
        return Ok(result);
    }

    [HttpGet("claims/{claimId:guid}")]
    public async Task<ActionResult<WarrantyClaimDto>> GetClaimById(Guid claimId)
    {
        var result = await _warrantyService.GetClaimByIdAsync(claimId);
        if (result == null) return NotFound(new { message = "Không tìm thấy phiếu bảo hành." });
        return Ok(result);
    }

    [HttpPut("claims/{claimId:guid}/status")]
    public async Task<IActionResult> UpdateClaimStatus(Guid claimId, [FromBody] UpdateWarrantyClaimRequest request)
    {
        try
        {
            await _warrantyService.UpdateClaimStatusAsync(claimId, request, GetUserId());
            return Ok(new { message = "Cập nhật trạng thái thành công." });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (BusinessException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
