using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.OwnerController
{
    [ApiController]
    [Route("api/owner/branches")]
    [Authorize(Roles = "Owner")]
    [Tags("Quản lý chi nhánh (Owner)")]

    public class OwnerBranchController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;

        public OwnerBranchController(MEPDatabaseContext context)
        {
            _context = context;
        }

        private Guid GetOwnerUserIdFromClaim()
        {
            var claimValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(claimValue) || !Guid.TryParse(claimValue, out var userId))
                throw new UnauthorizedAccessException("User claim is missing or invalid.");
            return userId;
        }

        // Helper verify branch có thuộc owner không
        private async Task<bool> IsOwnerOfBranch(Guid ownerUserId, Guid branchId)
        {
            return await _context.StaffAssignments
                .AnyAsync(sa => sa.UserId == ownerUserId && sa.BranchId == branchId);
        }

        /// <summary>
        /// GET /api/owner/branches
        /// Returns the branch that belongs to the current Owner.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetBranch()
        {
            try
            {
                var ownerUserId = GetOwnerUserIdFromClaim();

                // Lấy TẤT CẢ branch của owner này
                var branches = await _context.Branches
                    .Include(b => b.ManagerUser)
                    .Where(b => b.StaffAssignments.Any(sa => sa.UserId == ownerUserId) && b.IsActive == 1)
                    .ToListAsync();

                if (!branches.Any())
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh nào.",
                        Errors = new() { "No active branch found for this owner." }
                    });

                return Ok(new ApiResponse<List<OwnerBranchDto>>
                {
                    Success = true,
                    Message = "Danh sách chi nhánh.",
                    Data = branches.Select(MapToDto).ToList()
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "Unauthorized.", Errors = new() { ex.Message } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi hệ thống.", Errors = new() { ex.Message } });
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetBranchById(Guid id)
        {
            try
            {
                var ownerUserId = GetOwnerUserIdFromClaim();

                if (!await IsOwnerOfBranch(ownerUserId, id))
                    return Forbid();

                var branch = await _context.Branches
                    .Include(b => b.ManagerUser)
                    .FirstOrDefaultAsync(b => b.BranchId == id && b.IsActive == 1);

                if (branch == null)
                    return NotFound(new ApiResponse<object> { Success = false, Message = "Chi nhánh không tồn tại." });

                return Ok(new ApiResponse<OwnerBranchDto> { Success = true, Message = "Chi tiết chi nhánh.", Data = MapToDto(branch) });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "Unauthorized.", Errors = new() { ex.Message } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi hệ thống.", Errors = new() { ex.Message } });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateBranch([FromBody] CreateOwnerBranchRequest dto)
        {
            try
            {
                var ownerUserId = GetOwnerUserIdFromClaim();

                if (string.IsNullOrWhiteSpace(dto.BranchName))
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Tên chi nhánh là bắt buộc." });

                if (!string.IsNullOrWhiteSpace(dto.BranchCode))
                {
                    var codeExists = await _context.Branches.AnyAsync(b => b.BranchCode == dto.BranchCode.Trim());
                    if (codeExists)
                        return BadRequest(new ApiResponse<object> { Success = false, Message = "Mã chi nhánh đã tồn tại." });
                }

                var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                var branch = new Branch
                {
                    BranchId = Guid.NewGuid(),
                    BranchName = dto.BranchName.Trim(),
                    BranchCode = dto.BranchCode?.Trim(),
                    Phone = dto.Phone?.Trim(),
                    City = dto.City?.Trim(),
                    Address = dto.Address?.Trim(),
                    Type = dto.Type?.Trim(),
                    ManagerUserId = ownerUserId, // Owner luôn là manager
                    IsActive = 1,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                await using var transaction = await _context.Database.BeginTransactionAsync();

                _context.Branches.Add(branch);

                // Gán Owner vào branch mới
                _context.StaffAssignments.Add(new StaffAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    UserId = ownerUserId,
                    BranchId = branch.BranchId,
                    AssignedAt = now
                });

                // Init tồn kho
                var products = await _context.Products.Select(p => new { p.ProductId }).ToListAsync();
                foreach (var product in products)
                {
                    _context.BranchProducts.Add(new BranchProduct
                    {
                        BranchProductId = Guid.NewGuid(),
                        BranchId = branch.BranchId,
                        ProductId = product.ProductId,
                        ActualStock = 0m,
                        AvailableStock = 0m,
                        ReservedStock = 0m,
                        CostPrice = 0m,
                        SellPrice = 0m,
                        ShelfLocation = "Chưa xếp vị trí",
                        IsActive = 1
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetBranchById), new { id = branch.BranchId },
                    new ApiResponse<OwnerBranchDto>
                    {
                        Success = true,
                        Message = "Chi nhánh mới đã được tạo.",
                        Data = MapToDto(branch)
                    });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "Unauthorized.", Errors = new() { ex.Message } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi hệ thống.", Errors = new() { ex.Message } });
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateBranch(Guid id, [FromBody] UpdateOwnerBranchRequest dto)
        {
            try
            {
                var ownerUserId = GetOwnerUserIdFromClaim();

                if (!await IsOwnerOfBranch(ownerUserId, id))
                    return Forbid();

                var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
                if (branch == null)
                    return NotFound(new ApiResponse<object> { Success = false, Message = "Chi nhánh không tồn tại." });

                if (!string.IsNullOrWhiteSpace(dto.BranchName)) branch.BranchName = dto.BranchName.Trim();
                if (!string.IsNullOrWhiteSpace(dto.BranchCode)) branch.BranchCode = dto.BranchCode.Trim();
                if (!string.IsNullOrWhiteSpace(dto.Phone)) branch.Phone = dto.Phone.Trim();
                if (!string.IsNullOrWhiteSpace(dto.City)) branch.City = dto.City.Trim();
                if (!string.IsNullOrWhiteSpace(dto.Address)) branch.Address = dto.Address.Trim();
                if (!string.IsNullOrWhiteSpace(dto.Type)) branch.Type = dto.Type.Trim();
                if (dto.IsActive.HasValue) branch.IsActive = dto.IsActive.Value;
                // ManagerUserId không cho sửa — Owner luôn là manager
                branch.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<OwnerBranchDto> { Success = true, Message = "Cập nhật chi nhánh thành công.", Data = MapToDto(branch) });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "Unauthorized.", Errors = new() { ex.Message } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi hệ thống.", Errors = new() { ex.Message } });
            }
        }

        [HttpGet("{id:guid}/history")]
        public async Task<IActionResult> GetBranchHistory(Guid id,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20,
    [FromQuery] string? type = null,
    [FromQuery] string? status = null)
        {
            try
            {
                var ownerUserId = GetOwnerUserIdFromClaim();

                if (!await IsOwnerOfBranch(ownerUserId, id))
                    return Forbid();

                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                var query = _context.StockTickets
                    .Include(st => st.User)
                    .Where(st => st.BranchId == id)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(type))
                    query = query.Where(st => st.TicketType != null && st.TicketType.ToLower() == type.Trim().ToLower());

                if (!string.IsNullOrWhiteSpace(status))
                    query = query.Where(st => st.Status != null && st.Status.ToLower() == status.Trim().ToLower());

                var total = await query.CountAsync();

                var items = await query
                    .OrderByDescending(st => st.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(st => new OwnerBranchHistoryDto
                    {
                        StockTicketId = st.StockTicketId,
                        TicketCode = st.TicketCode,
                        TicketType = st.TicketType,
                        Status = st.Status,
                        Notes = st.Notes,
                        CreatedAt = st.CreatedAt,
                        UserId = st.UserId,
                        UserFullName = st.User.FullName,
                        UserEmail = st.User.Email,
                        ItemCount = st.StockTicketItems.Count
                    })
                    .ToListAsync();

                return Ok(new ApiResponse<PaginatedResponse<OwnerBranchHistoryDto>>
                {
                    Success = true,
                    Message = "Lịch sử chi nhánh.",
                    Data = new PaginatedResponse<OwnerBranchHistoryDto>
                    {
                        Items = items,
                        TotalCount = total,
                        PageNumber = page,
                        PageSize = pageSize,
                        TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                        HasNextPage = page * pageSize < total,
                        HasPreviousPage = page > 1
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "Unauthorized.", Errors = new() { ex.Message } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi hệ thống.", Errors = new() { ex.Message } });
            }
        }

        /// <summary>
        /// GET /api/owner/branches/{id}/settings
        /// Returns return/exchange policy settings for the branch.
        /// </summary>
        [HttpGet("{id:guid}/settings")]
        public async Task<IActionResult> GetBranchSettings(Guid id)
        {
            try
            {
                var ownerUserId = GetOwnerUserIdFromClaim();

                if (!await IsOwnerOfBranch(ownerUserId, id))
                    return Forbid();

                var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
                if (branch == null)
                    return NotFound(new ApiResponse<object> { Success = false, Message = "Chi nhánh không tồn tại." });

                return Ok(new ApiResponse<BranchSettingsDto>
                {
                    Success = true,
                    Message = "Cài đặt chi nhánh.",
                    Data = new BranchSettingsDto
                    {
                        BranchId = branch.BranchId,
                        BranchName = branch.BranchName,
                        ReturnDaysAllowed = branch.ReturnDaysAllowed,
                        ExchangeDaysAllowed = branch.ExchangeDaysAllowed
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "Unauthorized.", Errors = new() { ex.Message } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi hệ thống.", Errors = new() { ex.Message } });
            }
        }

        /// <summary>
        /// PUT /api/owner/branches/{id}/settings
        /// Updates return/exchange policy settings for the branch.
        /// </summary>
        [HttpPut("{id:guid}/settings")]
        public async Task<IActionResult> UpdateBranchSettings(Guid id, [FromBody] UpdateBranchSettingsRequest dto)
        {
            try
            {
                var ownerUserId = GetOwnerUserIdFromClaim();

                if (!await IsOwnerOfBranch(ownerUserId, id))
                    return Forbid();

                var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
                if (branch == null)
                    return NotFound(new ApiResponse<object> { Success = false, Message = "Chi nhánh không tồn tại." });

                // Validate: if set, must be non-negative
                if (dto.ReturnDaysAllowed.HasValue && dto.ReturnDaysAllowed.Value < 0)
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Thời hạn trả hàng phải >= 0." });
                if (dto.ExchangeDaysAllowed.HasValue && dto.ExchangeDaysAllowed.Value < 0)
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Thời hạn đổi hàng phải >= 0." });

                // Only update if explicitly provided (setting to 0 disables the policy)
                if (dto.ReturnDaysAllowed.HasValue)
                    branch.ReturnDaysAllowed = dto.ReturnDaysAllowed.Value == 0 ? null : dto.ReturnDaysAllowed.Value;
                if (dto.ExchangeDaysAllowed.HasValue)
                    branch.ExchangeDaysAllowed = dto.ExchangeDaysAllowed.Value == 0 ? null : dto.ExchangeDaysAllowed.Value;

                branch.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<BranchSettingsDto>
                {
                    Success = true,
                    Message = "Cập nhật cài đặt chi nhánh thành công.",
                    Data = new BranchSettingsDto
                    {
                        BranchId = branch.BranchId,
                        BranchName = branch.BranchName,
                        ReturnDaysAllowed = branch.ReturnDaysAllowed,
                        ExchangeDaysAllowed = branch.ExchangeDaysAllowed
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object> { Success = false, Message = "Unauthorized.", Errors = new() { ex.Message } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi hệ thống.", Errors = new() { ex.Message } });
            }
        }

        private static OwnerBranchDto MapToDto(Branch branch)
        {
            return new OwnerBranchDto
            {
                BranchId = branch.BranchId,
                BranchName = branch.BranchName,
                BranchCode = branch.BranchCode,
                Phone = branch.Phone,
                City = branch.City,
                Address = branch.Address,
                Type = branch.Type,
                ManagerUserId = branch.ManagerUserId,
                ManagerFullName = branch.ManagerUser?.FullName,
                ManagerEmail = branch.ManagerUser?.Email,
                IsActive = branch.IsActive,
                CreatedAt = branch.CreatedAt,
                UpdatedAt = branch.UpdatedAt
            };
        }
    }
}
