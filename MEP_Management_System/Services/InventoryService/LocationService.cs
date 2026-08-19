using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.InventoryService;

public class LocationService : ILocationService
{
    private readonly MEPDatabaseContext _context;

    public LocationService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<List<ProductLocation>> GetLocationsAsync(Guid? branchId, bool? activeOnly)
    {
        var query = _context.ProductLocations.Where(l => l.BranchId == branchId).AsQueryable();

        if (activeOnly == true)
        {
            query = query.Where(l => l.IsActive == 1);
        }

        return await query.OrderBy(l => l.LocationName).ToListAsync();
    }

    public async Task<ProductLocation?> GetLocationByIdAsync(Guid id)
    {
        return await _context.ProductLocations.FindAsync(id);
    }

    public async Task<ProductLocation> CreateLocationAsync(CreateLocationDto dto, Guid? branchId, Guid? userId)
    {
        if (string.IsNullOrWhiteSpace(dto.LocationName))
            throw new BusinessException("Tên vị trí là bắt buộc.");

        var existing = await _context.ProductLocations
            .FirstOrDefaultAsync(l => l.LocationName.ToLower() == dto.LocationName.Trim().ToLower() && l.BranchId == branchId);

        if (existing != null)
        {
            if (existing.IsActive == 1)
                throw new BusinessException("Tên vị trí đã tồn tại trong chi nhánh này.");
            else
            {
                existing.IsActive = dto.IsActive;
                existing.LocationCode = dto.LocationCode?.Trim() ?? existing.LocationCode;
                existing.Description = dto.Description?.Trim() ?? existing.Description;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = userId;
                await _context.SaveChangesAsync();
                return existing;
            }
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
            CreatedBy = userId
        };

        _context.ProductLocations.Add(location);
        await _context.SaveChangesAsync();

        return location;
    }

    public async Task<ProductLocation> UpdateLocationAsync(Guid id, UpdateLocationDto dto, Guid? branchId, Guid? userId)
    {
        var location = await _context.ProductLocations.FindAsync(id);
        if (location == null || (location.BranchId != null && location.BranchId != branchId))
            throw new KeyNotFoundException("Không tìm thấy vị trí hoặc bạn không có quyền sửa vị trí này.");

        if (!string.IsNullOrWhiteSpace(dto.LocationName))
        {
            var existing = await _context.ProductLocations
                .FirstOrDefaultAsync(l => l.LocationName.ToLower() == dto.LocationName.Trim().ToLower() && l.LocationId != id && l.BranchId == branchId && l.IsActive == 1);

            if (existing != null)
                throw new BusinessException("Tên vị trí đã tồn tại trong chi nhánh này.");

            location.LocationName = dto.LocationName.Trim();
        }

        if (dto.LocationCode != null)
            location.LocationCode = dto.LocationCode.Trim();

        if (dto.Description != null)
            location.Description = dto.Description.Trim();

        if (dto.IsActive.HasValue)
            location.IsActive = dto.IsActive.Value;

        location.UpdatedAt = DateTime.UtcNow;
        location.UpdatedBy = userId;

        await _context.SaveChangesAsync();
        return location;
    }

    public async Task<bool> DeleteLocationAsync(Guid id, Guid? branchId)
    {
        var location = await _context.ProductLocations.FindAsync(id);
        if (location == null || (location.BranchId != null && location.BranchId != branchId))
            throw new KeyNotFoundException("Không tìm thấy vị trí.");

        location.IsActive = 0;
        location.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }
}
