using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.Services.InventoryService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocationsController : ControllerBase
    {
        private readonly ILocationService _locationService;

        public LocationsController(ILocationService locationService)
        {
            _locationService = locationService;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("userId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
        }

        private Guid? GetCurrentBranchId()
        {
            var branchIdClaim = User.FindFirst("branchId")?.Value;
            return Guid.TryParse(branchIdClaim, out Guid branchId) ? branchId : null;
        }

        private void EnsureUserId()
        {
            if (GetCurrentUserId() == null)
                throw new UnauthorizedAccessException("Không xác định được người dùng.");
        }

        private void EnsureCreatePermission()
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.LOCATION_CREATE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                throw new UnauthorizedAccessException("Bạn không có quyền thêm vị trí kho.");
        }

        private void EnsureUpdatePermission()
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.LOCATION_UPDATE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                throw new UnauthorizedAccessException("Bạn không có quyền sửa vị trí kho.");
        }

        private void EnsureDeletePermission()
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.LOCATION_DELETE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                throw new UnauthorizedAccessException("Bạn không có quyền xóa vị trí kho.");
        }

        [HttpGet]
        public async Task<IActionResult> GetLocations([FromQuery] bool? activeOnly = true)
        {
            var locations = await _locationService.GetLocationsAsync(GetCurrentBranchId(), activeOnly);

            return Ok(new ApiResponse<List<Models.ProductLocation>>
            {
                Success = true,
                Message = "Lấy danh sách vị trí thành công.",
                Data = locations
            });
        }

        [HttpGet("{id:guid}")]
        [HasPermission(PermissionConstants.LOCATION_VIEW)]
        public async Task<IActionResult> GetLocationById(Guid id)
        {
            var location = await _locationService.GetLocationByIdAsync(id);
            if (location == null)
                throw new KeyNotFoundException("Không tìm thấy vị trí.");

            return Ok(new ApiResponse<Models.ProductLocation>
            {
                Success = true,
                Message = "Lấy thông tin vị trí thành công.",
                Data = location
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateLocation([FromBody] CreateLocationDto dto)
        {
            EnsureUserId();
            EnsureCreatePermission();

            var location = await _locationService.CreateLocationAsync(dto, GetCurrentBranchId(), GetCurrentUserId());

            return Ok(new ApiResponse<Models.ProductLocation>
            {
                Success = true,
                Message = "Tạo vị trí thành công.",
                Data = location
            });
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] UpdateLocationDto dto)
        {
            EnsureUserId();
            EnsureUpdatePermission();

            var location = await _locationService.UpdateLocationAsync(id, dto, GetCurrentBranchId(), GetCurrentUserId());

            return Ok(new ApiResponse<Models.ProductLocation>
            {
                Success = true,
                Message = "Cập nhật vị trí thành công.",
                Data = location
            });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteLocation(Guid id)
        {
            EnsureDeletePermission();

            await _locationService.DeleteLocationAsync(id, GetCurrentBranchId());

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Xóa vị trí thành công."
            });
        }
    }
}
