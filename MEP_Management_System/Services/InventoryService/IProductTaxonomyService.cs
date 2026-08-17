using MEP_Management_System.Models;

namespace MEP_Management_System.Services.InventoryService;

public interface IProductTaxonomyService
{
    Task<List<ProductTaxonomy>> GetTaxonomiesAsync(Guid? branchId, string? type);
    Task<ProductTaxonomy?> GetTaxonomyByIdAsync(Guid id);
    Task<ProductTaxonomy> CreateTaxonomyAsync(CreateProductTaxonomyDto dto, Guid? branchId);
    Task<ProductTaxonomy?> UpdateTaxonomyAsync(Guid id, UpdateProductTaxonomyDto dto, Guid? branchId);
    Task<bool> DeleteTaxonomyAsync(Guid id, Guid? branchId);
}

public class CreateProductTaxonomyDto
{
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
}

public class UpdateProductTaxonomyDto
{
    public string Name { get; set; } = null!;
}
