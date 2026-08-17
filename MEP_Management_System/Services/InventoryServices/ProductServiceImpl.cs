using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services.InventoryServices;

public class ProductServiceImpl : IProductService
{
    private readonly MEPDatabaseContext _context;
    private readonly ICurrentBranchAccessor _currentBranch;
    private readonly IMapper _mapper;

    public ProductServiceImpl(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch, IMapper mapper)
    {
        _context = context;
        _currentBranch = currentBranch;
        _mapper = mapper;
    }

    private Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(userIdStr, out Guid userId) ? userId : null;
    }

    private Guid? GetCurrentBranchId() => _currentBranch.GetBranchId();

    private static DateTime UtcNow() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    public async Task<ApiResponse<object>> GetProductsLookupAsync(ClaimsPrincipal user, Guid? branchId, int pageNumber, int pageSize, string? searchTerm)
    {
        if (branchId == null)
            return new ApiResponse<object> { Success = true, Message = "Chua duoc gan chi nhanh.", Data = new { items = new List<object>() } };

        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _context.BranchProducts
            .Include(bp => bp.Product)
            .Where(bp => bp.BranchId == branchId && bp.IsActive == 1 && bp.Product.Status == 1)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(bp =>
                bp.Product.ProductCode.ToLower().Contains(term) ||
                bp.Product.ProductName.ToLower().Contains(term));
        }

        var items = await query
            .OrderBy(bp => bp.Product.ProductName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(bp => new
            {
                branchProductId = bp.BranchProductId,
                productId = bp.Product.ProductId,
                productCode = bp.Product.ProductCode,
                productName = bp.Product.ProductName,
                unitName = bp.Product.BaseUnit,
                costPrice = bp.CostPrice,
                actualStock = bp.ActualStock,
                availableStock = bp.AvailableStock,
                warrantyQuantity = bp.WarrantyQuantity
            })
            .ToListAsync();

        return new ApiResponse<object> { Success = true, Message = "Danh sach san pham.", Data = new { items } };
    }

    public async Task<ApiResponse<PaginatedResponse<ProductUpsertDto>>> GetProductsAsync(ClaimsPrincipal user, ProductParameters @params, Guid? branchId)
    {
        if (branchId == null)
        {
            return new ApiResponse<PaginatedResponse<ProductUpsertDto>>
            {
                Success = true,
                Message = "Chua duoc gan chi nhanh.",
                Data = new PaginatedResponse<ProductUpsertDto>
                {
                    Items = new List<ProductUpsertDto>(),
                    TotalCount = 0,
                    PageNumber = @params.PageNumber,
                    PageSize = @params.PageSize,
                    TotalPages = 1,
                    HasNextPage = false,
                    HasPreviousPage = false
                }
            };
        }

        if (@params.PageSize < 1 || @params.PageSize > 1000)
            throw new BusinessException("Invalid pagination parameters", "PageSize must be between 1 and 1000");

        var query = _context.BranchProducts
            .Include(bp => bp.Product).ThenInclude(p => p.Supplier)
            .Include(bp => bp.Product).ThenInclude(p => p.MediaResources)
            .Include(bp => bp.Product).ThenInclude(p => p.ProductAttributes)
            .Include(bp => bp.Product).ThenInclude(p => p.ProductConversionUnits)
            .Include(bp => bp.Product).ThenInclude(p => p.CreatedByUser)
            .Include(bp => bp.Product).ThenInclude(p => p.UpdatedByUser)
            .Where(bp => bp.BranchId == branchId)
            .Where(bp => bp.IsActive != -1 &&
                         (bp.Product.Status == null || bp.Product.Status != -1) &&
                         (bp.Product.BranchId == null || bp.Product.BranchId == branchId))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(@params.SearchTerm))
        {
            string search = @params.SearchTerm.Trim().ToLower();
            query = query.Where(bp =>
                bp.Product.ProductCode.ToLower().Contains(search) ||
                bp.Product.ProductName.ToLower().Contains(search) ||
                (bp.Product.Barcode != null && bp.Product.Barcode.ToLower().Contains(search)) ||
                (bp.Product.CategoryName != null && bp.Product.CategoryName.ToLower().Contains(search)) ||
                (bp.Product.BrandName != null && bp.Product.BrandName.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(@params.CategoryName))
            query = query.Where(bp => bp.Product.CategoryName != null && bp.Product.CategoryName.ToLower() == @params.CategoryName.Trim().ToLower());

        if (!string.IsNullOrWhiteSpace(@params.BrandName))
            query = query.Where(bp => bp.Product.BrandName != null && bp.Product.BrandName.ToLower() == @params.BrandName.Trim().ToLower());

        if (!string.IsNullOrWhiteSpace(@params.Status))
        {
            short statusValue = @params.Status.ToLower() == "active" ? (short)1 : (short)0;
            query = query.Where(bp => bp.IsActive == statusValue);
        }

        if (@params.SupplierId.HasValue)
            query = query.Where(bp => bp.Product.SupplierId == @params.SupplierId.Value);

        query = ApplySorting(query, @params.Sort, @params.SortOrder);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)@params.PageSize);
        var hasNextPage = @params.PageNumber < totalPages;
        var hasPreviousPage = @params.PageNumber > 1;

        var items = await query
            .Skip((@params.PageNumber - 1) * @params.PageSize)
            .Take(@params.PageSize)
            .ToListAsync();

        var resultList = items.Select(bp => MapToProductDto(bp)).ToList();

        return new ApiResponse<PaginatedResponse<ProductUpsertDto>>
        {
            Success = true,
            Message = "Products retrieved successfully",
            Data = new PaginatedResponse<ProductUpsertDto>
            {
                Items = resultList,
                TotalCount = totalCount,
                PageNumber = @params.PageNumber,
                PageSize = @params.PageSize,
                TotalPages = totalPages,
                HasNextPage = hasNextPage,
                HasPreviousPage = hasPreviousPage
            }
        };
    }

    public async Task<ApiResponse<object>> SearchProductsAsync(ClaimsPrincipal user, string term, int limit, Guid? branchId)
    {
        if (branchId == null)
            return new ApiResponse<object> { Success = true, Message = "Chua duoc gan chi nhanh.", Data = new { items = new List<ProductSearchResultDto>() } };

        if (string.IsNullOrWhiteSpace(term))
        {
            var fallbackResults = await _context.BranchProducts
                .Include(bp => bp.Product).ThenInclude(p => p.MediaResources)
                .Where(bp => bp.BranchId == branchId && bp.IsActive == 1 && bp.Product.Status == 1)
                .OrderByDescending(bp => bp.Product.CreatedAt)
                .Take(Math.Clamp(limit, 1, 50))
                .Select(bp => new ProductSearchResultDto
                {
                    ProductId = bp.Product.ProductId,
                    BranchProductId = bp.BranchProductId,
                    ProductCode = bp.Product.ProductCode,
                    ProductName = bp.Product.ProductName,
                    BrandName = bp.Product.BrandName,
                    CategoryName = bp.Product.CategoryName,
                    Barcode = bp.Product.Barcode,
                    Unit = bp.Product.BaseUnit,
                    ImageUrl = bp.Product.MediaResources.Where(m => m.IsPrimary == 1).Select(m => m.MediaUrl).FirstOrDefault()
                })
                .ToListAsync();

            return new ApiResponse<object> { Success = true, Message = "Danh sach san pham.", Data = new { items = fallbackResults } };
        }

        limit = Math.Clamp(limit, 1, 50);
        var trimmedTerm = term.Trim().ToLower();

        var results = await _context.BranchProducts
            .Include(bp => bp.Product).ThenInclude(p => p.MediaResources)
            .Where(bp => bp.BranchId == branchId && bp.IsActive == 1 && bp.Product.Status == 1)
            .Where(bp =>
                bp.Product.ProductCode.ToLower().Contains(trimmedTerm) ||
                bp.Product.ProductName.ToLower().Contains(trimmedTerm) ||
                (bp.Product.Barcode != null && bp.Product.Barcode.ToLower().Contains(trimmedTerm)))
            .OrderBy(bp => bp.Product.ProductName)
            .Take(limit)
            .Select(bp => new ProductSearchResultDto
            {
                ProductId = bp.Product.ProductId,
                BranchProductId = bp.BranchProductId,
                ProductCode = bp.Product.ProductCode,
                ProductName = bp.Product.ProductName,
                BrandName = bp.Product.BrandName,
                CategoryName = bp.Product.CategoryName,
                Barcode = bp.Product.Barcode,
                Unit = bp.Product.BaseUnit,
                ImageUrl = bp.Product.MediaResources.Where(m => m.IsPrimary == 1).Select(m => m.MediaUrl).FirstOrDefault()
                    ?? bp.Product.MediaResources.Select(m => m.MediaUrl).FirstOrDefault(),
                CurrentStock = bp.AvailableStock,
                WarrantyQuantity = bp.WarrantyQuantity,
                CostPrice = bp.CostPrice,
                SalePrice = bp.SellPrice,
                WarrantyPeriod = bp.Product.WarrantyPeriod,
                WarrantyUnit = bp.Product.WarrantyUnit ?? "MONTH"
            })
            .ToListAsync();

        return new ApiResponse<object> { Success = true, Message = $"Found {results.Count} product(s)", Data = new { items = results, total = results.Count } };
    }

    public async Task<ApiResponse<object>> SuggestNewProductAsync(ClaimsPrincipal user, string term, Guid? branchId)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
            throw new BusinessException("Term phai co it nhat 2 ky tu");

        if (branchId == null)
            throw new BusinessException("Khong tim thay chi nhanh hop le.");

        var namePart = term.Trim();
        string baseCode = new string(namePart.ToUpper().Replace(' ', '_').Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (baseCode.Length > 12) baseCode = baseCode.Substring(0, 12);
        if (string.IsNullOrEmpty(baseCode)) baseCode = "SP";

        var existingCodes = await _context.BranchProducts
            .Include(bp => bp.Product)
            .Where(bp => bp.BranchId == branchId && bp.Product.ProductCode.StartsWith(baseCode))
            .Select(bp => bp.Product.ProductCode)
            .ToListAsync();

        string suggestedCode = baseCode;
        int suffix = 1;
        while (existingCodes.Contains(suggestedCode))
        {
            suffix++;
            suggestedCode = $"{baseCode}{suffix:000}";
        }

        var brandSuggestions = await _context.BranchProducts
            .Where(bp => bp.BranchId == branchId && bp.Product.BrandName != null && bp.Product.BrandName != "")
            .Select(bp => bp.Product.BrandName!)
            .Distinct()
            .OrderBy(b => b)
            .Take(10)
            .ToListAsync();

        var categorySuggestions = await _context.BranchProducts
            .Where(bp => bp.BranchId == branchId && bp.Product.CategoryName != null && bp.Product.CategoryName != "")
            .Select(bp => bp.Product.CategoryName!)
            .Distinct()
            .OrderBy(c => c)
            .Take(10)
            .ToListAsync();

        return new ApiResponse<object>
        {
            Success = true,
            Data = new NewProductSuggestionDto
            {
                SuggestedCode = suggestedCode,
                ProductName = namePart,
                DefaultUnit = "Cai",
                BrandSuggestions = brandSuggestions,
                CategorySuggestions = categorySuggestions
            }
        };
    }

    public async Task<ApiResponse<ProductUpsertDto>> GetProductByIdAsync(ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        var bp = await _context.BranchProducts
            .Include(b => b.Product).ThenInclude(p => p.Supplier)
            .Include(b => b.Product).ThenInclude(p => p.MediaResources)
            .Include(b => b.Product).ThenInclude(p => p.ProductAttributes)
            .Include(b => b.Product).ThenInclude(p => p.ProductConversionUnits)
            .Include(b => b.Product).ThenInclude(p => p.CreatedByUser)
            .Include(b => b.Product).ThenInclude(p => p.UpdatedByUser)
            .FirstOrDefaultAsync(b => b.ProductId == id && b.BranchId == branchId);

        if (bp == null)
            throw new KeyNotFoundException($"No product with ID: {id}");

        return new ApiResponse<ProductUpsertDto> { Success = true, Message = "Product retrieved successfully", Data = MapToProductDto(bp) };
    }

    public async Task<ApiResponse<ProductUpsertDto>> CreateProductAsync(ClaimsPrincipal user, ProductUpsertDto dto, Guid? branchId)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.ProductCode)) errors.Add("productCode is required");
        if (string.IsNullOrWhiteSpace(dto.ProductName)) errors.Add("productName is required");
        if (string.IsNullOrWhiteSpace(dto.Unit)) errors.Add("unit is required");
        if (dto.CostPrice <= 0) errors.Add("costPrice phai lon hon 0");
        if (dto.SalePrice <= 0) errors.Add("salePrice phai lon hon 0");

        if (errors.Any())
            throw new BusinessException("Validation failed", errors);

        bool isCodeExist = await _context.BranchProducts
            .AnyAsync(bp => bp.BranchId == branchId.Value &&
                            bp.IsActive != -1 &&
                            bp.Product.Status != -1 &&
                            bp.Product.ProductCode.ToLower() == dto.ProductCode.Trim().ToLower());
        if (isCodeExist)
            throw new BusinessException("Ma san pham da ton tai trong chi nhanh nay", $"Code '{dto.ProductCode}' exists");

        var currentTime = UtcNow();
        Guid? currentUserId = GetCurrentUserId(user);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var product = _mapper.Map<Product>(dto);
            product.ProductId = Guid.NewGuid();
            product.CreatedAt = currentTime;
            product.CreatedBy = currentUserId;
            product.BranchId = branchId;
            var hasSellable = dto.DirectSale == true || (dto.ConversionUnits != null && dto.ConversionUnits.Any(u => u.DirectSale == true));
            product.Status = hasSellable ? (short)1 : (short)0;
            product.ProductConversionUnits = new List<ProductConversionUnit>();
            product.BranchProducts = new List<BranchProduct>();

            _context.Products.Add(product);

            if (dto.Images != null && dto.Images.Any())
            {
                foreach (var imgUrl in dto.Images)
                {
                    _context.MediaResources.Add(new MediaResource
                    {
                        MediaId = Guid.NewGuid(),
                        ProductId = product.ProductId,
                        MediaUrl = imgUrl.Trim(),
                        MediaType = "IMAGE",
                        IsPrimary = (short)(imgUrl.Trim() == dto.ImageUrl?.Trim() ? 1 : 0),
                        CreatedAt = currentTime
                    });
                }
            }

            if (dto.Attributes != null && dto.Attributes.Any())
            {
                foreach (var attr in dto.Attributes)
                {
                    _context.ProductAttributes.Add(new ProductAttribute
                    {
                        AttributeId = Guid.NewGuid(),
                        ProductId = product.ProductId,
                        AttributeName = attr.Name,
                        AttributeValue = attr.Value
                    });
                }
            }

            if (dto.ConversionUnits != null && dto.ConversionUnits.Any())
            {
                var invalidUnits = dto.ConversionUnits.Where(u => u.Price < 0).ToList();
                if (invalidUnits.Any())
                    throw new BusinessException("Gia don vi quy doi khong duoc am");

                foreach (var unit in dto.ConversionUnits)
                {
                    _context.ProductConversionUnits.Add(new ProductConversionUnit
                    {
                        ConversionId = Guid.NewGuid(),
                        ProductId = product.ProductId,
                        UnitName = unit.Name,
                        ConvertValue = unit.Rate > 0 ? unit.Rate : unit.ConvertValue,
                        Price = unit.Price,
                        DirectSale = (short?)(unit.DirectSale ? 1 : 0),
                        Product = null!
                    });
                }
            }

            var primaryShelfLocation = !string.IsNullOrWhiteSpace(dto.ShelfLocation)
                ? dto.ShelfLocation
                : (dto.ShelfLocations != null && dto.ShelfLocations.Count > 0 ? dto.ShelfLocations[0] : null);

            var branchProduct = new BranchProduct
            {
                BranchProductId = Guid.NewGuid(),
                BranchId = branchId.Value,
                ProductId = product.ProductId,
                ActualStock = dto.ActualStock,
                AvailableStock = dto.AvailableStock,
                ReservedStock = dto.ReservedStock,
                ShelfLocation = primaryShelfLocation ?? "Chua xep vi tri",
                MinimumStock = dto.MinimumStock ?? 0,
                MaximumStock = dto.MaximumStock ?? 0,
                CostPrice = dto.CostPrice,
                SellPrice = dto.SalePrice,
                DirectSale = (short?)(dto.DirectSale ? 1 : 0),
                IsActive = (short)(dto.DirectSale == true || (dto.ConversionUnits != null && dto.ConversionUnits.Any(u => u.DirectSale == true)) ? 1 : 0),
                Product = null!
            };
            _context.BranchProducts.Add(branchProduct);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return new ApiResponse<ProductUpsertDto> { Success = true, Message = "Product created successfully", Data = MapToProductDto(branchProduct) };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse<ProductUpsertDto>> UpdateProductAsync(ClaimsPrincipal user, Guid id, UpdateProductDto dto, Guid? branchId)
    {
        var bp = await _context.BranchProducts
            .Include(b => b.Product).ThenInclude(p => p.MediaResources)
            .Include(b => b.Product).ThenInclude(p => p.ProductAttributes)
            .Include(b => b.Product).ThenInclude(p => p.ProductConversionUnits)
            .FirstOrDefaultAsync(b => b.ProductId == id && b.BranchId == branchId);

        if (bp == null)
            throw new KeyNotFoundException($"No product with ID: {id} in the branch");

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.ProductCode)) errors.Add("productCode is required");
        if (string.IsNullOrWhiteSpace(dto.ProductName)) errors.Add("productName is required");
        if (string.IsNullOrWhiteSpace(dto.Unit)) errors.Add("unit is required");
        if (dto.CostPrice <= 0) errors.Add("costPrice phai lon hon 0");
        if (dto.SalePrice <= 0) errors.Add("salePrice phai lon hon 0");

        if (errors.Any())
            throw new BusinessException("Validation failed", errors);

        bool isCodeExist = await _context.BranchProducts
            .AnyAsync(otherBp => otherBp.BranchId == branchId.Value &&
                                  otherBp.ProductId != id &&
                                  otherBp.IsActive != -1 &&
                                  otherBp.Product.Status != -1 &&
                                  otherBp.Product.ProductCode.ToLower() == dto.ProductCode.Trim().ToLower());
        if (isCodeExist)
            throw new BusinessException("Product code already exists in this branch", $"Code '{dto.ProductCode}' exists");

        var currentTime = UtcNow();
        Guid? currentUserId = GetCurrentUserId(user);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            bp.Product.ProductName = dto.ProductName;
            bp.Product.Barcode = dto.Barcode;
            bp.Product.Specification = dto.Specification;
            bp.Product.ItemType = dto.ItemType;
            bp.Product.BrandName = dto.BrandName;
            bp.Product.CategoryName = dto.CategoryName;
            bp.Product.SupplierId = dto.SupplierId;
            bp.Product.Weight = dto.Weight;
            bp.Product.WeightUnit = dto.WeightUnit;
            bp.Product.Width = dto.Width;
            bp.Product.Length = dto.Length;
            bp.Product.Height = dto.Height;
            bp.Product.SizeUnit = dto.SizeUnit;
            bp.Product.ProductCode = dto.ProductCode;
            bp.Product.BaseUnit = dto.Unit;
            bp.Product.UpdatedAt = currentTime;
            bp.Product.UpdatedBy = currentUserId;

            _context.Products.Update(bp.Product);

            bp.ActualStock = bp.ActualStock;
            bp.AvailableStock = bp.AvailableStock;
            bp.ReservedStock = bp.ReservedStock;

            var primaryShelfLocation = !string.IsNullOrWhiteSpace(dto.ShelfLocation)
                ? dto.ShelfLocation
                : (dto.ShelfLocations != null && dto.ShelfLocations.Count > 0 ? dto.ShelfLocations[0] : null);
            bp.ShelfLocation = primaryShelfLocation ?? bp.ShelfLocation ?? "Chua xep vi tri";

            bp.MinimumStock = dto.MinimumStock ?? bp.MinimumStock ?? 0;
            bp.MaximumStock = dto.MaximumStock ?? bp.MaximumStock ?? 0;
            bp.CostPrice = dto.CostPrice;
            bp.SellPrice = dto.SalePrice;
            bp.DirectSale = (short?)(dto.DirectSale ? 1 : 0);

            var hasAnySellableUnit = dto.DirectSale == true || (dto.ConversionUnits != null && dto.ConversionUnits.Any(u => u.DirectSale == true));
            if (hasAnySellableUnit)
            {
                if (bp.IsActive != 1) { bp.IsActive = 1; if (bp.Product != null) bp.Product.Status = 1; }
            }
            else
            {
                if (bp.IsActive != 0) { bp.IsActive = 0; if (bp.Product != null) bp.Product.Status = 0; }
            }

            _context.BranchProducts.Update(bp);

            var oldMedia = await _context.MediaResources.Where(m => m.ProductId == id).ToListAsync();
            _context.MediaResources.RemoveRange(oldMedia);

            if (dto.Images != null && dto.Images.Any())
            {
                foreach (var imgUrl in dto.Images)
                {
                    _context.MediaResources.Add(new MediaResource
                    {
                        MediaId = Guid.NewGuid(),
                        ProductId = id,
                        MediaUrl = imgUrl.Trim(),
                        MediaType = "IMAGE",
                        IsPrimary = (short)(imgUrl.Trim() == dto.ImageUrl?.Trim() ? 1 : 0),
                        CreatedAt = currentTime
                    });
                }
            }

            var oldAttributes = await _context.ProductAttributes.Where(a => a.ProductId == id).ToListAsync();
            _context.ProductAttributes.RemoveRange(oldAttributes);

            if (dto.Attributes != null && dto.Attributes.Any())
            {
                foreach (var attr in dto.Attributes)
                {
                    _context.ProductAttributes.Add(new ProductAttribute
                    {
                        AttributeId = Guid.NewGuid(),
                        ProductId = id,
                        AttributeName = attr.Name,
                        AttributeValue = attr.Value
                    });
                }
            }

            var oldUnits = await _context.ProductConversionUnits.Where(u => u.ProductId == id).ToListAsync();
            _context.ProductConversionUnits.RemoveRange(oldUnits);

            if (dto.ConversionUnits != null && dto.ConversionUnits.Any())
            {
                foreach (var unit in dto.ConversionUnits)
                {
                    _context.ProductConversionUnits.Add(new ProductConversionUnit
                    {
                        ConversionId = Guid.NewGuid(),
                        ProductId = id,
                        UnitName = unit.Name,
                        ConvertValue = unit.Rate > 0 ? unit.Rate : unit.ConvertValue,
                        Price = unit.Price,
                        DirectSale = (short?)(unit.DirectSale ? 1 : 0),
                        Product = null!
                    });
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var updatedBp = await _context.BranchProducts
                .Include(b => b.Product).ThenInclude(p => p.Supplier)
                .Include(b => b.Product).ThenInclude(p => p.MediaResources)
                .Include(b => b.Product).ThenInclude(p => p.ProductAttributes)
                .Include(b => b.Product).ThenInclude(p => p.ProductConversionUnits)
                .FirstOrDefaultAsync(b => b.ProductId == id && b.BranchId == branchId);

            return new ApiResponse<ProductUpsertDto> { Success = true, Message = "Product updated successfully", Data = MapToProductDto(updatedBp!) };
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ApiResponse<object>> DeleteProductAsync(ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        var bp = await _context.BranchProducts
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.ProductId == id && b.BranchId == branchId);

        if (bp == null)
            throw new KeyNotFoundException($"No product with ID: {id}");

        var hasDependencies = await _context.OrderItems.AnyAsync(oi => oi.BranchProductId == bp.BranchProductId) ||
                              await _context.ReturnItems.AnyAsync(ri => ri.BranchProductId == bp.BranchProductId) ||
                              await _context.StockTicketItems.AnyAsync(sti => sti.BranchProductId == bp.BranchProductId);

        if (hasDependencies)
        {
            bp.IsActive = -1;
            if (bp.Product != null)
            {
                bp.Product.Status = -1;
                if (!string.IsNullOrEmpty(bp.Product.ProductCode))
                {
                    var shortId = bp.Product.ProductId.ToString().Substring(0, 6);
                    var newCode = $"d_{shortId}_{bp.Product.ProductCode}";
                    if (newCode.Length > 50) newCode = newCode.Substring(0, 50);
                    bp.Product.ProductCode = newCode;
                }
                _context.Products.Update(bp.Product);
            }
            _context.BranchProducts.Update(bp);
        }
        else
        {
            _context.BranchProducts.Remove(bp);
            if (bp.Product != null)
            {
                var hasOtherBranches = await _context.BranchProducts.AnyAsync(otherBp => otherBp.ProductId == bp.ProductId && otherBp.BranchProductId != bp.BranchProductId);
                if (!hasOtherBranches)
                    _context.Products.Remove(bp.Product);
            }
        }

        await _context.SaveChangesAsync();
        return new ApiResponse<object> { Success = true, Message = "Product deleted successfully" };
    }

    public async Task<ApiResponse<object>> ToggleStatusAsync(ClaimsPrincipal user, Guid id, bool isActive, Guid? branchId)
    {
        var bp = await _context.BranchProducts
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.ProductId == id && b.BranchId == branchId);

        if (bp == null)
            throw new KeyNotFoundException($"No product with ID {id} belongs to this branch");

        short statusVal = isActive ? (short)1 : (short)0;
        bp.IsActive = statusVal;

        if (isActive)
        {
            if (bp.DirectSale != 1)
            {
                var hasAnySellableUnit = await _context.ProductConversionUnits
                    .AnyAsync(u => u.ProductId == bp.ProductId && u.DirectSale == 1);
                if (!hasAnySellableUnit)
                    bp.DirectSale = 1;
            }
        }
        else
        {
            bp.DirectSale = 0;
            var conversionUnits = await _context.ProductConversionUnits
                .Where(u => u.ProductId == bp.ProductId && u.DirectSale == 1).ToListAsync();
            foreach (var cu in conversionUnits) cu.DirectSale = 0;
        }

        if (bp.Product != null)
        {
            bp.Product.Status = statusVal;
            _context.Products.Update(bp.Product);
        }

        _context.BranchProducts.Update(bp);
        await _context.SaveChangesAsync();

        return new ApiResponse<object> { Success = true, Message = $"Product status updated to {(isActive ? "Active" : "Inactive")} successfully" };
    }

    public async Task<ApiResponse<object>> ToggleStatusBulkAsync(ClaimsPrincipal user, BulkToggleStatusRequest request, Guid? branchId)
    {
        if (request?.Ids == null || !request.Ids.Any())
            throw new BusinessException("Validation failed", "List of product IDs cannot be empty");

        if (request.Ids.Count > 500)
            throw new BusinessException("Validation failed", "Maximum 500 ids per request");

        var bps = await _context.BranchProducts
            .Include(b => b.Product)
            .Where(b => b.BranchId == branchId && request.Ids.Contains(b.ProductId))
            .ToListAsync();

        if (!bps.Any())
            throw new KeyNotFoundException("None of the provided ids belong to the current branch");

        var updatedIds = new List<Guid>();
        var notFoundIds = request.Ids.Except(bps.Select(b => b.ProductId)).ToList();

        foreach (var bp in bps)
        {
            short statusVal = request.IsActive ? (short)1 : (short)0;
            bp.IsActive = statusVal;

            if (request.IsActive)
            {
                if (bp.DirectSale != 1)
                {
                    var hasAnySellableUnit = await _context.ProductConversionUnits
                        .AnyAsync(u => u.ProductId == bp.ProductId && u.DirectSale == 1);
                    if (!hasAnySellableUnit)
                        bp.DirectSale = 1;
                }
            }
            else
            {
                bp.DirectSale = 0;
                var conversionUnits = await _context.ProductConversionUnits
                    .Where(u => u.ProductId == bp.ProductId && u.DirectSale == 1).ToListAsync();
                foreach (var cu in conversionUnits) cu.DirectSale = 0;
            }

            if (bp.Product != null)
            {
                bp.Product.Status = statusVal;
                _context.Products.Update(bp.Product);
            }

            _context.BranchProducts.Update(bp);
            updatedIds.Add(bp.ProductId);
        }

        await _context.SaveChangesAsync();

        return new ApiResponse<object>
        {
            Success = true,
            Message = $"Bulk status update completed. Updated {updatedIds.Count} product(s).",
            Data = new { UpdatedCount = updatedIds.Count, UpdatedIds = updatedIds, NotFoundCount = notFoundIds.Count, NotFoundIds = notFoundIds }
        };
    }

    public async Task<ApiResponse<object>> BulkImportProductsAsync(ClaimsPrincipal user, ProductBulkImportDto dto, Guid? branchId)
    {
        if (dto.Products == null || !dto.Products.Any())
            throw new BusinessException("No products to import");

        var currentTime = UtcNow();
        Guid? currentUserId = GetCurrentUserId(user);

        var existingCodes = await _context.Products.Select(p => p.ProductCode.ToLower()).ToListAsync();
        var existingCodeSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);
        var importedCodesInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        const int batchSize = 500;
        var productsToInsert = new List<Product>();
        var branchProductsToInsert = new List<BranchProduct>();
        var successCount = 0;
        var errors = new List<string>();

        var existingCategories = await _context.ProductTaxonomies.Where(t => t.Type == "Category").ToListAsync();
        var existingBrands = await _context.ProductTaxonomies.Where(t => t.Type == "Brand").ToListAsync();
        var categoryDict = existingCategories.ToDictionary(c => c.Name.Trim().ToLower(), c => c.TaxonomyId);
        var brandDict = existingBrands.ToDictionary(b => b.Name.Trim().ToLower(), b => b.TaxonomyId);
        var newTaxonomies = new List<ProductTaxonomy>();
        var pendingCategoryKeys = new HashSet<string>();
        var pendingBrandKeys = new HashSet<string>();

        foreach (var pDto in dto.Products)
        {
            var rowErrors = new List<string>();
            if (string.IsNullOrWhiteSpace(pDto.ProductCode)) rowErrors.Add("Ma san pham trong");
            else if (pDto.ProductCode.Length > 50) rowErrors.Add("Ma san pham qua 50 ky tu");
            if (string.IsNullOrWhiteSpace(pDto.ProductName)) rowErrors.Add("Ten san pham trong");
            else if (pDto.ProductName.Length > 255) rowErrors.Add("Ten san pham qua 255 ky tu");
            if (string.IsNullOrWhiteSpace(pDto.Unit)) rowErrors.Add("Don vi trong");
            else if (pDto.Unit.Length > 20) rowErrors.Add($"Don vi '{pDto.Unit}' qua 20 ky tu");
            if (pDto.CategoryName != null && pDto.CategoryName.Length > 255) rowErrors.Add("Ten danh muc qua 255 ky tu");
            if (pDto.BrandName != null && pDto.BrandName.Length > 255) rowErrors.Add("Ten thuong hieu qua 255 ky tu");
            if (pDto.CostPrice < 0) rowErrors.Add("Gia von khong duoc am");
            if (pDto.SalePrice < 0) rowErrors.Add("Gia ban khong duoc am");

            if (rowErrors.Any()) { errors.Add($"San pham '{pDto.ProductName ?? "Khong ten"}': {string.Join(", ", rowErrors)}"); continue; }

            var codeKey = pDto.ProductCode.Trim().ToLower();
            if (existingCodeSet.Contains(codeKey) || importedCodesInBatch.Contains(codeKey))
            { errors.Add($"San pham '{pDto.ProductName}': Ma '{pDto.ProductCode}' da ton tai"); continue; }

            if (dto.AutoCreateMissingCategories && !string.IsNullOrWhiteSpace(pDto.CategoryName))
            {
                var catName = pDto.CategoryName.Trim();
                var catKey = catName.ToLower();
                if (!categoryDict.TryGetValue(catKey, out _))
                {
                    if (!pendingCategoryKeys.Contains(catKey))
                    {
                        var newCat = new ProductTaxonomy { TaxonomyId = Guid.NewGuid(), BranchId = branchId.Value, Name = catName, Type = "Category", CreatedAt = currentTime };
                        newTaxonomies.Add(newCat);
                        pendingCategoryKeys.Add(catKey);
                        pDto.CategoryTaxonomyId = newCat.TaxonomyId;
                    }
                    else
                    {
                        var pendingCat = newTaxonomies.FirstOrDefault(t => t.Name.ToLower() == catKey && t.Type == "Category");
                        pDto.CategoryTaxonomyId = pendingCat?.TaxonomyId ?? Guid.Empty;
                    }
                }
            }

            if (dto.AutoCreateMissingBrands && !string.IsNullOrWhiteSpace(pDto.BrandName))
            {
                var brandName = pDto.BrandName.Trim();
                var brandKey = brandName.ToLower();
                if (!brandDict.TryGetValue(brandKey, out _))
                {
                    if (!pendingBrandKeys.Contains(brandKey))
                    {
                        var newBrand = new ProductTaxonomy { TaxonomyId = Guid.NewGuid(), BranchId = branchId.Value, Name = brandName, Type = "Brand", CreatedAt = currentTime };
                        newTaxonomies.Add(newBrand);
                        pendingBrandKeys.Add(brandKey);
                        pDto.BrandTaxonomyId = newBrand.TaxonomyId;
                    }
                    else
                    {
                        var pendingBrand = newTaxonomies.FirstOrDefault(t => t.Name.ToLower() == brandKey && t.Type == "Brand");
                        pDto.BrandTaxonomyId = pendingBrand?.TaxonomyId ?? Guid.Empty;
                    }
                }
            }

            var product = _mapper.Map<Product>(pDto);
            product.ProductId = Guid.NewGuid();
            product.CreatedAt = currentTime;
            product.CreatedBy = currentUserId;
            product.BranchId = branchId;
            product.ProductConversionUnits = new List<ProductConversionUnit>();
            product.BranchProducts = new List<BranchProduct>();

            importedCodesInBatch.Add(codeKey);

            if (pDto.Images != null && pDto.Images.Any())
            {
                foreach (var imgUrl in pDto.Images)
                {
                    _context.MediaResources.Add(new MediaResource
                    {
                        MediaId = Guid.NewGuid(), ProductId = product.ProductId, MediaUrl = imgUrl.Trim(), MediaType = "IMAGE",
                        IsPrimary = (short)(imgUrl.Trim() == pDto.ImageUrl?.Trim() ? 1 : 0), CreatedAt = currentTime
                    });
                }
            }

            var branchProduct = new BranchProduct
            {
                BranchProductId = Guid.NewGuid(), BranchId = branchId.Value, ProductId = product.ProductId,
                ActualStock = pDto.ActualStock > 0 ? pDto.ActualStock : 0,
                AvailableStock = pDto.ActualStock > 0 ? pDto.ActualStock : 0,
                ReservedStock = 0,
                ShelfLocation = !string.IsNullOrWhiteSpace(pDto.ShelfLocation) ? pDto.ShelfLocation : "Chua xep vi tri",
                MinimumStock = pDto.MinimumStock ?? 0, MaximumStock = pDto.MaximumStock ?? 0,
                CostPrice = pDto.CostPrice, SellPrice = pDto.SalePrice,
                DirectSale = (short?)(pDto.DirectSale ? 1 : 0), IsActive = 1, Product = null!
            };

            productsToInsert.Add(product);
            branchProductsToInsert.Add(branchProduct);
            successCount++;

            if (productsToInsert.Count >= batchSize)
            {
                if (newTaxonomies.Any())
                {
                    _context.ProductTaxonomies.AddRange(newTaxonomies);
                    await _context.SaveChangesAsync();
                    foreach (var t in newTaxonomies)
                    {
                        var key = t.Name.Trim().ToLower();
                        if (t.Type == "Category") categoryDict[key] = t.TaxonomyId;
                        else brandDict[key] = t.TaxonomyId;
                    }
                    newTaxonomies.Clear();
                    pendingCategoryKeys.Clear();
                    pendingBrandKeys.Clear();
                }

                _context.Products.AddRange(productsToInsert);
                _context.BranchProducts.AddRange(branchProductsToInsert);
                await _context.SaveChangesAsync();
                productsToInsert.Clear();
                branchProductsToInsert.Clear();
                _context.ChangeTracker.Clear();
            }
        }

        if (newTaxonomies.Any()) { _context.ProductTaxonomies.AddRange(newTaxonomies); await _context.SaveChangesAsync(); }
        if (productsToInsert.Any()) { _context.Products.AddRange(productsToInsert); _context.BranchProducts.AddRange(branchProductsToInsert); await _context.SaveChangesAsync(); }

        return new ApiResponse<object>
        {
            Success = true,
            Message = $"Nhap thanh cong {successCount} san pham" + (errors.Any() ? $", bo qua {errors.Count} san pham loi." : "."),
            Data = new { successCount, errors }
        };
    }

    public async Task<ApiResponse<List<TaxonomyItemDto>>> GetCategoriesAsync(ClaimsPrincipal user, Guid? branchId)
    {
        if (branchId == null)
            return new ApiResponse<List<TaxonomyItemDto>> { Success = true, Message = "Chua duoc gan chi nhanh.", Data = new List<TaxonomyItemDto>() };

        var productGroupings = await _context.BranchProducts
            .Where(bp => bp.BranchId == branchId && bp.Product.CategoryName != null && bp.Product.CategoryName != "")
            .Where(bp => bp.IsActive != -1 && (bp.Product.Status == null || bp.Product.Status != -1) && (bp.Product.BranchId == null || bp.Product.BranchId == branchId))
            .Select(bp => new { bp.ProductId, bp.Product.CategoryName })
            .Distinct()
            .GroupBy(x => x.CategoryName)
            .Select(g => new { Name = g.Key!, Count = g.Count() })
            .ToListAsync();

        var taxonomies = await _context.ProductTaxonomies.Where(t => t.BranchId == branchId && t.Type == "Category").Select(t => t.Name).ToListAsync();

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in productGroupings)
        {
            if (dict.ContainsKey(g.Name)) dict[g.Name] += g.Count;
            else dict[g.Name] = g.Count;
        }

        var resultList = new List<TaxonomyItemDto>();
        foreach (var tax in taxonomies)
        {
            int count = dict.ContainsKey(tax) ? dict[tax] : 0;
            resultList.Add(new TaxonomyItemDto { Name = tax, ProductCount = count });
            dict.Remove(tax);
        }

        foreach (var kv in dict)
            resultList.Add(new TaxonomyItemDto { Name = kv.Key, ProductCount = kv.Value });

        var result = resultList.OrderBy(x => x.Name).ToList();
        return new ApiResponse<List<TaxonomyItemDto>> { Success = true, Message = "Lay danh sach nhom hang thanh cong", Data = result };
    }

    public async Task<ApiResponse<List<TaxonomyItemDto>>> GetBrandsAsync(ClaimsPrincipal user, Guid? branchId)
    {
        if (branchId == null)
            return new ApiResponse<List<TaxonomyItemDto>> { Success = true, Message = "Chua duoc gan chi nhanh.", Data = new List<TaxonomyItemDto>() };

        var productGroupings = await _context.BranchProducts
            .Where(bp => bp.BranchId == branchId && bp.Product.BrandName != null && bp.Product.BrandName != "")
            .Where(bp => bp.IsActive != -1 && (bp.Product.Status == null || bp.Product.Status != -1) && (bp.Product.BranchId == null || bp.Product.BranchId == branchId))
            .Select(bp => new { bp.ProductId, bp.Product.BrandName })
            .Distinct()
            .GroupBy(x => x.BrandName)
            .Select(g => new { Name = g.Key!, Count = g.Count() })
            .ToListAsync();

        var taxonomies = await _context.ProductTaxonomies.Where(t => t.BranchId == branchId && t.Type == "Brand").Select(t => t.Name).ToListAsync();

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in productGroupings)
        {
            if (dict.ContainsKey(g.Name)) dict[g.Name] += g.Count;
            else dict[g.Name] = g.Count;
        }

        var resultList = new List<TaxonomyItemDto>();
        foreach (var tax in taxonomies)
        {
            int count = dict.ContainsKey(tax) ? dict[tax] : 0;
            resultList.Add(new TaxonomyItemDto { Name = tax, ProductCount = count });
            dict.Remove(tax);
        }

        foreach (var kv in dict)
            resultList.Add(new TaxonomyItemDto { Name = kv.Key, ProductCount = kv.Value });

        var result = resultList.OrderBy(x => x.Name).ToList();
        return new ApiResponse<List<TaxonomyItemDto>> { Success = true, Message = "Lay danh sach thuong hieu thanh cong", Data = result };
    }

    public async Task<ApiResponse<object>> CreateCategoryAsync(ClaimsPrincipal user, CreateTaxonomyDto dto, Guid? branchId)
    {
        if (branchId == null)
            throw new BusinessException("Khong tim thay chi nhanh hop le.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BusinessException("Ten nhom hang la bat buoc.");

        var trimmedName = dto.Name.Trim();
        var exists = await _context.BranchProducts
            .Include(bp => bp.Product)
            .AnyAsync(bp => bp.BranchId == branchId && bp.Product.CategoryName != null && bp.Product.CategoryName.ToLower() == trimmedName.ToLower());
        var existsInTaxonomies = await _context.ProductTaxonomies
            .AnyAsync(t => t.BranchId == branchId && t.Type == "Category" && t.Name.ToLower() == trimmedName.ToLower());

        if (exists || existsInTaxonomies)
            throw new BusinessException($"Nhom hang '{trimmedName}' da ton tai trong chi nhanh nay.");

        var tax = new ProductTaxonomy
        {
            TaxonomyId = Guid.NewGuid(),
            BranchId = branchId,
            Name = trimmedName,
            Type = "Category",
            CreatedAt = UtcNow()
        };
        _context.ProductTaxonomies.Add(tax);
        await _context.SaveChangesAsync();

        return new ApiResponse<object> { Success = true, Message = $"Nhom hang '{trimmedName}' da duoc them.", Data = new { Name = trimmedName } };
    }

    public async Task<ApiResponse<object>> CreateBrandAsync(ClaimsPrincipal user, CreateTaxonomyDto dto, Guid? branchId)
    {
        if (branchId == null)
            throw new BusinessException("Khong tim thay chi nhanh hop le.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BusinessException("Ten thuong hieu la bat buoc.");

        var trimmedName = dto.Name.Trim();
        var exists = await _context.BranchProducts
            .Include(bp => bp.Product)
            .AnyAsync(bp => bp.BranchId == branchId && bp.Product.BrandName != null && bp.Product.BrandName.ToLower() == trimmedName.ToLower());
        var existsInTaxonomies = await _context.ProductTaxonomies
            .AnyAsync(t => t.BranchId == branchId && t.Type == "Brand" && t.Name.ToLower() == trimmedName.ToLower());

        if (exists || existsInTaxonomies)
            throw new BusinessException($"Thuong hieu '{trimmedName}' da ton tai trong chi nhanh nay.");

        var tax = new ProductTaxonomy
        {
            TaxonomyId = Guid.NewGuid(),
            BranchId = branchId,
            Name = trimmedName,
            Type = "Brand",
            CreatedAt = UtcNow()
        };
        _context.ProductTaxonomies.Add(tax);
        await _context.SaveChangesAsync();

        return new ApiResponse<object> { Success = true, Message = $"Thuong hieu '{trimmedName}' da duoc them.", Data = new { Name = trimmedName } };
    }

    public async Task<ApiResponse<object>> RenameCategoryAsync(ClaimsPrincipal user, RenameTaxonomyDto dto, Guid? branchId)
    {
        if (branchId == null)
            throw new BusinessException("Khong tim thay chi nhanh hop le.");

        if (string.IsNullOrWhiteSpace(dto.OldName) || string.IsNullOrWhiteSpace(dto.NewName))
            throw new BusinessException("Du lieu khong hop le", "oldName va newName la bat buoc");

        var newName = dto.NewName.Trim();
        var oldName = dto.OldName.Trim();

        var affectedProductIds = await _context.BranchProducts
            .Where(bp => bp.BranchId == branchId && bp.Product.CategoryName != null && bp.Product.CategoryName.ToLower() == oldName.ToLower())
            .Where(bp => bp.IsActive != -1 && (bp.Product.Status == null || bp.Product.Status != -1) && (bp.Product.BranchId == null || bp.Product.BranchId == branchId))
            .Select(bp => bp.ProductId)
            .Distinct()
            .ToListAsync();

        var affectedProducts = await _context.Products.Where(p => affectedProductIds.Contains(p.ProductId)).ToListAsync();
        var tax = await _context.ProductTaxonomies.FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == "Category" && t.Name.ToLower() == oldName.ToLower());

        if (!affectedProducts.Any() && tax == null)
            throw new KeyNotFoundException($"Khong co du lieu ve nhom hang '{oldName}'");

        var currentTime = UtcNow();
        Guid? currentUserId = GetCurrentUserId(user);

        foreach (var product in affectedProducts)
        {
            product.CategoryName = newName;
            product.UpdatedAt = currentTime;
            product.UpdatedBy = currentUserId;
        }

        if (tax != null)
            tax.Name = newName;

        var config = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "CATEGORY_RETURN_POLICIES");
        if (config != null && !string.IsNullOrWhiteSpace(config.ConfigValue))
        {
            try
            {
                var caseInsOptions = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var policies = System.Text.Json.JsonSerializer.Deserialize<List<CategoryPolicyDto>>(config.ConfigValue, caseInsOptions);
                if (policies != null)
                {
                    foreach (var policy in policies)
                    {
                        bool matchById = tax != null && policy.CategoryId == tax.TaxonomyId;
                        bool matchByName = policy.CategoryName != null && policy.CategoryName.Equals(oldName, StringComparison.OrdinalIgnoreCase);
                        if (matchById || matchByName)
                        {
                            policy.CategoryName = newName;
                            if (tax != null && policy.CategoryId == null)
                                policy.CategoryId = tax.TaxonomyId;
                        }
                    }
                    config.ConfigValue = System.Text.Json.JsonSerializer.Serialize(policies, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                }
            }
            catch { /* ignore serialization errors */ }
        }

        await _context.SaveChangesAsync();
        return new ApiResponse<object> { Success = true, Message = $"Doi ten nhom hang thanh cong." };
    }

    public async Task<ApiResponse<object>> RenameBrandAsync(ClaimsPrincipal user, RenameTaxonomyDto dto, Guid? branchId)
    {
        if (branchId == null)
            throw new BusinessException("Khong tim thay chi nhanh hop le.");

        if (string.IsNullOrWhiteSpace(dto.OldName) || string.IsNullOrWhiteSpace(dto.NewName))
            throw new BusinessException("Du lieu khong hop le", "oldName va newName la bat buoc");

        var newName = dto.NewName.Trim();
        var oldName = dto.OldName.Trim();

        var affectedProductIds = await _context.BranchProducts
            .Where(bp => bp.BranchId == branchId && bp.Product.BrandName != null && bp.Product.BrandName.ToLower() == oldName.ToLower())
            .Where(bp => bp.IsActive != -1 && (bp.Product.Status == null || bp.Product.Status != -1) && (bp.Product.BranchId == null || bp.Product.BranchId == branchId))
            .Select(bp => bp.ProductId)
            .Distinct()
            .ToListAsync();

        var affectedProducts = await _context.Products.Where(p => affectedProductIds.Contains(p.ProductId)).ToListAsync();
        var tax = await _context.ProductTaxonomies.FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == "Brand" && t.Name.ToLower() == oldName.ToLower());

        if (!affectedProducts.Any() && tax == null)
            throw new KeyNotFoundException($"Khong co du lieu ve thuong hieu '{oldName}'");

        var currentTime = UtcNow();
        Guid? currentUserId = GetCurrentUserId(user);

        foreach (var product in affectedProducts)
        {
            product.BrandName = newName;
            product.UpdatedAt = currentTime;
            product.UpdatedBy = currentUserId;
        }

        if (tax != null)
            tax.Name = newName;

        await _context.SaveChangesAsync();
        return new ApiResponse<object> { Success = true, Message = $"Doi ten thuong hieu thanh cong." };
    }

    public async Task<ApiResponse<object>> DeleteCategoryAsync(ClaimsPrincipal user, string name, Guid? branchId)
    {
        if (branchId == null)
            throw new BusinessException("Khong tim thay chi nhanh hop le.");

        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessException("Ten nhom hang la bat buoc.");

        var trimmedName = name.Trim();

        var tax = await _context.ProductTaxonomies.FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == "Category" && t.Name.ToLower() == trimmedName.ToLower());
        if (tax != null)
        {
            _context.ProductTaxonomies.Remove(tax);
            await _context.SaveChangesAsync();
        }

        return new ApiResponse<object> { Success = true, Message = $"Nhom hang '{trimmedName}' da duoc xoa." };
    }

    public async Task<ApiResponse<object>> DeleteBrandAsync(ClaimsPrincipal user, string name, Guid? branchId)
    {
        if (branchId == null)
            throw new BusinessException("Khong tim thay chi nhanh hop le.");

        if (string.IsNullOrWhiteSpace(name))
            throw new BusinessException("Ten thuong hieu la bat buoc.");

        var trimmedName = name.Trim();

        var tax = await _context.ProductTaxonomies.FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == "Brand" && t.Name.ToLower() == trimmedName.ToLower());
        if (tax != null)
        {
            _context.ProductTaxonomies.Remove(tax);
            await _context.SaveChangesAsync();
        }

        return new ApiResponse<object> { Success = true, Message = $"Thuong hieu '{trimmedName}' da duoc xoa." };
    }

    public async Task<ApiResponse<List<TaxonomyItemDto>>> GetLocationsAsync(ClaimsPrincipal user, Guid? branchId)
    {
        if (branchId == null)
            return new ApiResponse<List<TaxonomyItemDto>> { Success = true, Message = "Chua duoc gan chi nhanh.", Data = new List<TaxonomyItemDto>() };

        var locations = await _context.BranchProducts
            .Where(bp => bp.BranchId == branchId && bp.ShelfLocation != null && bp.ShelfLocation != "")
            .Select(bp => bp.ShelfLocation!)
            .Distinct()
            .ToListAsync();

        var locationCounts = await _context.BranchProducts
            .Where(bp => bp.BranchId == branchId && bp.ShelfLocation != null && bp.ShelfLocation != "")
            .GroupBy(bp => bp.ShelfLocation)
            .Select(g => new { Name = g.Key!, Count = g.Count() })
            .ToListAsync();

        var dict = locationCounts.ToDictionary(x => x.Name, x => x.Count, StringComparer.OrdinalIgnoreCase);

        var resultList = locations.Select(loc => new TaxonomyItemDto
        {
            Name = loc,
            ProductCount = dict.ContainsKey(loc) ? dict[loc] : 0
        }).OrderBy(x => x.Name).ToList();

        return new ApiResponse<List<TaxonomyItemDto>> { Success = true, Message = "Lay danh sach vi tri thanh cong", Data = resultList };
    }

    public async Task<ApiResponse<object>> CreateLocationAsync(ClaimsPrincipal user, CreateTaxonomyDto dto, Guid? branchId)
    {
        if (branchId == null)
            throw new BusinessException("Khong tim thay chi nhanh hop le.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new BusinessException("Ten vi tri la bat buoc.");

        return new ApiResponse<object> { Success = true, Message = $"Vi tri '{dto.Name}' da duoc tao." };
    }

    public async Task<ApiResponse<object>> RenameLocationAsync(ClaimsPrincipal user, RenameTaxonomyDto dto, Guid? branchId)
    {
        if (branchId == null)
            throw new BusinessException("Khong tim thay chi nhanh hop le.");

        if (string.IsNullOrWhiteSpace(dto.OldName) || string.IsNullOrWhiteSpace(dto.NewName))
            throw new BusinessException("Du lieu khong hop le", "oldName va newName la bat buoc");

        var newName = dto.NewName.Trim();
        var oldName = dto.OldName.Trim();

        var affectedProducts = await _context.BranchProducts
            .Where(bp => bp.BranchId == branchId && bp.ShelfLocation != null && bp.ShelfLocation.ToLower() == oldName.ToLower())
            .ToListAsync();

        foreach (var bp in affectedProducts)
        {
            bp.ShelfLocation = newName;
        }

        await _context.SaveChangesAsync();
        return new ApiResponse<object> { Success = true, Message = $"Doi ten vi tri thanh cong." };
    }

    // ===== Private helper methods =====

    private static IQueryable<BranchProduct> ApplySorting(IQueryable<BranchProduct> query, string? sortBy, string? order)
    {
        bool isDesc = order?.ToLower() == "desc";
        string sort = sortBy?.ToLower() ?? "name";

        return sort switch
        {
            "code" => isDesc ? query.OrderByDescending(bp => bp.Product.ProductCode) : query.OrderBy(bp => bp.Product.ProductCode),
            "stock" => isDesc ? query.OrderByDescending(bp => bp.AvailableStock) : query.OrderBy(bp => bp.AvailableStock),
            "price" => isDesc ? query.OrderByDescending(bp => bp.SellPrice) : query.OrderBy(bp => bp.SellPrice),
            "cost" => isDesc ? query.OrderByDescending(bp => bp.CostPrice) : query.OrderBy(bp => bp.CostPrice),
            "createdat" => isDesc ? query.OrderByDescending(bp => bp.Product.CreatedAt) : query.OrderBy(bp => bp.Product.CreatedAt),
            _ => isDesc ? query.OrderByDescending(bp => bp.Product.ProductName) : query.OrderBy(bp => bp.Product.ProductName)
        };
    }

    private ProductUpsertDto MapToProductDto(BranchProduct bp)
    {
        var p = bp.Product;

        string productStatus = p.Status == 0 ? "inactive" : "active";
        string stockStatus = bp.ActualStock > 0
            ? (bp.ActualStock > (bp.MinimumStock ?? 0) ? "San hang" : "Sap het")
            : "Het hang";

        string statusTone = stockStatus switch
        {
            "San hang" => "green",
            "Sap het" => "amber",
            "Het hang" => "red",
            _ => "gray"
        };

        return new ProductUpsertDto
        {
            ProductId = p.ProductId,
            BranchProductId = bp.BranchProductId,
            Id = p.ProductId.ToString(),
            ProductCode = p.ProductCode,
            ProductName = p.ProductName,
            Barcode = p.Barcode,
            Unit = p.BaseUnit,
            BrandName = p.BrandName,
            CategoryName = p.CategoryName,
            SupplierId = p.SupplierId,
            SupplierName = p.Supplier?.SupplierName ?? "Khong xac dinh",
            ItemType = p.ItemType,
            CostPrice = bp.CostPrice,
            SalePrice = bp.SellPrice,
            ActualStock = bp.ActualStock,
            AvailableStock = bp.AvailableStock,
            WarrantyQuantity = bp.WarrantyQuantity,
            ReservedStock = bp.ReservedStock,
            MinimumStock = bp.MinimumStock ?? 0,
            MaximumStock = bp.MaximumStock ?? 0,
            ShelfLocation = bp.ShelfLocation,
            ShelfLocations = string.IsNullOrEmpty(bp.ShelfLocation) ? new() : new() { bp.ShelfLocation },
            Weight = p.Weight,
            WeightUnit = p.WeightUnit,
            Width = p.Width,
            Length = p.Length,
            Height = p.Height,
            SizeUnit = p.SizeUnit,
            Specification = p.Specification,
            SpecificationDetail = $"{p.Width ?? 0}x{p.Length ?? 0}x{p.Height ?? 0}",
            ImageUrl = p.MediaResources.FirstOrDefault(m => m.IsPrimary == 1)?.MediaUrl ?? p.MediaResources.FirstOrDefault()?.MediaUrl,
            Images = p.MediaResources.Select(m => m.MediaUrl).ToList(),
            DirectSale = bp.DirectSale == 1,
            SalesChannelLinked = p.SalesChannelLinked == 1,
            IsActive = bp.IsActive == 1,
            Attributes = p.ProductAttributes.Select(a => new ProductAttributeDto { Id = a.AttributeId, Name = a.AttributeName ?? "", Value = a.AttributeValue ?? "" }).ToList(),
            ConversionUnits = p.ProductConversionUnits.Select(c => new ProductConversionUnitDto { Id = c.ConversionId, Name = c.UnitName, Rate = c.ConvertValue, ConvertValue = c.ConvertValue, ConvertFrom = p.BaseUnit, Price = c.Price, DirectSale = c.DirectSale == 1 }).ToList(),
            Status = stockStatus,
            StatusTone = statusTone,
            ProductStatus = productStatus,
            EstimatedOutAt = p.CreatedAt?.AddDays(30),
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            CreatedBy = p.CreatedBy,
            CreatedByName = p.CreatedByUser?.FullName ?? "He thong",
            UpdatedBy = p.UpdatedBy,
            UpdatedByName = p.UpdatedByUser?.FullName ?? "Chua cap nhat"
        };
    }
}
