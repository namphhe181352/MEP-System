using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.InventoryService;

public class ProductTaxonomyService : IProductTaxonomyService
{
    private readonly MEPDatabaseContext _context;

    public ProductTaxonomyService(MEPDatabaseContext context)
    {
        _context = context;
    }

    public async Task<List<ProductTaxonomy>> GetTaxonomiesAsync(Guid? branchId, string? type)
    {
        var query = _context.ProductTaxonomies
            .Where(t => (t.BranchId == branchId || t.BranchId == null));

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(t => t.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
        }

        return await query.OrderBy(t => t.Name).ToListAsync();
    }

    public async Task<ProductTaxonomy?> GetTaxonomyByIdAsync(Guid id)
    {
        return await _context.ProductTaxonomies.FindAsync(id);
    }

    public async Task<ProductTaxonomy> CreateTaxonomyAsync(CreateProductTaxonomyDto dto, Guid? branchId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BusinessException("Tên không được để trống.");

        if (string.IsNullOrWhiteSpace(dto.Type))
            throw new BusinessException("Loại không được để trống (Brand hoặc Category).");

        var normalizedType = char.ToUpper(dto.Type[0]) + dto.Type.Substring(1).ToLower();
        if (normalizedType != "Brand" && normalizedType != "Category")
            throw new BusinessException("Loại phải là 'Brand' hoặc 'Category'.");

        var exists = await _context.ProductTaxonomies
            .AnyAsync(t => t.Name.ToLower() == dto.Name.Trim().ToLower()
                && t.Type == normalizedType
                && (t.BranchId == branchId || t.BranchId == null));

        if (exists)
            throw new BusinessException("Tên đã tồn tại.");

        var taxonomy = new ProductTaxonomy
        {
            TaxonomyId = Guid.NewGuid(),
            Name = dto.Name.Trim(),
            Type = normalizedType,
            BranchId = branchId,
            CreatedAt = DateTime.UtcNow
        };

        _context.ProductTaxonomies.Add(taxonomy);
        await _context.SaveChangesAsync();

        return taxonomy;
    }

    public async Task<ProductTaxonomy?> UpdateTaxonomyAsync(Guid id, UpdateProductTaxonomyDto dto, Guid? branchId)
    {
        var taxonomy = await _context.ProductTaxonomies.FindAsync(id);
        if (taxonomy == null) return null;

        if (taxonomy.BranchId != null && taxonomy.BranchId != branchId)
            throw new BusinessException("Bạn không có quyền sửa taxonomy này.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BusinessException("Tên không được để trống.");

        var exists = await _context.ProductTaxonomies
            .AnyAsync(t => t.TaxonomyId != id
                && t.Name.ToLower() == dto.Name.Trim().ToLower()
                && t.Type == taxonomy.Type
                && (t.BranchId == branchId || t.BranchId == null));

        if (exists)
            throw new BusinessException("Tên đã tồn tại.");

        taxonomy.Name = dto.Name.Trim();
        await _context.SaveChangesAsync();

        return taxonomy;
    }

    public async Task<bool> DeleteTaxonomyAsync(Guid id, Guid? branchId)
    {
        var taxonomy = await _context.ProductTaxonomies.FindAsync(id);
        if (taxonomy == null)
            throw new KeyNotFoundException("Không tìm thấy taxonomy.");

        if (taxonomy.BranchId != null && taxonomy.BranchId != branchId)
            throw new BusinessException("Bạn không có quyền xóa taxonomy này.");

        var isInUse = await _context.Products
            .AnyAsync(p => p.BrandTaxonomyId == id || p.CategoryTaxonomyId == id);

        if (isInUse)
            throw new BusinessException("Không thể xóa vì đang được sử dụng bởi sản phẩm.");

        _context.ProductTaxonomies.Remove(taxonomy);
        await _context.SaveChangesAsync();

        return true;
    }
}
