using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Services;
using MEP_Management_System.Services.InventoryService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MEP_Management_System.Controllers.InventoryController.Supplier;

[Authorize]
[ApiController]
[Route("api/suppliers")]
[Tags("Quản lý nhà cung cấp (Inventory)")]
public class SupplierController : ControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly ICurrentBranchAccessor _currentBranch;

    public SupplierController(ISupplierService supplierService, ICurrentBranchAccessor currentBranch)
    {
        _supplierService = supplierService;
        _currentBranch = currentBranch;
    }

    private Guid? GetCurrentUserId()
    {
        var nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(nameIdentifier, out var parsedGuid) ? parsedGuid : null;
    }

    private Guid GetBranchId()
    {
        var branchId = _currentBranch.GetBranchId();
        if (branchId == null)
            throw new BusinessException("Không xác định được chi nhánh.");
        return branchId.Value;
    }

    [HttpGet]
    public async Task<IActionResult> GetSuppliers([FromQuery] SupplierQueryParameters p)
    {
        var branchId = GetBranchId();

        var (items, totalCount, distinctGroups) = await _supplierService.GetSuppliersAsync(branchId, p);

        var totalPages = (int)Math.Ceiling(totalCount / (double)p.PageSize);

        return Ok(new ApiResponse<SupplierPagedResponse>
        {
            Success = true,
            Message = "Suppliers retrieved successfully",
            Data = new SupplierPagedResponse
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = p.PageNumber,
                PageSize = p.PageSize,
                TotalPages = totalPages,
                HasNextPage = p.PageNumber < totalPages,
                HasPreviousPage = p.PageNumber > 1,
                DistinctGroups = distinctGroups
            }
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSupplier(Guid id)
    {
        var branchId = GetBranchId();

        var supplier = await _supplierService.GetSupplierAsync(id, branchId);
        if (supplier == null)
            throw new KeyNotFoundException($"No supplier with ID: {id}");

        return Ok(new ApiResponse<SupplierDetailDto>
        {
            Success = true,
            Message = "Supplier retrieved successfully",
            Data = supplier
        });
    }

    [HttpPost]
    [HasPermission(PermissionConstants.SUPPLIER_CREATE)]
    public async Task<IActionResult> CreateSupplier([FromBody] SupplierUpsertRequest req)
    {
        var branchId = GetBranchId();
        var userId = GetCurrentUserId();

        var result = await _supplierService.CreateSupplierAsync(req, branchId, userId);

        return Ok(new ApiResponse<SupplierDetailDto>
        {
            Success = true,
            Message = "Supplier created successfully",
            Data = result
        });
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionConstants.SUPPLIER_UPDATE)]
    public async Task<IActionResult> UpdateSupplier(Guid id, [FromBody] SupplierUpsertRequest req)
    {
        var branchId = GetBranchId();
        var userId = GetCurrentUserId();

        var result = await _supplierService.UpdateSupplierAsync(id, req, branchId, userId);
        if (result == null)
            throw new KeyNotFoundException($"No supplier with ID: {id}");

        return Ok(new ApiResponse<SupplierDetailDto>
        {
            Success = true,
            Message = "Supplier updated successfully",
            Data = result
        });
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionConstants.SUPPLIER_DELETE)]
    public async Task<IActionResult> DeleteSupplier(Guid id)
    {
        var branchId = GetBranchId();

        var (success, message) = await _supplierService.DeleteSupplierAsync(id, branchId);

        return Ok(new ApiResponse<object>
        {
            Success = success,
            Message = message
        });
    }

    [HttpPatch("{id:guid}/status")]
    [HasPermission(PermissionConstants.SUPPLIER_UPDATE)]
    public async Task<IActionResult> ToggleSupplierStatus(Guid id, [FromBody] StatusToggleRequest req)
    {
        var branchId = GetBranchId();
        var userId = GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(req?.Status))
            throw new BusinessException("Status is required. Body must be: { \"status\": \"active\" | \"inactive\" }");

        var (success, message, newStatus) = await _supplierService.ToggleStatusAsync(id, req.Status, branchId, userId);

        return Ok(new ApiResponse<SupplierStatusToggleResponse>
        {
            Success = success,
            Message = message,
            Data = new SupplierStatusToggleResponse
            {
                Id = id,
                Status = newStatus == 1 ? "active" : "inactive"
            }
        });
    }
}
