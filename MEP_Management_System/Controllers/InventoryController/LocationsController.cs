using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("api/[controller]")]
    public class LocationsController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;

        public LocationsController(MEPDatabaseContext context)
        {
            _context = context;
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

        /// <summary>
        /// GET /api/locations - Lấy danh sách vị trí
        /// </summary>
        [HttpGet]
        [HasPermission(PermissionConstants.LOCATION_VIEW)]
        public async Task<IActionResult> GetLocations([FromQuery] bool? activeOnly = true)
        {
            try
            {
                var branchId = GetCurrentBranchId();
                var query = _context.ProductLocations.Where(l => l.BranchId == branchId).AsQueryable();

                if (activeOnly == true)
                {
                    query = query.Where(l => l.IsActive == 1);
                }

                var locations = await query
                    .OrderBy(l => l.LocationName)
                    .ToListAsync();

                return Ok(new ApiResponse<List<ProductLocation>>
                {
                    Success = true,
                    Message = "Lấy danh sách vị trí thành công.",
                    Data = locations
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi lấy danh sách vị trí.",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// GET /api/locations/{id} - Lấy chi tiết một vị trí
        /// </summary>
        [HttpGet("{id:guid}")]
        [HasPermission(PermissionConstants.LOCATION_VIEW)]
        public async Task<IActionResult> GetLocationById(Guid id)
        {
            try
            {
                var location = await _context.ProductLocations.FindAsync(id);

                if (location == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy vị trí."
                    });
                }

                return Ok(new ApiResponse<ProductLocation>
                {
                    Success = true,
                    Message = "Lấy thông tin vị trí thành công.",
                    Data = location
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi lấy thông tin vị trí.",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// POST /api/locations - Tạo vị trí mới
        /// </summary>
        [HttpPost]
        [HasPermission(PermissionConstants.LOCATION_CREATE)]
        public async Task<IActionResult> CreateLocation([FromBody] CreateLocationDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không xác định được người dùng."
                    });
                }

                // Validation
                if (string.IsNullOrWhiteSpace(dto.LocationName))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Tên vị trí là bắt buộc."
                    });
                }

                var branchId = GetCurrentBranchId();

                // Check duplicate name
                var existing = await _context.ProductLocations
                    .FirstOrDefaultAsync(l => l.LocationName.ToLower() == dto.LocationName.Trim().ToLower() && l.BranchId == branchId);

                if (existing != null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Tên vị trí đã tồn tại trong chi nhánh này."
                    });
                }

                var location = new ProductLocation
                {
                    LocationId = Guid.NewGuid(),
                    BranchId = branchId,
                    LocationName = dto.LocationName.Trim(),
                    LocationCode = dto.LocationCode?.Trim(),
                    Description = dto.Description?.Trim(),
                    IsActive = dto.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = currentUserId
                };

                _context.ProductLocations.Add(location);
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<ProductLocation>
                {
                    Success = true,
                    Message = "Tạo vị trí thành công.",
                    Data = location
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi tạo vị trí.",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// PUT /api/locations/{id} - Cập nhật vị trí
        /// </summary>
        [HttpPut("{id:guid}")]
        [HasPermission(PermissionConstants.LOCATION_UPDATE)]
        public async Task<IActionResult> UpdateLocation(Guid id, [FromBody] UpdateLocationDto dto)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không xác định được người dùng."
                    });
                }

                var branchId = GetCurrentBranchId();

                var location = await _context.ProductLocations.FindAsync(id);
                if (location == null || (location.BranchId != null && location.BranchId != branchId))
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy vị trí hoặc bạn không có quyền sửa vị trí này."
                    });
                }

                // Check duplicate name (exclude current)
                if (!string.IsNullOrWhiteSpace(dto.LocationName))
                {
                    var existing = await _context.ProductLocations
                        .FirstOrDefaultAsync(l => l.LocationName.ToLower() == dto.LocationName.Trim().ToLower() && l.LocationId != id && l.BranchId == branchId);

                    if (existing != null)
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Tên vị trí đã tồn tại trong chi nhánh này."
                        });
                    }

                    location.LocationName = dto.LocationName.Trim();
                }

                if (dto.LocationCode != null)
                {
                    location.LocationCode = dto.LocationCode.Trim();
                }

                if (dto.Description != null)
                {
                    location.Description = dto.Description.Trim();
                }

                if (dto.IsActive.HasValue)
                {
                    location.IsActive = dto.IsActive.Value;
                }

                location.UpdatedAt = DateTime.UtcNow;
                location.UpdatedBy = currentUserId;

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<ProductLocation>
                {
                    Success = true,
                    Message = "Cập nhật vị trí thành công.",
                    Data = location
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi cập nhật vị trí.",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// DELETE /api/locations/{id} - Xóa vị trí
        /// </summary>
        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionConstants.LOCATION_DELETE)]
        public async Task<IActionResult> DeleteLocation(Guid id)
        {
            try
            {
                var branchId = GetCurrentBranchId();

                var location = await _context.ProductLocations.FindAsync(id);
                if (location == null || (location.BranchId != null && location.BranchId != branchId))
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy vị trí hoặc bạn không có quyền xóa."
                    });
                }

                // Soft delete - set IsActive = 0
                location.IsActive = 0;
                location.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Xóa vị trí thành công."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi xóa vị trí.",
                    Errors = new() { ex.Message }
                });
            }
        }
    }

    public class CreateLocationDto
    {
        public string LocationName { get; set; } = null!;
        public string? LocationCode { get; set; }
        public string? Description { get; set; }
        public short IsActive { get; set; } = 1;
    }

    public class UpdateLocationDto
    {
        public string? LocationName { get; set; }
        public string? LocationCode { get; set; }
        public string? Description { get; set; }
        public short? IsActive { get; set; }
    }
}
