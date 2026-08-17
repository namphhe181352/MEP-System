using MEP_Management_System.Models;

namespace MEP_Management_System.Services.InventoryService;

public interface ILocationService
{
    Task<List<ProductLocation>> GetLocationsAsync(Guid? branchId, bool? activeOnly);
    Task<ProductLocation?> GetLocationByIdAsync(Guid id);
    Task<ProductLocation> CreateLocationAsync(CreateLocationDto dto, Guid? branchId, Guid? userId);
    Task<ProductLocation> UpdateLocationAsync(Guid id, UpdateLocationDto dto, Guid? branchId, Guid? userId);
    Task<bool> DeleteLocationAsync(Guid id, Guid? branchId);
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
