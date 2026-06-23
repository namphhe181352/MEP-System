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

        private Guid GetBranchIdFromClaim()
        {
            var claimValue = User.FindFirst("branchId")?.Value;
            if (string.IsNullOrWhiteSpace(claimValue) || !Guid.TryParse(claimValue, out var branchId))
                throw new UnauthorizedAccessException("Branch claim is missing or invalid.");

            return branchId;
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
                var branchId = GetBranchIdFromClaim();

                var branch = await _context.Branches
                    .Include(b => b.ManagerUser)
                    .FirstOrDefaultAsync(b => b.BranchId == branchId && b.IsActive == 1);

                if (branch == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Branch not found for current owner.",
                        Errors = new() { "No active branch is assigned to this owner." }
                    });
                }

                return Ok(new ApiResponse<OwnerBranchDto>
                {
                    Success = true,
                    Message = "Owner branch retrieved successfully.",
                    Data = MapToDto(branch)
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Unauthorized.",
                    Errors = new() { ex.Message }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Unable to load branch data.",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// GET /api/owner/branches/{id}
        /// Returns a specific branch if it belongs to the current Owner.
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetBranchById(Guid id)
        {
            try
            {
                var branchId = GetBranchIdFromClaim();
                if (branchId != id)
                {
                    return Forbid();
                }

                var branch = await _context.Branches
                    .Include(b => b.ManagerUser)
                    .FirstOrDefaultAsync(b => b.BranchId == id && b.IsActive == 1);

                if (branch == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Branch not found.",
                        Errors = new() { "No active branch found for the requested id." }
                    });
                }

                return Ok(new ApiResponse<OwnerBranchDto>
                {
                    Success = true,
                    Message = "Branch details loaded.",
                    Data = MapToDto(branch)
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Unauthorized.",
                    Errors = new() { ex.Message }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to load branch.",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// POST /api/owner/branches
        /// Creates a new branch and initializes inventory rows for all products.
        ///</summary>
        [HttpPost]
        public async Task<IActionResult> CreateBranch([FromBody] CreateOwnerBranchRequest dto)
        {
            try
            {
                var branchId = GetBranchIdFromClaim();
                var assignedBranch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);
                if (assignedBranch != null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Current owner already has an assigned branch.",
                        Errors = new() { "Please use update to modify your existing branch." }
                    });
                }

                if (string.IsNullOrWhiteSpace(dto.BranchName))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Branch name is required.",
                        Errors = new() { "BranchName cannot be empty." }
                    });
                }

                var existingCode = !string.IsNullOrWhiteSpace(dto.BranchCode)
                    ? await _context.Branches.AnyAsync(b => b.BranchCode == dto.BranchCode.Trim())
                    : false;
                if (existingCode)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Branch code already exists.",
                        Errors = new() { "Please provide a unique branch code." }
                    });
                }

                User? managerUser = null;
                if (dto.ManagerUserId.HasValue)
                {
                    managerUser = await _context.Users.FindAsync(dto.ManagerUserId.Value);
                    if (managerUser == null)
                    {
                        return NotFound(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Branch manager user not found.",
                            Errors = new() { "Manager user does not exist." }
                        });
                    }
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
                    ManagerUserId = dto.ManagerUserId,
                    IsActive = 1,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                await using var transaction = await _context.Database.BeginTransactionAsync();

                _context.Branches.Add(branch);

                var products = await _context.Products
                    .Select(p => new { p.ProductId })
                    .ToListAsync();

                foreach (var product in products)
                {
                    var branchProduct = new BranchProduct
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
                    };
                    _context.BranchProducts.Add(branchProduct);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetBranchById), new { id = branch.BranchId }, new ApiResponse<OwnerBranchDto>
                {
                    Success = true,
                    Message = "Owner branch created and inventory initialized.",
                    Data = MapToDto(branch)
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Unauthorized.",
                    Errors = new() { ex.Message }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to create branch.",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// PUT /api/owner/branches/{id}
        /// Updates the branch metadata for the current owner.
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateBranch(Guid id, [FromBody] UpdateOwnerBranchRequest dto)
        {
            try
            {
                var branchId = GetBranchIdFromClaim();
                if (branchId != id)
                {
                    return Forbid();
                }

                var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
                if (branch == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Branch not found.",
                        Errors = new() { "Requested branch does not exist." }
                    });
                }

                if (!string.IsNullOrWhiteSpace(dto.BranchName))
                    branch.BranchName = dto.BranchName.Trim();
                if (!string.IsNullOrWhiteSpace(dto.BranchCode))
                    branch.BranchCode = dto.BranchCode.Trim();
                if (!string.IsNullOrWhiteSpace(dto.Phone))
                    branch.Phone = dto.Phone.Trim();
                if (!string.IsNullOrWhiteSpace(dto.City))
                    branch.City = dto.City.Trim();
                if (!string.IsNullOrWhiteSpace(dto.Address))
                    branch.Address = dto.Address.Trim();
                if (!string.IsNullOrWhiteSpace(dto.Type))
                    branch.Type = dto.Type.Trim();
                if (dto.ManagerUserId.HasValue)
                    branch.ManagerUserId = dto.ManagerUserId;
                if (dto.IsActive.HasValue)
                    branch.IsActive = dto.IsActive.Value;

                branch.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<OwnerBranchDto>
                {
                    Success = true,
                    Message = "Branch updated successfully.",
                    Data = MapToDto(branch)
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Unauthorized.",
                    Errors = new() { ex.Message }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to update branch.",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// GET /api/owner/branches/{id}/history
        /// Returns branch stock ticket history for the current owner.
        /// </summary>
        [HttpGet("{id:guid}/history")]
        public async Task<IActionResult> GetBranchHistory(Guid id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? type = null,
            [FromQuery] string? status = null)
        {
            try
            {
                var branchId = GetBranchIdFromClaim();
                if (branchId != id)
                {
                    return Forbid();
                }

                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100;

                var query = _context.StockTickets
                    .Include(st => st.User)
                    .Where(st => st.BranchId == id);

                if (!string.IsNullOrWhiteSpace(type))
                {
                    query = query.Where(st => st.TicketType != null && st.TicketType.ToLower() == type.Trim().ToLower());
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(st => st.Status != null && st.Status.ToLower() == status.Trim().ToLower());
                }

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
                    Message = "Branch history loaded.",
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
                return Unauthorized(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Unauthorized.",
                    Errors = new() { ex.Message }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to load branch history.",
                    Errors = new() { ex.Message }
                });
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
