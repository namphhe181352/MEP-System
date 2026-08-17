using System.Text.Json;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.OwnerServices;

public class OwnerBranchService : IOwnerBranchService
{
    private readonly MEPDatabaseContext _context;
    private static readonly JsonSerializerOptions _caseInsensitiveJsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions _camelCaseJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public OwnerBranchService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<List<OwnerBranchDto>> GetBranchesAsync(Guid ownerUserId)
    {
        var branches = await _context.Branches
            .Include(b => b.ManagerUser)
            .Where(b => b.StaffAssignments.Any(sa => sa.UserId == ownerUserId) && b.IsActive == 1)
            .ToListAsync();

        if (!branches.Any())
            throw new KeyNotFoundException("Không tìm thấy chi nhánh nào.");

        return branches.Select(MapToDto).ToList();
    }

    public async Task<OwnerBranchDto> GetBranchByIdAsync(Guid ownerUserId, Guid branchId)
    {
        if (!await IsOwnerOfBranch(ownerUserId, branchId))
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập chi nhánh này.");

        var branch = await _context.Branches
            .Include(b => b.ManagerUser)
            .FirstOrDefaultAsync(b => b.BranchId == branchId && b.IsActive == 1);

        if (branch == null)
            throw new KeyNotFoundException("Chi nhánh không tồn tại.");

        return MapToDto(branch);
    }

    public async Task<OwnerBranchDto> CreateBranchAsync(Guid ownerUserId, CreateOwnerBranchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BranchName))
            throw new BusinessException("Tên chi nhánh là bắt buộc.");

        if (!string.IsNullOrWhiteSpace(request.BranchCode))
        {
            var codeExists = await _context.Branches.AnyAsync(b => b.BranchCode == request.BranchCode.Trim());
            if (codeExists)
                throw new BusinessException("Mã chi nhánh đã tồn tại.");
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var branch = new Branch
        {
            BranchId = Guid.NewGuid(),
            BranchName = request.BranchName.Trim(),
            BranchCode = request.BranchCode?.Trim(),
            Phone = request.Phone?.Trim(),
            City = request.City?.Trim(),
            Address = request.Address?.Trim(),
            Type = request.Type?.Trim(),
            ManagerUserId = ownerUserId,
            IsActive = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using var transaction = await _context.Database.BeginTransactionAsync();

        _context.Branches.Add(branch);

        _context.StaffAssignments.Add(new StaffAssignment
        {
            AssignmentId = Guid.NewGuid(),
            UserId = ownerUserId,
            BranchId = branch.BranchId,
            AssignedAt = now
        });

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        await _context.Entry(branch).Reference(b => b.ManagerUser).LoadAsync();

        return MapToDto(branch);
    }

    public async Task<OwnerBranchDto> UpdateBranchAsync(Guid ownerUserId, Guid branchId, UpdateOwnerBranchRequest request)
    {
        if (!await IsOwnerOfBranch(ownerUserId, branchId))
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập chi nhánh này.");

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);
        if (branch == null)
            throw new KeyNotFoundException("Chi nhánh không tồn tại.");

        if (!string.IsNullOrWhiteSpace(request.BranchName)) branch.BranchName = request.BranchName.Trim();
        if (!string.IsNullOrWhiteSpace(request.BranchCode)) branch.BranchCode = request.BranchCode.Trim();
        if (!string.IsNullOrWhiteSpace(request.Phone)) branch.Phone = request.Phone.Trim();
        if (!string.IsNullOrWhiteSpace(request.City)) branch.City = request.City.Trim();
        if (!string.IsNullOrWhiteSpace(request.Address)) branch.Address = request.Address.Trim();
        if (!string.IsNullOrWhiteSpace(request.Type)) branch.Type = request.Type.Trim();
        if (request.IsActive.HasValue) branch.IsActive = request.IsActive.Value;
        branch.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync();

        await _context.Entry(branch).Reference(b => b.ManagerUser).LoadAsync();

        return MapToDto(branch);
    }

    public async Task<PaginatedResponse<OwnerBranchHistoryDto>> GetBranchHistoryAsync(
        Guid ownerUserId, Guid branchId, int page, int pageSize, string? type, string? status)
    {
        if (!await IsOwnerOfBranch(ownerUserId, branchId))
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập chi nhánh này.");

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.StockTickets
            .Include(st => st.User)
            .Where(st => st.BranchId == branchId)
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

        return new PaginatedResponse<OwnerBranchHistoryDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize),
            HasNextPage = page * pageSize < total,
            HasPreviousPage = page > 1
        };
    }

    public async Task<BranchSettingsDto> GetBranchSettingsAsync(Guid ownerUserId, Guid branchId)
    {
        if (!await IsOwnerOfBranch(ownerUserId, branchId))
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập chi nhánh này.");

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);
        if (branch == null)
            throw new KeyNotFoundException("Chi nhánh không tồn tại.");

        return new BranchSettingsDto
        {
            BranchId = branch.BranchId,
            BranchName = branch.BranchName,
            ReturnDaysAllowed = branch.ReturnDaysAllowed,
            ExchangeDaysAllowed = branch.ExchangeDaysAllowed
        };
    }

    public async Task<BranchSettingsDto> UpdateBranchSettingsAsync(Guid ownerUserId, Guid branchId, UpdateBranchSettingsRequest request)
    {
        if (!await IsOwnerOfBranch(ownerUserId, branchId))
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập chi nhánh này.");

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId);
        if (branch == null)
            throw new KeyNotFoundException("Chi nhánh không tồn tại.");

        if (request.ReturnDaysAllowed.HasValue && request.ReturnDaysAllowed.Value < 0)
            throw new BusinessException("Thời hạn trả hàng phải >= 0.");
        if (request.ExchangeDaysAllowed.HasValue && request.ExchangeDaysAllowed.Value < 0)
            throw new BusinessException("Thời hạn đổi hàng phải >= 0.");

        if (request.ReturnDaysAllowed.HasValue)
            branch.ReturnDaysAllowed = request.ReturnDaysAllowed.Value == 0 ? null : request.ReturnDaysAllowed.Value;
        if (request.ExchangeDaysAllowed.HasValue)
            branch.ExchangeDaysAllowed = request.ExchangeDaysAllowed.Value == 0 ? null : request.ExchangeDaysAllowed.Value;

        branch.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync();

        return new BranchSettingsDto
        {
            BranchId = branch.BranchId,
            BranchName = branch.BranchName,
            ReturnDaysAllowed = branch.ReturnDaysAllowed,
            ExchangeDaysAllowed = branch.ExchangeDaysAllowed
        };
    }

    public async Task<List<CategoryPolicyDto>> GetCategoryPoliciesAsync(Guid ownerUserId, Guid branchId)
    {
        if (!await IsOwnerOfBranch(ownerUserId, branchId))
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập chi nhánh này.");

        var branchExists = await _context.Branches.AnyAsync(b => b.BranchId == branchId);
        if (!branchExists)
            throw new KeyNotFoundException("Chi nhánh không tồn tại.");

        var config = await _context.SystemConfigs
            .FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "CATEGORY_RETURN_POLICIES");

        var policies = new List<CategoryPolicyDto>();
        if (config != null && !string.IsNullOrWhiteSpace(config.ConfigValue))
        {
            try
            {
                var deserialized = JsonSerializer.Deserialize<List<CategoryPolicyDto>>(config.ConfigValue, _caseInsensitiveJsonOptions);
                if (deserialized != null) policies = deserialized;
            }
            catch { }
        }

        var categories = await _context.ProductTaxonomies
            .Where(t => t.Type == "Category" && (t.BranchId == null || t.BranchId == branchId))
            .ToListAsync();

        var activeCategoryNames = categories
            .Select(c => c.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var cleaned = policies
            .Where(p => string.IsNullOrEmpty(p.CategoryName) || activeCategoryNames.Contains(p.CategoryName))
            .ToList();

        if (cleaned.Count != policies.Count && config != null)
        {
            config.ConfigValue = JsonSerializer.Serialize(cleaned, _camelCaseJsonOptions);
            _context.SystemConfigs.Update(config);
            await _context.SaveChangesAsync();
            policies = cleaned;
        }

        foreach (var policy in policies)
        {
            if (policy.CategoryId == null && !string.IsNullOrEmpty(policy.CategoryName))
            {
                var match = categories.FirstOrDefault(c =>
                    c.Name.Equals(policy.CategoryName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    policy.CategoryId = match.TaxonomyId;
            }
        }

        return policies;
    }

    public async Task UpdateCategoryPoliciesAsync(Guid ownerUserId, Guid branchId, UpdateCategoryPoliciesRequest request)
    {
        if (!await IsOwnerOfBranch(ownerUserId, branchId))
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập chi nhánh này.");

        var branchExists = await _context.Branches.AnyAsync(b => b.BranchId == branchId);
        if (!branchExists)
            throw new KeyNotFoundException("Chi nhánh không tồn tại.");

        var config = await _context.SystemConfigs
            .FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "CATEGORY_RETURN_POLICIES");

        var jsonValue = JsonSerializer.Serialize(request.Policies ?? new List<CategoryPolicyDto>(), _camelCaseJsonOptions);

        if (config == null)
        {
            config = new SystemConfig
            {
                ConfigId = Guid.NewGuid(),
                BranchId = branchId,
                ConfigKey = "CATEGORY_RETURN_POLICIES",
                ConfigValue = jsonValue
            };
            _context.SystemConfigs.Add(config);
        }
        else
        {
            config.ConfigValue = jsonValue;
            _context.SystemConfigs.Update(config);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<object> GetReturnDiscountAsync(Guid ownerUserId, Guid branchId)
    {
        if (!await IsOwnerOfBranch(ownerUserId, branchId))
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập chi nhánh này.");

        var branchExists = await _context.Branches.AnyAsync(b => b.BranchId == branchId);
        if (!branchExists)
            throw new KeyNotFoundException("Chi nhánh không tồn tại.");

        var config = await _context.SystemConfigs
            .FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "RETURN_DISCOUNT_PERCENT");

        decimal? percent = null;
        if (config != null && !string.IsNullOrWhiteSpace(config.ConfigValue))
        {
            if (decimal.TryParse(config.ConfigValue, System.Globalization.CultureInfo.InvariantCulture, out var p))
                percent = p;
        }

        return new { returnDiscountPercent = percent };
    }

    public async Task<object> UpdateReturnDiscountAsync(Guid ownerUserId, Guid branchId, UpdateReturnDiscountRequest request)
    {
        if (!await IsOwnerOfBranch(ownerUserId, branchId))
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập chi nhánh này.");

        var branchExists = await _context.Branches.AnyAsync(b => b.BranchId == branchId);
        if (!branchExists)
            throw new KeyNotFoundException("Chi nhánh không tồn tại.");

        if (request.ReturnDiscountPercent.HasValue &&
            (request.ReturnDiscountPercent.Value < 0 || request.ReturnDiscountPercent.Value > 100))
        {
            throw new BusinessException("Chiết khấu trả hàng phải từ 0 đến 100 (%)}.");
        }

        var config = await _context.SystemConfigs
            .FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "RETURN_DISCOUNT_PERCENT");

        var value = request.ReturnDiscountPercent.HasValue
            ? request.ReturnDiscountPercent.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "";

        if (config == null)
        {
            config = new SystemConfig
            {
                ConfigId = Guid.NewGuid(),
                BranchId = branchId,
                ConfigKey = "RETURN_DISCOUNT_PERCENT",
                ConfigValue = value
            };
            _context.SystemConfigs.Add(config);
        }
        else
        {
            config.ConfigValue = value;
            _context.SystemConfigs.Update(config);
        }

        await _context.SaveChangesAsync();

        return new { returnDiscountPercent = request.ReturnDiscountPercent };
    }

    private async Task<bool> IsOwnerOfBranch(Guid ownerUserId, Guid branchId)
    {
        return await _context.StaffAssignments
            .AnyAsync(sa => sa.UserId == ownerUserId && sa.BranchId == branchId);
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
