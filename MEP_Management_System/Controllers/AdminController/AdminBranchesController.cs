using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.AdminController
{
    [ApiController]
    [Route("api/admin/branches")]
    [Authorize(Roles = "Admin")]
    [Tags("Admin (Branches)")]
    public class AdminBranchesController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;

        public AdminBranchesController(MEPDatabaseContext context)
        {
            _context = context;
        }

        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;
            return Guid.TryParse(userIdClaim, out var id) ? id : null;
        }

        private Task LogAction(Guid targetEntityId, string action, string tableName, string entityType, string description)
        {
            var adminId = GetCurrentUserId();
            if (adminId == null) return Task.CompletedTask;

            var log = new SystemLog
            {
                LogId = Guid.NewGuid(),
                UserId = adminId.Value,
                Action = action,
                TableName = tableName,
                EntityType = entityType,
                EntityId = targetEntityId,
                Description = description,
                Level = "INFO",
                Source = "AdminService",
                Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };
            _context.SystemLogs.Add(log);
            return Task.CompletedTask;
        }

        [HttpGet]
        public async Task<IActionResult> GetBranches(
            [FromQuery] string? search,
            [FromQuery] short? isActive,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _context.Branches.Include(b => b.ManagerUser).AsQueryable();

            if (isActive.HasValue)
                query = query.Where(b => b.IsActive == isActive.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var t = search.Trim().ToLower();
                query = query.Where(b => b.BranchName.ToLower().Contains(t) || 
                                         (b.BranchCode != null && b.BranchCode.ToLower().Contains(t)));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new PageResultDto<AdminBranchDto>
            {
                Items = items.Select(MapToDto).ToList(),
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetBranchById(Guid id)
        {
            var branch = await _context.Branches
                .Include(b => b.ManagerUser)
                .FirstOrDefaultAsync(b => b.BranchId == id);

            if (branch == null)
                return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Chi nhánh không tồn tại." } });

            return Ok(MapToDto(branch));
        }

        [HttpPost]
        public async Task<IActionResult> CreateBranch([FromBody] CreateAdminBranchRequest dto)
        {
            if (string.IsNullOrWhiteSpace(dto.BranchName))
                return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Tên chi nhánh là bắt buộc." } });

            if (!string.IsNullOrWhiteSpace(dto.BranchCode))
            {
                var codeExists = await _context.Branches.AnyAsync(b => b.BranchCode == dto.BranchCode.Trim());
                if (codeExists)
                    return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Mã chi nhánh đã tồn tại." } });
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

            if (dto.ManagerUserId.HasValue)
            {
                _context.StaffAssignments.Add(new StaffAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    UserId = dto.ManagerUserId.Value,
                    BranchId = branch.BranchId,
                    AssignedAt = now
                });
            }

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

            await LogAction(branch.BranchId, "CREATE_BRANCH", "branches", "Branch", $"Tạo chi nhánh: {branch.BranchName}");

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(nameof(GetBranchById), new { id = branch.BranchId }, MapToDto(branch));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateBranch(Guid id, [FromBody] UpdateAdminBranchRequest dto)
        {
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
            if (branch == null)
                return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Chi nhánh không tồn tại." } });

            if (!string.IsNullOrWhiteSpace(dto.BranchName)) branch.BranchName = dto.BranchName.Trim();
            
            if (!string.IsNullOrWhiteSpace(dto.BranchCode))
            {
                var trimmedCode = dto.BranchCode.Trim();
                if (trimmedCode != branch.BranchCode)
                {
                    var exists = await _context.Branches.AnyAsync(b => b.BranchCode == trimmedCode);
                    if (exists) return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Mã chi nhánh đã tồn tại." } });
                    branch.BranchCode = trimmedCode;
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Phone)) branch.Phone = dto.Phone.Trim();
            if (!string.IsNullOrWhiteSpace(dto.City)) branch.City = dto.City.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Address)) branch.Address = dto.Address.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Type)) branch.Type = dto.Type.Trim();
            if (dto.ManagerUserId.HasValue) branch.ManagerUserId = dto.ManagerUserId.Value;
            if (dto.IsActive.HasValue) branch.IsActive = dto.IsActive.Value;
            
            branch.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            await LogAction(branch.BranchId, "UPDATE_BRANCH", "branches", "Branch", $"Cập nhật chi nhánh: {branch.BranchName}");
            await _context.SaveChangesAsync();

            return Ok(MapToDto(branch));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteBranch(Guid id)
        {
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
            if (branch == null)
                return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Chi nhánh không tồn tại." } });

            branch.IsActive = 0;
            branch.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            await LogAction(branch.BranchId, "DELETE_BRANCH", "branches", "Branch", $"Xóa (soft) chi nhánh: {branch.BranchName}");
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa chi nhánh thành công." });
        }

        [HttpGet("{id:guid}/users")]
        public async Task<IActionResult> GetBranchUsers(Guid id)
        {
            var exists = await _context.Branches.AnyAsync(b => b.BranchId == id);
            if (!exists)
                return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Chi nhánh không tồn tại." } });

            var users = await _context.StaffAssignments
                .Include(sa => sa.User)
                .ThenInclude(u => u.Roles)
                .Where(sa => sa.BranchId == id)
                .Select(sa => new BranchUserDto
                {
                    StaffAssignmentId = sa.AssignmentId,
                    UserId = sa.UserId,
                    FullName = sa.User.FullName,
                    Email = sa.User.Email,
                    Role = sa.User.Roles.FirstOrDefault().RoleName,
                    IsManager = sa.User.UserId == sa.Branch.ManagerUserId ? (short)1 : (short)0,
                    AssignedAt = sa.AssignedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPut("{id:guid}/users")]
        public async Task<IActionResult> AssignUserToBranch(Guid id, [FromBody] AssignUserToBranchRequest dto)
        {
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
            if (branch == null)
                return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Chi nhánh không tồn tại." } });

            var userExists = await _context.Users.AnyAsync(u => u.UserId == dto.UserId);
            if (!userExists)
                return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "User không tồn tại." } });

            var assignment = await _context.StaffAssignments
                .FirstOrDefaultAsync(sa => sa.BranchId == id && sa.UserId == dto.UserId);

            if (assignment == null)
            {
                assignment = new StaffAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    UserId = dto.UserId,
                    BranchId = id,
                    AssignedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
                };
                _context.StaffAssignments.Add(assignment);
            }

            if (dto.IsManager)
            {
                branch.ManagerUserId = dto.UserId;
            }

            await LogAction(branch.BranchId, "ASSIGN_USER", "staff_assignments", "Branch", $"Gán user {dto.UserId} vào chi nhánh {branch.BranchName}");
            await _context.SaveChangesAsync();

            return Ok(new { message = "Gán người dùng thành công." });
        }

        [HttpDelete("{id:guid}/users/{userId:guid}")]
        public async Task<IActionResult> RemoveUserFromBranch(Guid id, Guid userId)
        {
            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
            if (branch == null)
                return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Chi nhánh không tồn tại." } });

            var assignment = await _context.StaffAssignments
                .FirstOrDefaultAsync(sa => sa.BranchId == id && sa.UserId == userId);

            if (assignment == null)
                return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Người dùng không thuộc chi nhánh này." } });

            _context.StaffAssignments.Remove(assignment);

            if (branch.ManagerUserId == userId)
            {
                branch.ManagerUserId = null;
            }

            await LogAction(branch.BranchId, "REMOVE_USER", "staff_assignments", "Branch", $"Xóa user {userId} khỏi chi nhánh {branch.BranchName}");
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa người dùng thành công." });
        }

        private static AdminBranchDto MapToDto(Branch branch)
        {
            return new AdminBranchDto
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
