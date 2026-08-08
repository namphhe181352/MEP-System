using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.AdminDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
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
        private readonly MepNotificationService _notify;

        public AdminBranchesController(MEPDatabaseContext context, MepNotificationService notify)
        {
            _context = context;
            _notify = notify;
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
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

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
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var branchCode = dto.BranchCode?.Trim();
            var phone = dto.Phone?.Trim();
            var address = dto.Address?.Trim();

            if (!string.IsNullOrWhiteSpace(branchCode))
            {
                var codeExists = await _context.Branches.AnyAsync(b => b.BranchCode != null && b.BranchCode.ToLower() == branchCode.ToLower());
                if (codeExists)
                    return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Mã chi nhánh đã tồn tại." } });
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                var phoneExists = await _context.Branches.AnyAsync(b => b.Phone != null && b.Phone.ToLower() == phone.ToLower());
                if (phoneExists)
                    return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Số điện thoại đã tồn tại trong hệ thống." } });
            }

            if (!string.IsNullOrWhiteSpace(address))
            {
                var addressExists = await _context.Branches.AnyAsync(b => b.Address != null && b.Address.ToLower() == address.ToLower());
                if (addressExists)
                    return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Địa chỉ đã tồn tại trong hệ thống." } });
            }

            var branchNameTrimmed = dto.BranchName?.Trim();
            if (!string.IsNullOrWhiteSpace(branchNameTrimmed))
            {
                var nameExists = await _context.Branches.AnyAsync(b => b.BranchName.ToLower() == branchNameTrimmed.ToLower());
                if (nameExists)
                    return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Tên cửa hàng đã tồn tại." } });
            }

            if (dto.ManagerUserId.HasValue)
            {
                var ownerBranchExists = await _context.Branches.AnyAsync(b => b.ManagerUserId == dto.ManagerUserId.Value);
                if (ownerBranchExists)
                    return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Chủ cửa hàng này đã được gán cho một cửa hàng khác." } });
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

            // Lưu ý: KHÔNG seed global products cho branch mới — mỗi cửa hàng tự quản lý catalog riêng.
            // Owner phải tự thêm sản phẩm sau khi tạo branch.



            await LogAction(branch.BranchId, "CREATE_BRANCH", "branches", "Branch", $"Tạo chi nhánh: {branch.BranchName}");

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(nameof(GetBranchById), new { id = branch.BranchId }, MapToDto(branch));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateBranch(Guid id, [FromBody] UpdateAdminBranchRequest dto)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == id);
            if (branch == null)
                return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Chi nhánh không tồn tại." } });

            if (!string.IsNullOrWhiteSpace(dto.BranchName)) branch.BranchName = dto.BranchName.Trim();

            if (!string.IsNullOrWhiteSpace(dto.BranchCode))
            {
                var trimmedCode = dto.BranchCode.Trim();
                if (trimmedCode != branch.BranchCode)
                {
                    var exists = await _context.Branches.AnyAsync(b => b.BranchId != id && b.BranchCode != null && b.BranchCode.ToLower() == trimmedCode.ToLower());
                    if (exists) return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Mã chi nhánh đã tồn tại." } });
                    branch.BranchCode = trimmedCode;
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Phone))
            {
                var normalizedPhone = dto.Phone.Trim();
                var phoneExists = await _context.Branches.AnyAsync(b => b.BranchId != id && b.Phone != null && b.Phone.ToLower() == normalizedPhone.ToLower());
                if (phoneExists)
                    return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Số điện thoại đã tồn tại trong hệ thống." } });

                branch.Phone = normalizedPhone;
            }

            if (!string.IsNullOrWhiteSpace(dto.City))
            {
                branch.City = dto.City.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dto.Address))
            {
                var normalizedAddress = dto.Address.Trim();
                var addressExists = await _context.Branches.AnyAsync(b => b.BranchId != id && b.Address != null && b.Address.ToLower() == normalizedAddress.ToLower());
                if (addressExists)
                    return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Địa chỉ đã tồn tại trong hệ thống." } });

                branch.Address = normalizedAddress;
            }

            if (!string.IsNullOrWhiteSpace(dto.Type))
            {
                branch.Type = dto.Type.Trim();
            }

            if (dto.ManagerUserId.HasValue)
            {
                var ownerBranchExists = await _context.Branches.AnyAsync(b => b.BranchId != id && b.ManagerUserId == dto.ManagerUserId.Value);
                if (ownerBranchExists)
                    return BadRequest(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-400", Message = "Chủ cửa hàng này đã được gán cho một cửa hàng khác." } });

                branch.ManagerUserId = dto.ManagerUserId.Value;
            }

            if (dto.IsActive.HasValue) branch.IsActive = dto.IsActive.Value;

            branch.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            await LogAction(branch.BranchId, "UPDATE_BRANCH", "branches", "Branch", $"Cập nhật chi nhánh: {branch.BranchName}");
            await _context.SaveChangesAsync();

            // Thông báo cho chủ cửa hàng (nếu có)
            await SafeNotifyBranchChangeAsync(branch.ManagerUserId, branch.BranchId, branch.BranchName, "UPDATE_BRANCH",
                $"Cửa hàng \"{branch.BranchName}\" của bạn vừa được cập nhật bởi quản trị viên.");

            return Ok(MapToDto(branch));
        }

        /// <summary>
        /// Xóa nháp (soft delete): chỉ chuyển IsActive = 0. Vẫn chặn nếu còn dữ liệu nghiệp vụ.
        /// Sau khi xóa nháp, dùng endpoint DELETE /branches/{id}/permanent để xóa hẳn.
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteBranch(Guid id)
        {
            var branch = await _context.Branches
                .FirstOrDefaultAsync(b => b.BranchId == id);

            if (branch == null)
                return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Chi nhánh không tồn tại." } });

            if (branch.IsActive == 0)
                return Ok(new { message = "Chi nhánh đã ở trạng thái xóa nháp.", isActive = (short)0 });

            // Chặn xóa nháp nếu còn dữ liệu nghiệp vụ liên quan
            var relatedData = await CheckBranchHasBusinessDataAsync(id);
            if (relatedData.HasRelations)
            {
                return Conflict(new ErrorResponseDto
                {
                    Error = new ErrorDetail
                    {
                        Code = "MSG-409",
                        Message = relatedData.Message
                    }
                });
            }

            // Capture managerId trước khi xóa (nếu là hard delete còn giữ trong DB, soft delete vẫn giữ)
            var managerId = branch.ManagerUserId;
            var branchName = branch.BranchName;

            // Soft delete: đánh dấu IsActive = 0
            branch.IsActive = 0;
            branch.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            await LogAction(branch.BranchId, "SOFT_DELETE_BRANCH", "branches", "Branch", $"Xóa nháp chi nhánh: {branchName}");
            await _context.SaveChangesAsync();

            await SafeNotifyBranchChangeAsync(managerId, branch.BranchId, branchName, "SOFT_DELETE_BRANCH",
                $"Cửa hàng \"{branchName}\" của bạn vừa được tạm ngưng hoạt động bởi quản trị viên.");

            return Ok(new { message = "Đã xóa nháp chi nhánh. Bạn có thể khôi phục hoặc xóa hẳn.", isActive = (short)0 });
        }

        /// <summary>
        /// Khôi phục chi nhánh đã xóa nháp (IsActive = 0 → 1).
        /// </summary>
        [HttpPatch("{id:guid}/restore")]
        public async Task<IActionResult> RestoreBranch(Guid id)
        {
            var branch = await _context.Branches
                .FirstOrDefaultAsync(b => b.BranchId == id);

            if (branch == null)
                return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Chi nhánh không tồn tại." } });

            if (branch.IsActive == 1)
                return Ok(new { message = "Chi nhánh đang hoạt động.", isActive = (short)1 });

            branch.IsActive = 1;
            branch.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            await LogAction(branch.BranchId, "RESTORE_BRANCH", "branches", "Branch", $"Khôi phục chi nhánh: {branch.BranchName}");
            await _context.SaveChangesAsync();

            await SafeNotifyBranchChangeAsync(branch.ManagerUserId, branch.BranchId, branch.BranchName, "RESTORE_BRANCH",
                $"Cửa hàng \"{branch.BranchName}\" của bạn vừa được khôi phục hoạt động.");

            return Ok(new { message = "Khôi phục chi nhánh thành công.", isActive = (short)1 });
        }

        /// <summary>
        /// Xóa hẳn chi nhánh (hard delete). Chỉ áp dụng cho chi nhánh đã ở trạng thái xóa nháp (IsActive = 0).
        /// </summary>
        [HttpDelete("{id:guid}/permanent")]
        public async Task<IActionResult> HardDeleteBranch(Guid id)
        {
            var branch = await _context.Branches
                .FirstOrDefaultAsync(b => b.BranchId == id);

            if (branch == null)
                return NotFound(new ErrorResponseDto { Error = new ErrorDetail { Code = "MSG-404", Message = "Chi nhánh không tồn tại." } });

            if (branch.IsActive == 1)
            {
                return Conflict(new ErrorResponseDto
                {
                    Error = new ErrorDetail
                    {
                        Code = "MSG-409",
                        Message = "Vui lòng xóa nháp trước khi xóa hẳn."
                    }
                });
            }

            // Vẫn check business data lần cuối để an toàn
            var relatedData = await CheckBranchHasBusinessDataAsync(id);
            if (relatedData.HasRelations)
            {
                return Conflict(new ErrorResponseDto
                {
                    Error = new ErrorDetail
                    {
                        Code = "MSG-409",
                        Message = relatedData.Message
                    }
                });
            }

            // Capture managerId + branchName TRƯỚC khi xóa hẳn branch khỏi DB
            var managerId = branch.ManagerUserId;
            var branchName = branch.BranchName;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            // Xóa cascade các bảng phụ thuộc an toàn trước khi hard delete Branch
            await _context.StaffAssignments.Where(sa => sa.BranchId == id).ExecuteDeleteAsync();
            await _context.Shifts.Where(s => s.BranchId == id).ExecuteDeleteAsync();
            await _context.SystemConfigs.Where(c => c.BranchId == id).ExecuteDeleteAsync();
            await _context.InvoicePrintTemplates.Where(t => t.BranchId == id).ExecuteDeleteAsync();
            await _context.StockPrintTemplates.Where(t => t.BranchId == id).ExecuteDeleteAsync();
            await _context.ProductTaxonomies.Where(t => t.BranchId == id).ExecuteDeleteAsync();
            await _context.OrderDiscountTiers.Where(t => t.BranchId == id).ExecuteDeleteAsync();
            await _context.BranchProducts.Where(bp => bp.BranchId == id).ExecuteDeleteAsync();

            var deletedRows = await _context.Branches
                .Where(b => b.BranchId == id)
                .ExecuteDeleteAsync();

            if (deletedRows == 0)
            {
                await transaction.RollbackAsync();
                return Conflict(new ErrorResponseDto
                {
                    Error = new ErrorDetail
                    {
                        Code = "MSG-409",
                        Message = "Chi nhánh đang được xử lý bởi một yêu cầu khác. Vui lòng thử lại."
                    }
                });
            }

            await LogAction(id, "HARD_DELETE_BRANCH", "branches", "Branch", $"Xóa hẳn chi nhánh: {branchName}");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Notify SAU khi commit (branch name vẫn còn trong capture)
            await SafeNotifyBranchChangeAsync(managerId, id, branchName, "HARD_DELETE_BRANCH",
                $"Cửa hàng \"{branchName}\" của bạn đã bị xóa vĩnh viễn bởi quản trị viên.");

            return Ok(new { message = "Xóa hẳn chi nhánh thành công." });
        }

        /// <summary>
        /// Helper gọi MepNotificationService mà không làm fail request nếu SignalR lỗi.
        /// </summary>
        private async Task SafeNotifyBranchChangeAsync(Guid? ownerId, Guid branchId, string branchName, string action, string message)
        {
            try
            {
                await _notify.NotifyBranchChange(ownerId, branchId, action, branchName, message);
            }
            catch (Exception ex)
            {
                // Không fail mutation vì lỗi broadcast — chỉ log
                Console.WriteLine($"[AdminBranchesController] NotifyBranchChange failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Kiểm tra xem chi nhánh có dữ liệu nghiệp vụ liên quan không.
        /// Trả về true nếu có bất kỳ record nào trong các bảng FK IsRequired.
        /// </summary>
        private async Task<(bool HasRelations, string Message)> CheckBranchHasBusinessDataAsync(Guid branchId)
        {
            var ordersCount = await _context.Orders.CountAsync(o => o.BranchId == branchId);
            if (ordersCount > 0)
                return (true, $"Không thể xóa chi nhánh vì còn {ordersCount} đơn hàng liên quan. Vui lòng xử lý trước.");

            var stockTicketsCount = await _context.StockTickets.CountAsync(st => st.BranchId == branchId);
            if (stockTicketsCount > 0)
                return (true, $"Không thể xóa chi nhánh vì còn {stockTicketsCount} phiếu kho liên quan. Vui lòng xử lý trước.");

            var inventoryCheckTicketsCount = await _context.InventoryCheckTickets.CountAsync(t => t.BranchId == branchId);
            if (inventoryCheckTicketsCount > 0)
                return (true, $"Không thể xóa chi nhánh vì còn {inventoryCheckTicketsCount} phiếu kiểm kê liên quan. Vui lòng xử lý trước.");

            var notificationsCount = await _context.Notifications.CountAsync(n => n.BranchId == branchId);
            if (notificationsCount > 0)
                return (true, $"Không thể xóa chi nhánh vì còn {notificationsCount} thông báo liên quan. Vui lòng xử lý trước.");

            // BranchProducts có stock thực tế thì chặn, còn lại cho phép xóa cascade
            var branchProductsWithStock = await _context.BranchProducts
                .CountAsync(bp => bp.BranchId == branchId && (bp.ActualStock > 0 || bp.ReservedStock > 0));
            if (branchProductsWithStock > 0)
                return (true, $"Không thể xóa chi nhánh vì còn tồn kho thực tế ở {branchProductsWithStock} sản phẩm. Vui lòng xử lý trước.");

            return (false, string.Empty);
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
