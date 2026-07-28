using AutoMapper;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MEP_Management_System.Controllers.InventoryController
{
    [ApiController]
    [Route("api/[controller]")]
    [Tags("Quản lý Kho (Inventory)")]
    public class ProductsController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly IMapper _mapper;
        private readonly ICurrentBranchAccessor _currentBranch;

        public ProductsController(MEPDatabaseContext context, IMapper mapper, ICurrentBranchAccessor currentBranch)
        {
            _context = context;
            _mapper = mapper;
            _currentBranch = currentBranch;
        }

        // Lấy UserId hiện tại từ JWT (dùng cho audit log)
        private Guid GetCurrentUserId()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid.TryParse(userIdStr, out Guid userId);
            return userId;
        }

        private Guid? GetCurrentBranchId()
        {
            return _currentBranch.GetBranchId();
        }

        private Guid? GetUserInfo()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (Guid.TryParse(userIdStr, out Guid userId))
            {
                return userId;
            }

            return null;
        }

        /// <summary>
        /// 1. GET /api/products - List all products with pagination, filtering, and sorting
        /// </summary>
        [HttpGet]
        [HasPermission(PermissionConstants.PRODUCT_VIEW)]
        public async Task<IActionResult> GetProducts([FromQuery] DTOs.InventoryDTOs.ProductParameters @params)
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ.",
                        Errors = new() { "No valid branch found." }
                    });
                }

                // Validate pagination
                if (@params.PageSize < 1 || @params.PageSize > 1000)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invalid pagination parameters",
                        Errors = new() { "PageSize must be between 1 and 1000" }
                    });
                }

                var query = _context.BranchProducts
                    .Include(bp => bp.Product).ThenInclude(p => p.Supplier)
                    .Include(bp => bp.Product).ThenInclude(p => p.MediaResources)
                    .Include(bp => bp.Product).ThenInclude(p => p.ProductAttributes)
                    .Include(bp => bp.Product).ThenInclude(p => p.ProductConversionUnits)
                    .Include(bp => bp.Product).ThenInclude(p => p.CreatedByUser)
                    .Include(bp => bp.Product).ThenInclude(p => p.UpdatedByUser)
                    .Where(bp => bp.BranchId == branchId).AsQueryable();

                // Exclude soft-deleted products
                query = query.Where(bp => bp.IsActive != -1);


                // Search by product name, code, or barcode
                if (!string.IsNullOrWhiteSpace(@params.SearchTerm))
                {
                    string search = @params.SearchTerm.Trim().ToLower();
                    query = query.Where(bp =>
                        bp.Product.ProductCode.ToLower().Contains(search) ||
                        bp.Product.ProductName.ToLower().Contains(search) ||
                        (bp.Product.Barcode != null && bp.Product.Barcode.ToLower().Contains(search))
                    );
                }

                // Filter by category
                if (!string.IsNullOrWhiteSpace(@params.CategoryName))
                {
                    query = query.Where(bp => bp.Product.CategoryName != null &&
                        bp.Product.CategoryName.ToLower() == @params.CategoryName.Trim().ToLower());
                }

                // Filter by brand
                if (!string.IsNullOrWhiteSpace(@params.BrandName))
                {
                    query = query.Where(bp => bp.Product.BrandName != null &&
                        bp.Product.BrandName.ToLower() == @params.BrandName.Trim().ToLower());
                }

                // Filter by status
                if (!string.IsNullOrWhiteSpace(@params.Status))
                {
                    short statusValue = @params.Status.ToLower() == "active" ? (short)1 : (short)0;
                    query = query.Where(bp => bp.IsActive == statusValue);
                }

                // Filter by supplier
                if (@params.SupplierId.HasValue)
                {
                    query = query.Where(bp => bp.Product.SupplierId == @params.SupplierId.Value);
                }

                // Sorting
                query = ApplySorting(query, @params.Sort, @params.Order);

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)@params.PageSize);
                var hasNextPage = @params.PageNumber < totalPages;
                var hasPreviousPage = @params.PageNumber > 1;

                var items = await query
                    .Skip((@params.PageNumber - 1) * @params.PageSize)
                    .Take(@params.PageSize)
                    .ToListAsync();

                var resultList = items.Select(bp => MapToProductDto(bp)).ToList();

                var paginatedData = new PaginatedResponse<ProductUpsertDto>
                {
                    Items = resultList,
                    TotalCount = totalCount,
                    PageNumber = @params.PageNumber,
                    PageSize = @params.PageSize,
                    TotalPages = totalPages,
                    HasNextPage = hasNextPage,
                    HasPreviousPage = hasPreviousPage
                };

                return Ok(new ApiResponse<PaginatedResponse<ProductUpsertDto>>
                {
                    Success = true,
                    Message = "Products retrieved successfully",
                    Data = paginatedData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// 1a. GET /api/products/search - Autocomplete tìm sản phẩm (dùng trong form nhập hàng).
        /// </summary>
        [HttpGet("search")]
        [HasPermission(PermissionConstants.PRODUCT_VIEW)]
        public async Task<IActionResult> SearchProducts([FromQuery] string term, [FromQuery] int limit = 10)
        {
            try
            {
                // 🔐 Quyền đã kiểm tra bằng [HasPermission] ở attribute
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ.",
                        Errors = new() { "No valid branch found." }
                    });
                }

                if (string.IsNullOrWhiteSpace(term))
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Empty term",
                        Data = new { items = new List<ProductSearchResultDto>() }
                    });
                }

                if (limit < 1) limit = 10;
                if (limit > 50) limit = 50;

                var trimmedTerm = term.Trim().ToLower();

                var results = await _context.BranchProducts
                    .Include(bp => bp.Product).ThenInclude(p => p.MediaResources)
                    .Where(bp => bp.BranchId == branchId
                        && bp.IsActive == 1
                        && bp.Product.Status == 1)
                    .Where(bp =>
                        bp.Product.ProductCode.ToLower().Contains(trimmedTerm)
                        || bp.Product.ProductName.ToLower().Contains(trimmedTerm)
                        || (bp.Product.Barcode != null && bp.Product.Barcode.ToLower().Contains(trimmedTerm))
                    )
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
                        ImageUrl = bp.Product.MediaResources
                            .Where(m => m.IsPrimary == 1)
                            .Select(m => m.MediaUrl)
                            .FirstOrDefault() ?? bp.Product.MediaResources.Select(m => m.MediaUrl).FirstOrDefault(),
                        CurrentStock = bp.AvailableStock,
                        CostPrice = bp.CostPrice,
                        SalePrice = bp.SellPrice
                    })
                    .ToListAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Found {results.Count} product(s)",
                    Data = new { items = results, total = results.Count }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// 1b. GET /api/products/suggest-new-product - Gợi ý thông tin sản phẩm mới inline
        /// </summary>
        [HttpGet("suggest-new-product")]
        [HasPermission(PermissionConstants.PRODUCT_VIEW)]
        public async Task<IActionResult> SuggestNewProduct([FromQuery] string term)
        {
            try
            {
                // 🔐 Quyền đã kiểm tra bằng [HasPermission] ở attribute

                if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Term phải có ít nhất 2 ký tự"
                    });
                }

                var namePart = term.Trim();

                // Get current branch for per-branch suggestions
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                // Sinh mã gợi ý không trùng trong chi nhánh hiện tại
                string baseCode = new string(namePart
                    .ToUpper()
                    .Replace(' ', '_')
                    .Where(c => char.IsLetterOrDigit(c) || c == '_')
                    .ToArray());

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

                // Gợi ý brand/category có sẵn TRONG CHI NHÁNH HIỆN TẠI
                var brandSuggestions = await _context.BranchProducts
                    .Include(bp => bp.Product)
                    .Where(bp => bp.BranchId == branchId && bp.Product.BrandName != null && bp.Product.BrandName != "")
                    .Select(bp => bp.Product.BrandName!)
                    .Distinct()
                    .OrderBy(b => b)
                    .Take(10)
                    .ToListAsync();

                var categorySuggestions = await _context.BranchProducts
                    .Include(bp => bp.Product)
                    .Where(bp => bp.BranchId == branchId && bp.Product.CategoryName != null && bp.Product.CategoryName != "")
                    .Select(bp => bp.Product.CategoryName!)
                    .Distinct()
                    .OrderBy(c => c)
                    .Take(10)
                    .ToListAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = new NewProductSuggestionDto
                    {
                        SuggestedCode = suggestedCode,
                        ProductName = namePart,
                        DefaultUnit = "Cái",
                        BrandSuggestions = brandSuggestions,
                        CategorySuggestions = categorySuggestions
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// 2. GET /api/products/{id} - Get product details by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        [HasPermission(PermissionConstants.PRODUCT_VIEW)]
        public async Task<IActionResult> GetProductById(Guid id)
        {
            try
            {
                var dto = await ProductService.GetProductByIdAsync(id, User, _context, _currentBranch, _mapper);
                if (dto == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Product not found",
                        Errors = new() { $"No product with ID: {id}" }
                    });
                }

                return Ok(new ApiResponse<ProductUpsertDto>
                {
                    Success = true,
                    Message = "Product retrieved successfully",
                    Data = dto
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Internal server error",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// 3. POST /api/products - Create product
        /// </summary>
        [HttpPost]
        [HasPermission(PermissionConstants.PRODUCT_CREATE)]
        public async Task<IActionResult> CreateProduct([FromBody] ProductUpsertDto dto)
        {
            try
            {
                var branchId = GetCurrentBranchId();

                var errors = new List<string>();
                if (string.IsNullOrWhiteSpace(dto.ProductCode)) errors.Add("productCode is required");
                if (string.IsNullOrWhiteSpace(dto.ProductName)) errors.Add("productName is required");
                if (string.IsNullOrWhiteSpace(dto.Unit)) errors.Add("unit is required");
                if (dto.CostPrice < 0) errors.Add("costPrice không được âm");
                if (dto.SalePrice < 0) errors.Add("salePrice không được âm");

                if (errors.Any())
                {
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Validation failed", Errors = errors });
                }

                // Check product code uniqueness WITHIN CURRENT BRANCH only
                bool isCodeExist = await _context.BranchProducts
                    .AnyAsync(bp => bp.BranchId == branchId.Value && bp.Product.ProductCode.ToLower() == dto.ProductCode.Trim().ToLower());
                if (isCodeExist)
                {
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Mã sản phẩm đã tồn tại trong chi nhánh này", Errors = new() { $"Code '{dto.ProductCode}' exists" } });
                }

                var currentLocalTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                Guid? currentUserId = GetUserInfo();

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var product = _mapper.Map<Product>(dto);
                    product.ProductId = Guid.NewGuid();
                    product.CreatedAt = currentLocalTime;
                    product.CreatedBy = currentUserId;

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
                                CreatedAt = currentLocalTime
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
                        // Validate ConversionUnits prices
                        var invalidUnits = dto.ConversionUnits.Where(u => u.Price < 0).ToList();
                        if (invalidUnits.Any())
                        {
                            return BadRequest(new ApiResponse<object>
                            {
                                Success = false,
                                Message = "ConversionUnits price validation failed",
                                Errors = new() { "Giá đơn vị quy đổi không được âm" }
                            });
                        }

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

                    // Chuẩn hoá ShelfLocation: ưu tiên ShelfLocation đầu tiên trong ShelfLocations
                    var primaryShelfLocation =
                        !string.IsNullOrWhiteSpace(dto.ShelfLocation)
                            ? dto.ShelfLocation
                            : (dto.ShelfLocations != null && dto.ShelfLocations.Count > 0
                                ? dto.ShelfLocations[0]
                                : null);

                    var branchProduct = new BranchProduct
                    {
                        BranchProductId = Guid.NewGuid(),
                        BranchId = branchId.Value,
                        ProductId = product.ProductId,
                        ActualStock = dto.ActualStock,
                        AvailableStock = dto.AvailableStock,
                        ReservedStock = dto.ReservedStock,
                        ShelfLocation = primaryShelfLocation ?? "Chưa xếp vị trí",
                        MinimumStock = dto.MinimumStock ?? 0,
                        MaximumStock = dto.MaximumStock ?? 0,
                        CostPrice = dto.CostPrice,
                        SellPrice = dto.SalePrice,
                        DirectSale = (short?)(dto.DirectSale ? 1 : 0),
                        Product = null!
                    };
                    _context.BranchProducts.Add(branchProduct);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return CreatedAtAction(nameof(GetProductById), new { id = product.ProductId },
                        new ApiResponse<ProductUpsertDto> { Success = true, Message = "Product created successfully", Data = MapToProductDto(branchProduct) });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Error creating product", Errors = new() { ex.Message } });
            }
        }

        /// <summary>
        /// 3b. POST /api/products/bulk-import - Bulk import products from Excel
        /// </summary>
        [HttpPost("bulk-import")]
        [HasPermission(PermissionConstants.PRODUCT_CREATE)]
        public async Task<IActionResult> BulkImportProducts([FromBody] ProductBulkImportDto dto)
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Branch not found" });
                }

                if (dto.Products == null || !dto.Products.Any())
                {
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "No products to import" });
                }

                var currentLocalTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                Guid? currentUserId = GetUserInfo();

                var successCount = 0;
                var errors = new List<string>();

                // Get existing categories and brands for this branch to reuse them or check existence
                var existingCategories = await _context.ProductTaxonomies
                    .Where(t => t.BranchId == branchId.Value && t.Type == "Category")
                    .ToListAsync();
                
                var existingBrands = await _context.ProductTaxonomies
                    .Where(t => t.BranchId == branchId.Value && t.Type == "Brand")
                    .ToListAsync();

                // Dictionaries to quickly find taxonomy by name (case-insensitive)
                var categoryDict = existingCategories.ToDictionary(c => c.Name.Trim().ToLower(), c => c);
                var brandDict = existingBrands.ToDictionary(b => b.Name.Trim().ToLower(), b => b);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var pDto in dto.Products)
                    {
                        var rowErrors = new List<string>();
                        if (string.IsNullOrWhiteSpace(pDto.ProductCode)) rowErrors.Add("Mã sản phẩm trống");
                        if (string.IsNullOrWhiteSpace(pDto.ProductName)) rowErrors.Add("Tên sản phẩm trống");
                        if (string.IsNullOrWhiteSpace(pDto.Unit)) rowErrors.Add("Đơn vị trống");
                        if (pDto.CostPrice < 0) rowErrors.Add("Giá vốn không được âm");
                        if (pDto.SalePrice < 0) rowErrors.Add("Giá bán không được âm");

                        if (rowErrors.Any())
                        {
                            errors.Add($"Sản phẩm '{pDto.ProductName ?? "Không tên"}': {string.Join(", ", rowErrors)}");
                            continue;
                        }

                        // Check existing code
                        bool isCodeExist = await _context.BranchProducts
                            .AnyAsync(bp => bp.BranchId == branchId.Value && bp.Product.ProductCode.ToLower() == pDto.ProductCode.Trim().ToLower());
                        
                        if (isCodeExist)
                        {
                            errors.Add($"Sản phẩm '{pDto.ProductName}': Mã '{pDto.ProductCode}' đã tồn tại");
                            continue;
                        }

                        // Auto-create category if missing
                        if (dto.AutoCreateMissingCategories && !string.IsNullOrWhiteSpace(pDto.CategoryName))
                        {
                            var catName = pDto.CategoryName.Trim();
                            var catKey = catName.ToLower();
                            if (!categoryDict.ContainsKey(catKey))
                            {
                                var newCat = new ProductTaxonomy
                                {
                                    TaxonomyId = Guid.NewGuid(),
                                    BranchId = branchId.Value,
                                    Name = catName,
                                    Type = "Category",
                                    CreatedAt = currentLocalTime
                                };
                                _context.ProductTaxonomies.Add(newCat);
                                categoryDict[catKey] = newCat; // Add to dict to prevent duplicates in same import
                            }
                            // Assign Taxonomy ID to product
                            pDto.CategoryTaxonomyId = categoryDict[catKey].TaxonomyId;
                        }

                        // Auto-create brand if missing
                        if (dto.AutoCreateMissingBrands && !string.IsNullOrWhiteSpace(pDto.BrandName))
                        {
                            var brandName = pDto.BrandName.Trim();
                            var brandKey = brandName.ToLower();
                            if (!brandDict.ContainsKey(brandKey))
                            {
                                var newBrand = new ProductTaxonomy
                                {
                                    TaxonomyId = Guid.NewGuid(),
                                    BranchId = branchId.Value,
                                    Name = brandName,
                                    Type = "Brand",
                                    CreatedAt = currentLocalTime
                                };
                                _context.ProductTaxonomies.Add(newBrand);
                                brandDict[brandKey] = newBrand;
                            }
                            pDto.BrandTaxonomyId = brandDict[brandKey].TaxonomyId;
                        }

                        var product = _mapper.Map<Product>(pDto);
                        product.ProductId = Guid.NewGuid();
                        product.CreatedAt = currentLocalTime;
                        product.CreatedBy = currentUserId;
                        
                        product.ProductConversionUnits = new List<ProductConversionUnit>();
                        product.BranchProducts = new List<BranchProduct>();

                        _context.Products.Add(product);

                        if (pDto.Images != null && pDto.Images.Any())
                        {
                            foreach (var imgUrl in pDto.Images)
                            {
                                _context.MediaResources.Add(new MediaResource
                                {
                                    MediaId = Guid.NewGuid(),
                                    ProductId = product.ProductId,
                                    MediaUrl = imgUrl.Trim(),
                                    MediaType = "IMAGE",
                                    IsPrimary = (short)(imgUrl.Trim() == pDto.ImageUrl?.Trim() ? 1 : 0),
                                    CreatedAt = currentLocalTime
                                });
                            }
                        }

                        var branchProduct = new BranchProduct
                        {
                            BranchProductId = Guid.NewGuid(),
                            BranchId = branchId.Value,
                            ProductId = product.ProductId,
                            ActualStock = pDto.ActualStock,
                            AvailableStock = pDto.AvailableStock,
                            ReservedStock = pDto.ReservedStock,
                            ShelfLocation = !string.IsNullOrWhiteSpace(pDto.ShelfLocation) ? pDto.ShelfLocation : "Chưa xếp vị trí",
                            MinimumStock = pDto.MinimumStock ?? 0,
                            MaximumStock = pDto.MaximumStock ?? 0,
                            CostPrice = pDto.CostPrice,
                            SellPrice = pDto.SalePrice,
                            DirectSale = (short?)(pDto.DirectSale ? 1 : 0),
                            Product = null!
                        };
                        _context.BranchProducts.Add(branchProduct);

                        successCount++;
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = $"Nhập thành công {successCount} sản phẩm" + (errors.Any() ? $", bỏ qua {errors.Count} sản phẩm lỗi." : "."),
                        Data = new { successCount, errors }
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi khi import", Errors = new() { ex.Message } });
            }
        }

        /// <summary>
        /// 4. PUT /api/products/{id} - Update product
        /// </summary>
        [HttpPut("{id:guid}")]
        [HasPermission(PermissionConstants.PRODUCT_UPDATE)]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] ProductUpsertDto dto)
        {
            try
            {
                var branchId = GetCurrentBranchId();

                var bp = await _context.BranchProducts
                    .Include(b => b.Product).ThenInclude(p => p.MediaResources)
                    .Include(b => b.Product).ThenInclude(p => p.ProductAttributes)
                    .Include(b => b.Product).ThenInclude(p => p.ProductConversionUnits)
                    .FirstOrDefaultAsync(b => b.ProductId == id && b.BranchId == branchId);

                if (bp == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Product not found",
                        Errors = new() { $"No product with ID: {id} in the branch" }
                    });
                }

                var errors = new List<string>();
                if (string.IsNullOrWhiteSpace(dto.ProductCode)) errors.Add("productCode is required");
                if (string.IsNullOrWhiteSpace(dto.ProductName)) errors.Add("productName is required");
                if (string.IsNullOrWhiteSpace(dto.Unit)) errors.Add("unit is required");
                if (dto.CostPrice < 0) errors.Add("costPrice không được âm");
                if (dto.SalePrice < 0) errors.Add("salePrice không được âm");

                if (errors.Any())
                {
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Validation failed", Errors = errors });
                }

                bool isCodeExist = await _context.Products.AnyAsync(p => p.ProductId != id && p.ProductCode.ToLower() == dto.ProductCode.Trim().ToLower());
                if (isCodeExist)
                {
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Product code already exists", Errors = new() { $"Code '{dto.ProductCode}' exists" } });
                }

                // UOM: Không cho sửa BaseUnit khi sản phẩm đã có giao dịch
                if (bp.Product.BaseUnit != dto.Unit)
                {
                    var hasTransaction = await _context.InvoiceItems
                        .AnyAsync(ii => ii.BranchProduct != null && ii.BranchProduct.ProductId == id);

                    if (hasTransaction)
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Không thể thay đổi đơn vị cơ bản khi sản phẩm đã có giao dịch.",
                            Errors = new() { "Sản phẩm đã xuất hiện trong hóa đơn. Không thể thay đổi BaseUnit." }
                        });
                    }
                }

                var currentLocalTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                Guid? currentUserId = GetUserInfo();

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var originalProductId = bp.Product.ProductId;
                    _mapper.Map(dto, bp.Product);
                    bp.Product.ProductId = originalProductId;

                    bp.Product.UpdatedAt = currentLocalTime;
                    bp.Product.UpdatedBy = currentUserId;

                    // Đảm bảo SizeUnit được lưu (DTO có, Product có, AutoMapper xử lý đúng)
                    if (!string.IsNullOrWhiteSpace(dto.SizeUnit))
                        bp.Product.SizeUnit = dto.SizeUnit;

                    _context.Products.Update(bp.Product);

                    bp.ActualStock = dto.ActualStock;
                    bp.AvailableStock = dto.AvailableStock;
                    bp.ReservedStock = dto.ReservedStock;

                    // Chuẩn hoá ShelfLocation: ưu tiên ShelfLocation đầu tiên trong ShelfLocations
                    var primaryShelfLocation =
                        !string.IsNullOrWhiteSpace(dto.ShelfLocation)
                            ? dto.ShelfLocation
                            : (dto.ShelfLocations != null && dto.ShelfLocations.Count > 0
                                ? dto.ShelfLocations[0]
                                : null);
                    bp.ShelfLocation = primaryShelfLocation ?? bp.ShelfLocation ?? "Chưa xếp vị trí";

                    // Lưu MinimumStock / MaximumStock vào BranchProduct (vì Product model không có 2 field này)
                    bp.MinimumStock = dto.MinimumStock ?? bp.MinimumStock ?? 0;
                    bp.MaximumStock = dto.MaximumStock ?? bp.MaximumStock ?? 0;

                    bp.CostPrice = dto.CostPrice;
                    bp.SellPrice = dto.SalePrice;
                    bp.DirectSale = (short?)(dto.DirectSale ? 1 : 0);

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
                                CreatedAt = currentLocalTime
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

                    return Ok(new ApiResponse<ProductUpsertDto>
                    {
                        Success = true,
                        Message = "Product updated successfully",
                        Data = MapToProductDto(updatedBp!)
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Error updating product", Errors = new() { ex.Message } });
            }
        }

        
        [HttpDelete("{id:guid}")]
        [HasPermission(PermissionConstants.PRODUCT_DELETE)]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            try
            {
                var branchId = GetCurrentBranchId();

                var bp = await _context.BranchProducts
                    .Include(b => b.Product)
                    .FirstOrDefaultAsync(b => b.ProductId == id && b.BranchId == branchId);

                if (bp == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Product not found",
                        Errors = new() { $"No product with ID: {id}" }
                    });
                }

                bp.IsActive = -1;
                if (bp.Product != null)
                {
                    bp.Product.Status = -1;
                    _context.Products.Update(bp.Product);
                }

                _context.BranchProducts.Update(bp);
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Product deleted successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error deleting product",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// 6. PATCH /api/products/{id}/toggle-status - Quick status toggle
        /// </summary>
        [HttpPatch("{id:guid}/toggle-status")]
        [HasPermission(PermissionConstants.PRODUCT_UPDATE)]
        public async Task<IActionResult> ToggleStatus(Guid id, [FromQuery] bool isActive)
        {
            try
            {
                var branchId = GetCurrentBranchId();

                var bp = await _context.BranchProducts
                    .Include(b => b.Product)
                    .FirstOrDefaultAsync(b => b.ProductId == id && b.BranchId == branchId);

                if (bp == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Product not found",
                        Errors = new() { $"No product with ID {id} belongs to this branch" }
                    });
                }

                short statusVal = isActive ? (short)1 : (short)0;
                bp.IsActive = statusVal;

                if (bp.Product != null)
                {
                    bp.Product.Status = statusVal;
                    _context.Products.Update(bp.Product);
                }

                _context.BranchProducts.Update(bp);
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Product status updated to {(isActive ? "Active" : "Inactive")} successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error toggling product status",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// 7. PATCH /api/products/toggle-status-bulk - Bulk toggle status
        /// </summary>
        [HttpPatch("toggle-status-bulk")]
        [HasPermission(PermissionConstants.PRODUCT_UPDATE)]
        public async Task<IActionResult> ToggleStatusBulk([FromBody] BulkToggleStatusRequest request)
        {
            try
            {
                var branchId = GetCurrentBranchId();

                if (request?.Ids == null || !request.Ids.Any())
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = new() { "List of product IDs cannot be empty" }
                    });
                }

                if (request.Ids.Count > 500)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = new() { "Maximum 500 ids per request" }
                    });
                }

                var bps = await _context.BranchProducts
                    .Include(b => b.Product)
                    .Where(b => b.BranchId == branchId && request.Ids.Contains(b.ProductId))
                    .ToListAsync();

                if (!bps.Any())
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "No products found",
                        Errors = new() { "None of the provided ids belong to the current branch" }
                    });
                }

                var updatedIds = new List<Guid>();
                var notFoundIds = request.Ids.Except(bps.Select(b => b.ProductId)).ToList();

                foreach (var bp in bps)
                {
                    short statusVal = request.IsActive ? (short)1 : (short)0;
                    bp.IsActive = statusVal;

                    if (bp.Product != null)
                    {
                        bp.Product.Status = statusVal;
                        _context.Products.Update(bp.Product);
                    }

                    _context.BranchProducts.Update(bp);
                    updatedIds.Add(bp.ProductId);
                }

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Bulk status update completed. Updated {updatedIds.Count} product(s).",
                    Data = new
                    {
                        UpdatedCount = updatedIds.Count,
                        UpdatedIds = updatedIds,
                        NotFoundCount = notFoundIds.Count,
                        NotFoundIds = notFoundIds
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error during bulk status updating",
                    Errors = new() { ex.Message }
                });
            }
        }

        private IQueryable<BranchProduct> ApplySorting(IQueryable<BranchProduct> query, string sortBy, string order)
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

            // Determine product status
            string productStatus = p.Status == 0 ? "inactive" : "active";

            // Determine stock status (so sánh với MinimumStock thay vì Weight)
            string stockStatus = bp.ActualStock > 0
                ? (bp.ActualStock > (bp.MinimumStock ?? 0) ? "Sẵn hàng" : "Sắp hết")
                : "Hết hàng";

            string statusTone = stockStatus switch
            {
                "Sẵn hàng" => "green",
                "Sắp hết" => "amber",
                "Hết hàng" => "red",
                _ => "gray"
            };

            return new ProductUpsertDto
            {
                // Identity
                ProductId = p.ProductId,
                BranchProductId = bp.BranchProductId,
                Id = p.ProductId.ToString(),
                ProductCode = p.ProductCode,
                ProductName = p.ProductName,
                Barcode = p.Barcode,

                // Basic Info
                Unit = p.BaseUnit,
                BrandName = p.BrandName,
                CategoryName = p.CategoryName,
                SupplierId = p.SupplierId,
                SupplierName = p.Supplier?.SupplierName ?? "Không xác định",
                ItemType = p.ItemType,

                // Pricing
                CostPrice = bp.CostPrice,
                SalePrice = bp.SellPrice,

                // Stock
                ActualStock = bp.ActualStock,
                AvailableStock = bp.AvailableStock,
                ReservedStock = bp.ReservedStock,

                // Lấy MinimumStock / MaximumStock từ BranchProduct (không phải Weight)
                MinimumStock = bp.MinimumStock ?? 0,
                MaximumStock = bp.MaximumStock ?? 0,

                // Location & Dimensions
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

                // Media
                ImageUrl = p.MediaResources.FirstOrDefault(m => m.IsPrimary == 1)?.MediaUrl
                    ?? p.MediaResources.FirstOrDefault()?.MediaUrl,
                Images = p.MediaResources.Select(m => m.MediaUrl).ToList(),

                // Sales & Status
                DirectSale = bp.DirectSale == 1 || bp.DirectSale == null,
                SalesChannelLinked = p.SalesChannelLinked == 1,
                IsActive = bp.IsActive == 1,

                // Nested DTOs
                Attributes = p.ProductAttributes.Select(a => new ProductAttributeDto
                {
                    Id = a.AttributeId,
                    Name = a.AttributeName ?? "",
                    Value = a.AttributeValue ?? ""
                }).ToList(),

                ConversionUnits = p.ProductConversionUnits.Select(c => new ProductConversionUnitDto
                {
                    Id = c.ConversionId,
                    Name = c.UnitName,
                    Rate = c.ConvertValue,
                    ConvertValue = c.ConvertValue,
                    ConvertFrom = p.BaseUnit,
                    Price = c.Price,
                    DirectSale = c.DirectSale == 1
                }).ToList(),

                // Frontend display fields
                Status = stockStatus,
                StatusTone = statusTone,
                ProductStatus = productStatus,

                // Timestamps
                EstimatedOutAt = p.CreatedAt?.AddDays(30),
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,

                // Trả thông tin định danh người tạo và sửa đổi
                CreatedBy = p.CreatedBy,
                CreatedByName = p.CreatedByUser?.FullName ?? "Hệ thống",
                UpdatedBy = p.UpdatedBy,
                UpdatedByName = p.UpdatedByUser?.FullName ?? "Chưa cập nhật"
            };
        }


        /// <summary>
        /// GET /api/products/categories - Danh sách nhóm hàng (distinct) kèm số SP đang dùng
        /// </summary>
        [HttpGet("categories")]
        [HasPermission(PermissionConstants.PRODUCT_VIEW)]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                var productGroupings = await _context.BranchProducts
                    .Include(bp => bp.Product)
                    .Where(bp => bp.BranchId == branchId && bp.Product.CategoryName != null && bp.Product.CategoryName != "")
                    .GroupBy(bp => bp.Product.CategoryName)
                    .Select(g => new { Name = g.Key!, Count = g.Count() })
                    .ToListAsync();

                var taxonomies = await _context.ProductTaxonomies
                    .Where(t => t.BranchId == branchId && t.Type == "Category")
                    .Select(t => t.Name)
                    .ToListAsync();

                var dict = productGroupings.ToDictionary(g => g.Name, g => g.Count);
                foreach (var tax in taxonomies)
                {
                    if (!dict.ContainsKey(tax)) dict[tax] = 0;
                }

                var result = dict.Select(kv => new TaxonomyItemDto
                {
                    Name = kv.Key,
                    ProductCount = kv.Value
                })
                .OrderBy(x => x.Name)
                .ToList();

                return Ok(new ApiResponse<List<TaxonomyItemDto>>
                {
                    Success = true,
                    Message = "Lấy danh sách nhóm hàng thành công",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi lấy danh sách nhóm hàng",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// GET /api/products/brands - Danh sách thương hiệu (distinct) kèm số SP đang dùng
        /// </summary>
        [HttpGet("brands")]
        [HasPermission(PermissionConstants.PRODUCT_VIEW)]
        public async Task<IActionResult> GetBrands()
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                var productGroupings = await _context.BranchProducts
                    .Include(bp => bp.Product)
                    .Where(bp => bp.BranchId == branchId && bp.Product.BrandName != null && bp.Product.BrandName != "")
                    .GroupBy(bp => bp.Product.BrandName)
                    .Select(g => new { Name = g.Key!, Count = g.Count() })
                    .ToListAsync();

                var taxonomies = await _context.ProductTaxonomies
                    .Where(t => t.BranchId == branchId && t.Type == "Brand")
                    .Select(t => t.Name)
                    .ToListAsync();

                var dict = productGroupings.ToDictionary(g => g.Name, g => g.Count);
                foreach (var tax in taxonomies)
                {
                    if (!dict.ContainsKey(tax)) dict[tax] = 0;
                }

                var result = dict.Select(kv => new TaxonomyItemDto
                {
                    Name = kv.Key,
                    ProductCount = kv.Value
                })
                .OrderBy(x => x.Name)
                .ToList();

                return Ok(new ApiResponse<List<TaxonomyItemDto>>
                {
                    Success = true,
                    Message = "Lấy danh sách thương hiệu thành công",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi lấy danh sách thương hiệu",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// POST /api/products/categories - Thêm mới nhóm hàng (category)
        /// </summary>
        [HttpPost("categories")]
        [HasPermission(PermissionConstants.PRODUCT_CREATE)]
        public async Task<IActionResult> CreateCategory([FromBody] CreateTaxonomyDto dto)
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Tên nhóm hàng là bắt buộc."
                    });
                }

                var trimmedName = dto.Name.Trim();
                // Check per-branch only
                var exists = await _context.BranchProducts
                    .Include(bp => bp.Product)
                    .AnyAsync(bp => bp.BranchId == branchId && bp.Product.CategoryName != null && bp.Product.CategoryName.ToLower() == trimmedName.ToLower());
                var existsInTaxonomies = await _context.ProductTaxonomies
                    .AnyAsync(t => t.BranchId == branchId && t.Type == "Category" && t.Name.ToLower() == trimmedName.ToLower());

                if (exists || existsInTaxonomies)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Nhóm hàng '{trimmedName}' đã tồn tại trong chi nhánh này."
                    });
                }

                var tax = new ProductTaxonomy
                {
                    BranchId = branchId,
                    Name = trimmedName,
                    Type = "Category"
                };
                _context.ProductTaxonomies.Add(tax);
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Nhóm hàng '{trimmedName}' đã được thêm.",
                    Data = new { Name = trimmedName }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi thêm nhóm hàng",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// POST /api/products/brands - Thêm mới thương hiệu (brand)
        /// </summary>
        [HttpPost("brands")]
        [HasPermission(PermissionConstants.PRODUCT_CREATE)]
        public async Task<IActionResult> CreateBrand([FromBody] CreateTaxonomyDto dto)
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Tên thương hiệu là bắt buộc."
                    });
                }

                var trimmedName = dto.Name.Trim();
                // Check per-branch only
                var exists = await _context.BranchProducts
                    .Include(bp => bp.Product)
                    .AnyAsync(bp => bp.BranchId == branchId && bp.Product.BrandName != null && bp.Product.BrandName.ToLower() == trimmedName.ToLower());
                var existsInTaxonomies = await _context.ProductTaxonomies
                    .AnyAsync(t => t.BranchId == branchId && t.Type == "Brand" && t.Name.ToLower() == trimmedName.ToLower());

                if (exists || existsInTaxonomies)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Thương hiệu '{trimmedName}' đã tồn tại trong chi nhánh này."
                    });
                }

                var tax = new ProductTaxonomy
                {
                    BranchId = branchId,
                    Name = trimmedName,
                    Type = "Brand"
                };
                _context.ProductTaxonomies.Add(tax);
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Thương hiệu '{trimmedName}' đã được thêm.",
                    Data = new { Name = trimmedName }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi thêm thương hiệu",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// PUT /api/products/categories/rename - Đổi tên nhóm hàng (bulk update mọi SP đang dùng tên cũ)
        /// </summary>
        [HttpPut("categories/rename")]
        [HasPermission(PermissionConstants.PRODUCT_UPDATE)]
        public async Task<IActionResult> RenameCategory([FromBody] RenameTaxonomyDto dto)
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                if (string.IsNullOrWhiteSpace(dto.OldName) || string.IsNullOrWhiteSpace(dto.NewName))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ",
                        Errors = new() { "oldName và newName là bắt buộc" }
                    });
                }

                var newName = dto.NewName.Trim();
                var oldName = dto.OldName.Trim();

                var affectedProducts = await _context.BranchProducts
                    .Include(bp => bp.Product)
                    .Where(bp => bp.BranchId == branchId && bp.Product.CategoryName != null && bp.Product.CategoryName.ToLower() == oldName.ToLower())
                    .Select(bp => bp.Product)
                    .ToListAsync();

                var tax = await _context.ProductTaxonomies
                    .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == "Category" && t.Name.ToLower() == oldName.ToLower());

                if (!affectedProducts.Any() && tax == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy nhóm hàng",
                        Errors = new() { $"Không có dữ liệu về nhóm hàng '{oldName}'" }
                    });
                }

                var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                var currentUserId = GetUserInfo();

                foreach (var product in affectedProducts)
                {
                    product.CategoryName = newName;
                    product.UpdatedAt = currentTime;
                    product.UpdatedBy = currentUserId;
                }

                if (tax != null)
                {
                    tax.Name = newName;
                }

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Đã đổi tên nhóm hàng '{oldName}' thành '{newName}' cho {affectedProducts.Count} sản phẩm",
                    Data = new { UpdatedCount = affectedProducts.Count }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi đổi tên nhóm hàng",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// PUT /api/products/brands/rename - Đổi tên thương hiệu (bulk update mọi SP đang dùng tên cũ)
        /// </summary>
        [HttpPut("brands/rename")]
        [HasPermission(PermissionConstants.PRODUCT_UPDATE)]
        public async Task<IActionResult> RenameBrand([FromBody] RenameTaxonomyDto dto)
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                if (string.IsNullOrWhiteSpace(dto.OldName) || string.IsNullOrWhiteSpace(dto.NewName))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Dữ liệu không hợp lệ",
                        Errors = new() { "oldName và newName là bắt buộc" }
                    });
                }

                var newName = dto.NewName.Trim();
                var oldName = dto.OldName.Trim();

                var affectedProducts = await _context.BranchProducts
                    .Include(bp => bp.Product)
                    .Where(bp => bp.BranchId == branchId && bp.Product.BrandName != null && bp.Product.BrandName.ToLower() == oldName.ToLower())
                    .Select(bp => bp.Product)
                    .ToListAsync();

                var tax = await _context.ProductTaxonomies
                    .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == "Brand" && t.Name.ToLower() == oldName.ToLower());

                if (!affectedProducts.Any() && tax == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy thương hiệu",
                        Errors = new() { $"Không có dữ liệu về thương hiệu '{oldName}'" }
                    });
                }

                var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                var currentUserId = GetUserInfo();

                foreach (var product in affectedProducts)
                {
                    product.BrandName = newName;
                    product.UpdatedAt = currentTime;
                    product.UpdatedBy = currentUserId;
                }

                if (tax != null)
                {
                    tax.Name = newName;
                }

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Đã đổi tên thương hiệu '{oldName}' thành '{newName}' cho {affectedProducts.Count} sản phẩm",
                    Data = new { UpdatedCount = affectedProducts.Count }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi đổi tên thương hiệu",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// DELETE /api/products/categories/{name} - Gỡ nhóm hàng khỏi tất cả SP đang dùng (set về null)
        /// </summary>
        [HttpDelete("categories/{name}")]
        [HasPermission(PermissionConstants.PRODUCT_UPDATE)]
        public async Task<IActionResult> DeleteCategory(string name)
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                var trimmed = name.Trim();

                var affectedProducts = await _context.BranchProducts
                    .Include(bp => bp.Product)
                    .Where(bp => bp.BranchId == branchId && bp.Product.CategoryName != null && bp.Product.CategoryName.ToLower() == trimmed.ToLower())
                    .Select(bp => bp.Product)
                    .ToListAsync();

                var tax = await _context.ProductTaxonomies
                    .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == "Category" && t.Name.ToLower() == trimmed.ToLower());

                if (!affectedProducts.Any() && tax == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy nhóm hàng",
                        Errors = new() { $"Không có dữ liệu về nhóm hàng '{trimmed}'" }
                    });
                }

                var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                var currentUserId = GetUserInfo();

                foreach (var product in affectedProducts)
                {
                    product.CategoryName = null;
                    product.UpdatedAt = currentTime;
                    product.UpdatedBy = currentUserId;
                }

                if (tax != null)
                {
                    _context.ProductTaxonomies.Remove(tax);
                }

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Đã gỡ nhóm hàng '{trimmed}' khỏi {affectedProducts.Count} sản phẩm",
                    Data = new { UpdatedCount = affectedProducts.Count }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi xoá nhóm hàng",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// DELETE /api/products/brands/{name} - Gỡ thương hiệu khỏi tất cả SP đang dùng (set về null)
        /// </summary>
        [HttpDelete("brands/{name}")]
        [HasPermission(PermissionConstants.PRODUCT_UPDATE)]
        public async Task<IActionResult> DeleteBrand(string name)
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                var trimmed = name.Trim();

                var affectedProducts = await _context.BranchProducts
                    .Include(bp => bp.Product)
                    .Where(bp => bp.BranchId == branchId && bp.Product.BrandName != null && bp.Product.BrandName.ToLower() == trimmed.ToLower())
                    .Select(bp => bp.Product)
                    .ToListAsync();

                var tax = await _context.ProductTaxonomies
                    .FirstOrDefaultAsync(t => t.BranchId == branchId && t.Type == "Brand" && t.Name.ToLower() == trimmed.ToLower());

                if (!affectedProducts.Any() && tax == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy thương hiệu",
                        Errors = new() { $"Không có dữ liệu về thương hiệu '{trimmed}'" }
                    });
                }

                var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                var currentUserId = GetUserInfo();

                foreach (var product in affectedProducts)
                {
                    product.BrandName = null;
                    product.UpdatedAt = currentTime;
                    product.UpdatedBy = currentUserId;
                }

                if (tax != null)
                {
                    _context.ProductTaxonomies.Remove(tax);
                }

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Đã gỡ thương hiệu '{trimmed}' khỏi {affectedProducts.Count} sản phẩm",
                    Data = new { UpdatedCount = affectedProducts.Count }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi xoá thương hiệu",
                    Errors = new() { ex.Message }
                });
            }
        }

        #region Location Management APIs

        /// <summary>
        /// GET /api/products/locations - Danh sách vị trí kho (distinct)
        /// </summary>
        [HttpGet("locations")]
        [HasPermission(PermissionConstants.PRODUCT_VIEW)]
        public async Task<IActionResult> GetLocations()
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                var result = await _context.BranchProducts
                    .Where(bp => bp.BranchId == branchId && bp.ShelfLocation != null && bp.ShelfLocation != "")
                    .Select(bp => bp.ShelfLocation!)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();

                return Ok(new ApiResponse<List<string>>
                {
                    Success = true,
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi lấy danh sách vị trí",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// POST /api/products/locations - Thêm mới vị trí kho
        /// </summary>
        [HttpPost("locations")]
        [HasPermission(PermissionConstants.PRODUCT_CREATE)]
        public async Task<IActionResult> CreateLocation([FromBody] CreateTaxonomyDto dto)
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                if (string.IsNullOrWhiteSpace(dto.Name))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Tên vị trí là bắt buộc."
                    });
                }

                var trimmedName = dto.Name.Trim();
                var exists = await _context.BranchProducts
                    .AnyAsync(bp => bp.BranchId == branchId && bp.ShelfLocation != null && bp.ShelfLocation.ToLower() == trimmedName.ToLower());

                if (exists)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Vị trí '{trimmedName}' đã tồn tại trong chi nhánh này."
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Vị trí '{trimmedName}' đã được thêm.",
                    Data = new { Name = trimmedName }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi thêm vị trí",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// PUT /api/products/locations - Đổi tên vị trí kho (bulk update)
        /// </summary>
        [HttpPut("locations")]
        [HasPermission(PermissionConstants.PRODUCT_UPDATE)]
        public async Task<IActionResult> RenameLocation([FromBody] RenameTaxonomyDto dto)
        {
            var branchId = GetCurrentBranchId();
            if (branchId == null)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Không tìm thấy chi nhánh hợp lệ."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.OldName) || string.IsNullOrWhiteSpace(dto.NewName))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Tên cũ và tên mới là bắt buộc."
                });
            }

            var oldTrimmed = dto.OldName.Trim();
            var newTrimmed = dto.NewName.Trim();

            // Sử dụng transaction để tránh race condition
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Kiểm tra trùng tên location mới với lock
                var newLocationExists = await _context.BranchProducts
                    .AnyAsync(bp => bp.BranchId == branchId &&
                        bp.ShelfLocation != null &&
                        bp.ShelfLocation.ToLower() == newTrimmed.ToLower());

                if (newLocationExists && oldTrimmed.ToLower() != newTrimmed.ToLower())
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Vị trí '{newTrimmed}' đã tồn tại"
                    });
                }

                // Lấy và update trong cùng transaction
                var affectedProducts = await _context.BranchProducts
                    .Where(bp => bp.BranchId == branchId &&
                        bp.ShelfLocation != null &&
                        bp.ShelfLocation.ToLower() == oldTrimmed.ToLower())
                    .ToListAsync();

                foreach (var bp in affectedProducts)
                {
                    bp.ShelfLocation = newTrimmed;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var message = affectedProducts.Count > 0
                    ? $"Đã đổi tên vị trí '{oldTrimmed}' thành '{newTrimmed}' cho {affectedProducts.Count} sản phẩm"
                    : $"Đã đổi tên vị trí '{oldTrimmed}' thành '{newTrimmed}'. Vị trí cũ chưa được sử dụng.";

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = message,
                    Data = new { UpdatedCount = affectedProducts.Count }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi đổi tên vị trí",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// DELETE /api/products/locations/{locationName} - Xóa vị trí kho
        /// </summary>
        [HttpDelete("locations/{locationName}")]
        [HasPermission(PermissionConstants.PRODUCT_DELETE)]
        public async Task<IActionResult> DeleteLocation(string locationName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(locationName))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Tên vị trí là bắt buộc."
                    });
                }

                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy chi nhánh hợp lệ."
                    });
                }

                var trimmed = locationName.Trim();

                var productsWithLocation = await _context.BranchProducts
                    .Where(bp => bp.BranchId == branchId && bp.ShelfLocation != null && bp.ShelfLocation.ToLower() == trimmed.ToLower())
                    .CountAsync();

                if (productsWithLocation > 0)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Không thể xóa vị trí '{trimmed}' vì có {productsWithLocation} sản phẩm đang sử dụng vị trí này."
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Đã xóa vị trí '{trimmed}'"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi xóa vị trí",
                    Errors = new() { ex.Message }
                });
            }
        }

        #endregion

        #region Attribute Type Management APIs

        /// <summary>
        /// GET /api/products/attribute-types - Danh sách loại thuộc tính
        /// </summary>
        [HttpGet("attribute-types")]
        [HasPermission(PermissionConstants.PRODUCT_VIEW)]
        public async Task<IActionResult> GetAttributeTypes()
        {
            try
            {
                var result = await _context.ProductAttributeTypes
                    .Where(t => t.IsActive == 1)
                    .OrderBy(t => t.TypeName)
                    .Select(t => new
                    {
                        t.TypeId,
                        t.TypeName,
                        t.TypeCode
                    })
                    .ToListAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi lấy danh sách loại thuộc tính",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// POST /api/products/attribute-types - Thêm mới loại thuộc tính
        /// </summary>
        [HttpPost("attribute-types")]
        [HasPermission(PermissionConstants.PRODUCT_CREATE)]
        public async Task<IActionResult> CreateAttributeType([FromBody] CreateAttributeTypeDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.TypeName))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Tên loại thuộc tính là bắt buộc."
                    });
                }

                var exists = await _context.ProductAttributeTypes
                    .AnyAsync(t => t.TypeName.ToLower() == dto.TypeName.Trim().ToLower() && t.IsActive == 1);

                if (exists)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Loại thuộc tính '{dto.TypeName}' đã tồn tại."
                    });
                }

                var userId = GetUserInfo();
                var currentTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                var attributeType = new ProductAttributeType
                {
                    TypeId = Guid.NewGuid(),
                    TypeName = dto.TypeName.Trim(),
                    TypeCode = !string.IsNullOrWhiteSpace(dto.TypeCode) ? dto.TypeCode.Trim().ToUpper() : null,
                    IsActive = 1,
                    CreatedAt = currentTime,
                    CreatedBy = userId
                };

                _context.ProductAttributeTypes.Add(attributeType);
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Đã thêm loại thuộc tính '{attributeType.TypeName}'",
                    Data = new
                    {
                        attributeType.TypeId,
                        attributeType.TypeName,
                        attributeType.TypeCode
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi thêm loại thuộc tính",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// PUT /api/products/attribute-types/{id} - Cập nhật loại thuộc tính
        /// </summary>
        [HttpPut("attribute-types/{id:guid}")]
        [HasPermission(PermissionConstants.PRODUCT_UPDATE)]
        public async Task<IActionResult> UpdateAttributeType(Guid id, [FromBody] UpdateAttributeTypeDto dto)
        {
            try
            {
                var attributeType = await _context.ProductAttributeTypes.FindAsync(id);
                if (attributeType == null || attributeType.IsActive == 0)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy loại thuộc tính."
                    });
                }

                if (!string.IsNullOrWhiteSpace(dto.TypeName))
                {
                    var exists = await _context.ProductAttributeTypes
                        .AnyAsync(t => t.TypeId != id && t.TypeName.ToLower() == dto.TypeName.Trim().ToLower() && t.IsActive == 1);

                    if (exists)
                    {
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = $"Tên loại thuộc tính '{dto.TypeName}' đã tồn tại."
                        });
                    }

                    attributeType.TypeName = dto.TypeName.Trim();
                }

                if (dto.TypeCode != null)
                {
                    attributeType.TypeCode = string.IsNullOrWhiteSpace(dto.TypeCode) ? null : dto.TypeCode.Trim().ToUpper();
                }

                attributeType.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                attributeType.UpdatedBy = GetUserInfo();

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Cập nhật loại thuộc tính thành công",
                    Data = new
                    {
                        attributeType.TypeId,
                        attributeType.TypeName,
                        attributeType.TypeCode
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi cập nhật loại thuộc tính",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// DELETE /api/products/attribute-types/{id} - Xóa loại thuộc tính
        /// </summary>
        [HttpDelete("attribute-types/{id:guid}")]
        [HasPermission(PermissionConstants.PRODUCT_DELETE)]
        public async Task<IActionResult> DeleteAttributeType(Guid id)
        {
            try
            {
                var attributeType = await _context.ProductAttributeTypes.FindAsync(id);
                if (attributeType == null || attributeType.IsActive == 0)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Không tìm thấy loại thuộc tính."
                    });
                }

                // Check if any product uses this attribute type
                var productsUsingType = await _context.ProductAttributes
                    .Where(a => a.AttributeName == attributeType.TypeName)
                    .CountAsync();

                if (productsUsingType > 0)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = $"Không thể xóa loại thuộc tính '{attributeType.TypeName}' vì có {productsUsingType} sản phẩm đang sử dụng."
                    });
                }

                attributeType.IsActive = 0;
                attributeType.UpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
                attributeType.UpdatedBy = GetUserInfo();

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Đã xóa loại thuộc tính '{attributeType.TypeName}'"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi khi xóa loại thuộc tính",
                    Errors = new() { ex.Message }
                });
            }
        }

        #endregion

    }
}