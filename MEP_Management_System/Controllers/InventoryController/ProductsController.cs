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
        /// 0. GET /api/products/lookup - Lightweight product list for dropdowns/selectboxes.
        /// Khong yeu cau quyen cu the, chi can da dang nhap.
        /// </summary>
        [HttpGet("lookup")]
        public async Task<IActionResult> GetProductsLookup(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 200,
            [FromQuery] string? searchTerm = null)
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Chưa được gán chi nhánh.",
                        Data = new { items = new List<object>() }
                    });

                pageSize = Math.Clamp(pageSize, 1, 500);

                var query = _context.BranchProducts
                    .Include(bp => bp.Product)
                    .Where(bp => bp.BranchId == branchId
                        && bp.IsActive == 1
                        && bp.Product.Status == 1)
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

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Danh sach san pham.",
                    Data = new { items }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Loi khi lay danh sach san pham.",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// 1. GET /api/products - List all products with pagination, filtering, and sorting
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] DTOs.InventoryDTOs.ProductParameters @params)
        {
            try
            {
                // Quyen xem: bat ky quyen PRODUCT nao
                if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_VIEW)
                    && !User.HasClaim("permission", PermissionConstants.PRODUCT_CREATE)
                    && !User.HasClaim("permission", PermissionConstants.PRODUCT_UPDATE)
                    && !User.HasClaim("permission", PermissionConstants.PRODUCT_DELETE)
                    && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                    return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền xem danh sách sản phẩm." });

                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return Ok(new ApiResponse<PaginatedResponse<ProductUpsertDto>>
                    {
                        Success = true,
                        Message = "Chưa được gán chi nhánh.",
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

                // Exclude soft-deleted products and enforce SaaS tenant isolation
                query = query.Where(bp => bp.IsActive != -1 && 
                                          (bp.Product.Status == null || bp.Product.Status != -1) &&
                                          (bp.Product.BranchId == null || bp.Product.BranchId == branchId));


                // Search by product name, code, barcode, category or brand
                if (!string.IsNullOrWhiteSpace(@params.SearchTerm))
                {
                    string search = @params.SearchTerm.Trim().ToLower();
                    query = query.Where(bp =>
                        bp.Product.ProductCode.ToLower().Contains(search) ||
                        bp.Product.ProductName.ToLower().Contains(search) ||
                        (bp.Product.Barcode != null && bp.Product.Barcode.ToLower().Contains(search)) ||
                        (bp.Product.CategoryName != null && bp.Product.CategoryName.ToLower().Contains(search)) ||
                        (bp.Product.BrandName != null && bp.Product.BrandName.ToLower().Contains(search))
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
        // Lookup API - cho phep moi authenticated user tim kiem SP cho dropdown
        public async Task<IActionResult> SearchProducts([FromQuery] string term, [FromQuery] int limit = 10)
        {
            try
            {
                // 🔐 Quyền đã kiểm tra bằng [HasPermission] ở attribute
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Chưa được gán chi nhánh.",
                        Data = new { items = new List<ProductSearchResultDto>() }
                    });
                }

                if (string.IsNullOrWhiteSpace(term))
                {
                    // Tra ve danh sach san pham gan day nhat khi khong co search term
                    var fallbackResults = await _context.BranchProducts
                        .Include(bp => bp.Product).ThenInclude(p => p.MediaResources)
                        .Where(bp => bp.BranchId == branchId
                            && bp.IsActive == 1
                            && bp.Product.Status == 1)
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
                            ImageUrl = bp.Product.MediaResources
                                .Where(m => m.IsPrimary == 1)
                                .Select(m => m.MediaUrl)
                                .FirstOrDefault()
                        })
                        .ToListAsync();

                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Danh sach san pham.",
                        Data = new { items = fallbackResults }
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
                        WarrantyQuantity = bp.WarrantyQuantity,
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
        // Lookup API - reference data cho forms
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
        public async Task<IActionResult> GetProductById(Guid id)
        {
            try
            {
                if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_VIEW)
                    && !User.HasClaim("permission", PermissionConstants.PRODUCT_CREATE)
                    && !User.HasClaim("permission", PermissionConstants.PRODUCT_UPDATE)
                    && !User.HasClaim("permission", PermissionConstants.PRODUCT_DELETE)
                    && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                    return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền xem chi tiết sản phẩm." });

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
                if (dto.CostPrice <= 0) errors.Add("costPrice phải lớn hơn 0");
                if (dto.SalePrice <= 0) errors.Add("salePrice phải lớn hơn 0");

                if (errors.Any())
                {
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Validation failed", Errors = errors });
                }

                // Check product code uniqueness WITHIN CURRENT BRANCH only
                bool isCodeExist = await _context.BranchProducts
                    .AnyAsync(bp => bp.BranchId == branchId.Value && 
                                    bp.IsActive != -1 && 
                                    bp.Product.Status != -1 &&
                                    bp.Product.ProductCode.ToLower() == dto.ProductCode.Trim().ToLower());
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
                    product.BranchId = branchId;
                    // Tự động active nếu có đơn vị được phép bán
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
                        // Tự động kích hoạt nếu có đơn vị được phép bán
                        IsActive = (short)(dto.DirectSale == true || (dto.ConversionUnits != null && dto.ConversionUnits.Any(u => u.DirectSale == true)) ? 1 : 0),
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

                // Get existing categories and brands (globally, because of UNIQUE constraint on name+type)
                var existingCategories = await _context.ProductTaxonomies
                    .Where(t => t.Type == "Category")
                    .ToListAsync();

                var existingBrands = await _context.ProductTaxonomies
                    .Where(t => t.Type == "Brand")
                    .ToListAsync();

                // Track taxonomy by name -> TaxonomyId (string to Guid)
                var categoryDict = existingCategories.ToDictionary(
                    c => c.Name.Trim().ToLower(),
                    c => c.TaxonomyId);
                var brandDict = existingBrands.ToDictionary(
                    b => b.Name.Trim().ToLower(),
                    b => b.TaxonomyId);

                // Track newly created taxonomies (entity + key) to save in next batch
                var newTaxonomies = new List<ProductTaxonomy>();
                var pendingCategoryKeys = new HashSet<string>();
                var pendingBrandKeys = new HashSet<string>();

                // OPTIMIZATION: Pre-load ALL existing product codes in ONE query (fixes N+1)
                // Note: product_code is globally unique, so we must check all products in the database.
                var existingCodes = await _context.Products
                    .Select(p => p.ProductCode.ToLower())
                    .ToListAsync();
                var existingCodeSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

                // Track codes being imported in this batch to prevent duplicates within same import
                var importedCodesInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                const int batchSize = 500; // Save every 500 products to avoid EF tracker overload
                var productsToInsert = new List<Product>();
                var branchProductsToInsert = new List<BranchProduct>();

                foreach (var pDto in dto.Products)
                {
                    var rowErrors = new List<string>();
                    if (string.IsNullOrWhiteSpace(pDto.ProductCode)) rowErrors.Add("Mã sản phẩm trống");
                    else if (pDto.ProductCode.Length > 50) rowErrors.Add("Mã sản phẩm quá 50 ký tự");
                    
                    if (string.IsNullOrWhiteSpace(pDto.ProductName)) rowErrors.Add("Tên sản phẩm trống");
                    else if (pDto.ProductName.Length > 255) rowErrors.Add("Tên sản phẩm quá 255 ký tự");
                    
                    if (string.IsNullOrWhiteSpace(pDto.Unit)) rowErrors.Add("Đơn vị trống");
                    else if (pDto.Unit.Length > 20) rowErrors.Add($"Đơn vị '{pDto.Unit}' quá 20 ký tự");
                    
                    if (pDto.CategoryName != null && pDto.CategoryName.Length > 255) rowErrors.Add("Tên danh mục quá 255 ký tự");
                    if (pDto.BrandName != null && pDto.BrandName.Length > 255) rowErrors.Add("Tên thương hiệu quá 255 ký tự");
                    
                    if (pDto.CostPrice < 0) rowErrors.Add("Giá vốn không được âm");
                    if (pDto.SalePrice < 0) rowErrors.Add("Giá bán không được âm");

                    if (rowErrors.Any())
                    {
                        errors.Add($"Sản phẩm '{pDto.ProductName ?? "Không tên"}': {string.Join(", ", rowErrors)}");
                        continue;
                    }

                    // OPTIMIZED: O(1) lookup instead of DB query
                    var codeKey = pDto.ProductCode.Trim().ToLower();
                    if (existingCodeSet.Contains(codeKey) || importedCodesInBatch.Contains(codeKey))
                    {
                        errors.Add($"Sản phẩm '{pDto.ProductName}': Mã '{pDto.ProductCode}' đã tồn tại");
                        continue;
                    }

                    // Auto-create category if missing
                    if (dto.AutoCreateMissingCategories && !string.IsNullOrWhiteSpace(pDto.CategoryName))
                    {
                        var catName = pDto.CategoryName.Trim();
                        var catKey = catName.ToLower();
                        if (categoryDict.TryGetValue(catKey, out var existingCatId))
                        {
                            pDto.CategoryTaxonomyId = existingCatId;
                        }
                        else if (pendingCategoryKeys.Contains(catKey))
                        {
                            // Find the pending taxonomy ID
                            var pendingCat = newTaxonomies.FirstOrDefault(t => t.Name.ToLower() == catKey && t.Type == "Category");
                            pDto.CategoryTaxonomyId = pendingCat?.TaxonomyId ?? Guid.Empty;
                        }
                        else
                        {
                            var newCat = new ProductTaxonomy
                            {
                                TaxonomyId = Guid.NewGuid(),
                                BranchId = branchId.Value,
                                Name = catName,
                                Type = "Category",
                                CreatedAt = currentLocalTime
                            };
                            newTaxonomies.Add(newCat);
                            pendingCategoryKeys.Add(catKey);
                            pDto.CategoryTaxonomyId = newCat.TaxonomyId;
                        }
                    }

                    // Auto-create brand if missing
                    if (dto.AutoCreateMissingBrands && !string.IsNullOrWhiteSpace(pDto.BrandName))
                    {
                        var brandName = pDto.BrandName.Trim();
                        var brandKey = brandName.ToLower();
                        if (brandDict.TryGetValue(brandKey, out var existingBrandId))
                        {
                            pDto.BrandTaxonomyId = existingBrandId;
                        }
                        else if (pendingBrandKeys.Contains(brandKey))
                        {
                            var pendingBrand = newTaxonomies.FirstOrDefault(t => t.Name.ToLower() == brandKey && t.Type == "Brand");
                            pDto.BrandTaxonomyId = pendingBrand?.TaxonomyId ?? Guid.Empty;
                        }
                        else
                        {
                            var newBrand = new ProductTaxonomy
                            {
                                TaxonomyId = Guid.NewGuid(),
                                BranchId = branchId.Value,
                                Name = brandName,
                                Type = "Brand",
                                CreatedAt = currentLocalTime
                            };
                            newTaxonomies.Add(newBrand);
                            pendingBrandKeys.Add(brandKey);
                            pDto.BrandTaxonomyId = newBrand.TaxonomyId;
                        }
                    }

                    var product = _mapper.Map<Product>(pDto);
                    product.ProductId = Guid.NewGuid();
                    product.CreatedAt = currentLocalTime;
                    product.CreatedBy = currentUserId;
                    product.BranchId = branchId;
                    product.ProductConversionUnits = new List<ProductConversionUnit>();
                    product.BranchProducts = new List<BranchProduct>();

                    // Track this code as imported
                    importedCodesInBatch.Add(codeKey);

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
                        ActualStock = pDto.ActualStock > 0 ? pDto.ActualStock : 0,
                        AvailableStock = pDto.ActualStock > 0 ? pDto.ActualStock : 0,
                        ReservedStock = 0,
                        ShelfLocation = !string.IsNullOrWhiteSpace(pDto.ShelfLocation) ? pDto.ShelfLocation : "Chưa xếp vị trí",
                        MinimumStock = pDto.MinimumStock ?? 0,
                        MaximumStock = pDto.MaximumStock ?? 0,
                        CostPrice = pDto.CostPrice,
                        SellPrice = pDto.SalePrice,
                        DirectSale = (short?)(pDto.DirectSale ? 1 : 0),
                        IsActive = 1,
                        Product = null!
                    };

                    productsToInsert.Add(product);
                    branchProductsToInsert.Add(branchProduct);
                    // OPTIMIZATION: Batch save every 500 products to prevent EF tracker overload
                    if (productsToInsert.Count >= batchSize)
                    {
                        // Save taxonomies first, then products
                        if (newTaxonomies.Any())
                        {
                            _context.ProductTaxonomies.AddRange(newTaxonomies);
                            await _context.SaveChangesAsync();
                            // Add saved taxonomies to dicts so next batch uses them
                            foreach (var t in newTaxonomies)
                            {
                                var key = t.Name.Trim().ToLower();
                                if (t.Type == "Category")
                                    categoryDict[key] = t.TaxonomyId;
                                else
                                    brandDict[key] = t.TaxonomyId;
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
                        _context.ChangeTracker.Clear(); // Clear tracker to free memory
                    }
                }

                // Save remaining taxonomies and products
                if (newTaxonomies.Any())
                {
                    _context.ProductTaxonomies.AddRange(newTaxonomies);
                    await _context.SaveChangesAsync();
                }
                if (productsToInsert.Any())
                {
                    _context.Products.AddRange(productsToInsert);
                    _context.BranchProducts.AddRange(branchProductsToInsert);
                    await _context.SaveChangesAsync();
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Nhập thành công {successCount} sản phẩm" + (errors.Any() ? $", bỏ qua {errors.Count} sản phẩm lỗi." : "."),
                    Data = new { successCount, errors }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi khi import: " + ex.Message, Errors = new() { ex.InnerException?.Message ?? "" } });
            }
        }

        /// <summary>
        /// 4. PUT /api/products/{id} - Update product
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductDto dto)
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

                // Quyen sua: PRODUCT_UPDATE hoac nguoi tao san pham (trong phien hien tai)
                var uid = GetCurrentUserId();
                bool isCreator = bp.Product.CreatedBy.HasValue && bp.Product.CreatedBy.Value == uid;
                // Chi cho phep nguoi tao sua neu san pham duoc tao trong phien dang nhap nay
                if (isCreator)
                {
                    var iatClaim = User.FindFirst("iat")?.Value;
                    if (iatClaim != null && long.TryParse(iatClaim, out var iat))
                    {
                        var sessionStart = DateTimeOffset.FromUnixTimeSeconds(iat).UtcDateTime;
                        if (bp.Product.CreatedAt.HasValue && bp.Product.CreatedAt.Value < sessionStart)
                            isCreator = false; // San pham tao truoc phien nay -> ko cho sua
                    }
                }
                if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_UPDATE) && !isCreator)
                    return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền chỉnh sửa sản phẩm này." });

                var errors = new List<string>();
                if (string.IsNullOrWhiteSpace(dto.ProductCode)) errors.Add("productCode is required");
                if (string.IsNullOrWhiteSpace(dto.ProductName)) errors.Add("productName is required");
                if (string.IsNullOrWhiteSpace(dto.Unit)) errors.Add("unit is required");
                if (dto.CostPrice <= 0) errors.Add("costPrice phải lớn hơn 0");
                if (dto.SalePrice <= 0) errors.Add("salePrice phải lớn hơn 0");

                if (errors.Any())
                {
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Validation failed", Errors = errors });
                }

                bool isCodeExist = await _context.BranchProducts
                    .AnyAsync(otherBp => otherBp.BranchId == branchId.Value && 
                                         otherBp.ProductId != id &&
                                         otherBp.IsActive != -1 &&
                                         otherBp.Product.Status != -1 &&
                                         otherBp.Product.ProductCode.ToLower() == dto.ProductCode.Trim().ToLower());
                if (isCodeExist)
                {
                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Product code already exists in this branch", Errors = new() { $"Code '{dto.ProductCode}' exists" } });
                }

                var currentLocalTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                Guid? currentUserId = GetUserInfo();

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var originalProductId = bp.Product.ProductId;
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
                    bp.Product.UpdatedAt = currentLocalTime;
                    bp.Product.UpdatedBy = currentUserId;

                    _context.Products.Update(bp.Product);

                    bp.ActualStock = bp.ActualStock;
                    bp.AvailableStock = bp.AvailableStock;
                    bp.ReservedStock = bp.ReservedStock;

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

                    // Tự động đồng bộ trạng thái Hoạt động với trạng thái bán của các đơn vị
                    var hasAnySellableUnit = dto.DirectSale == true
                        || (dto.ConversionUnits != null && dto.ConversionUnits.Any(u => u.DirectSale == true));
                    if (hasAnySellableUnit)
                    {
                        // Có ít nhất 1 đơn vị được bán → BẬT Hoạt động
                        if (bp.IsActive != 1)
                        {
                            bp.IsActive = 1;
                            if (bp.Product != null) bp.Product.Status = 1;
                        }
                    }
                    else
                    {
                        // TẤT CẢ các đơn vị đều không được bán → TẮT Hoạt động
                        if (bp.IsActive != 0)
                        {
                            bp.IsActive = 0;
                            if (bp.Product != null) bp.Product.Status = 0;
                        }
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

                // Quyen xoa: PRODUCT_DELETE hoac nguoi tao san pham (trong phien hien tai)
                var uid = GetCurrentUserId();
                bool isCreator = bp.Product.CreatedBy.HasValue && bp.Product.CreatedBy.Value == uid;
                if (isCreator)
                {
                    var iatClaim = User.FindFirst("iat")?.Value;
                    if (iatClaim != null && long.TryParse(iatClaim, out var iat))
                    {
                        var sessionStart = DateTimeOffset.FromUnixTimeSeconds(iat).UtcDateTime;
                        if (bp.Product.CreatedAt.HasValue && bp.Product.CreatedAt.Value < sessionStart)
                            isCreator = false;
                    }
                }
                if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_DELETE) && !isCreator)
                    return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền xóa sản phẩm này." });

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
                        {
                            _context.Products.Remove(bp.Product);
                        }
                    }
                }

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
                if (isActive)
                {
                    // Khi bật Hoạt động, nếu chưa có đơn vị nào được bán thì tự động bật DirectSale
                    if (bp.DirectSale != 1)
                    {
                        var hasAnySellableUnit = await _context.ProductConversionUnits
                            .AnyAsync(u => u.ProductId == bp.ProductId && u.DirectSale == 1);
                        if (!hasAnySellableUnit)
                        {
                            bp.DirectSale = 1;
                        }
                    }
                }
                else
                {
                    // Khi tắt Hoạt động, tắt tất cả đơn vị bán
                    bp.DirectSale = 0;
                    var conversionUnits = await _context.ProductConversionUnits
                        .Where(u => u.ProductId == bp.ProductId && u.DirectSale == 1)
                        .ToListAsync();
                    foreach (var cu in conversionUnits)
                    {
                        cu.DirectSale = 0;
                    }
                }

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
                    if (request.IsActive)
                    {
                        if (bp.DirectSale != 1)
                        {
                            var hasAnySellableUnit = await _context.ProductConversionUnits
                                .AnyAsync(u => u.ProductId == bp.ProductId && u.DirectSale == 1);
                            if (!hasAnySellableUnit)
                            {
                                bp.DirectSale = 1;
                            }
                        }
                    }
                    else
                    {
                        bp.DirectSale = 0;
                        var conversionUnits = await _context.ProductConversionUnits
                            .Where(u => u.ProductId == bp.ProductId && u.DirectSale == 1)
                            .ToListAsync();
                        foreach (var cu in conversionUnits)
                        {
                            cu.DirectSale = 0;
                        }
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
                WarrantyQuantity = bp.WarrantyQuantity,
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
                DirectSale = bp.DirectSale == 1,
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
        // Lookup API - reference data cho forms
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return Ok(new ApiResponse<List<TaxonomyItemDto>>
                    {
                        Success = true,
                        Message = "Chưa được gán chi nhánh.",
                        Data = new List<TaxonomyItemDto>()
                    });
                }

                var productGroupings = await _context.BranchProducts
                    .Where(bp => bp.BranchId == branchId && bp.Product.CategoryName != null && bp.Product.CategoryName != "")
                    .Where(bp => bp.IsActive != -1 && (bp.Product.Status == null || bp.Product.Status != -1) && (bp.Product.BranchId == null || bp.Product.BranchId == branchId))
                    .Select(bp => new { bp.ProductId, bp.Product.CategoryName })
                    .Distinct()
                    .GroupBy(x => x.CategoryName)
                    .Select(g => new { Name = g.Key!, Count = g.Count() })
                    .ToListAsync();

                var taxonomies = await _context.ProductTaxonomies
                    .Where(t => t.BranchId == branchId && t.Type == "Category")
                    .Select(t => t.Name)
                    .ToListAsync();

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
                {
                    resultList.Add(new TaxonomyItemDto { Name = kv.Key, ProductCount = kv.Value });
                }

                var result = resultList.OrderBy(x => x.Name).ToList();

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
        // Lookup API - reference data cho forms
        public async Task<IActionResult> GetBrands()
        {
            try
            {
                var branchId = GetCurrentBranchId();
                if (branchId == null)
                {
                    return Ok(new ApiResponse<List<TaxonomyItemDto>>
                    {
                        Success = true,
                        Message = "Chưa được gán chi nhánh.",
                        Data = new List<TaxonomyItemDto>()
                    });
                }

                var productGroupings = await _context.BranchProducts
                    .Where(bp => bp.BranchId == branchId && bp.Product.BrandName != null && bp.Product.BrandName != "")
                    .Where(bp => bp.IsActive != -1 && (bp.Product.Status == null || bp.Product.Status != -1) && (bp.Product.BranchId == null || bp.Product.BranchId == branchId))
                    .Select(bp => new { bp.ProductId, bp.Product.BrandName })
                    .Distinct()
                    .GroupBy(x => x.BrandName)
                    .Select(g => new { Name = g.Key!, Count = g.Count() })
                    .ToListAsync();

                var taxonomies = await _context.ProductTaxonomies
                    .Where(t => t.BranchId == branchId && t.Type == "Brand")
                    .Select(t => t.Name)
                    .ToListAsync();

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
                {
                    resultList.Add(new TaxonomyItemDto { Name = kv.Key, ProductCount = kv.Value });
                }

                var result = resultList.OrderBy(x => x.Name).ToList();

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
        [HasPermission(PermissionConstants.PRODUCT_CATEGORY_MANAGE)]
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
                    TaxonomyId = Guid.NewGuid(),
                    BranchId = branchId,
                    Name = trimmedName,
                    Type = "Category",
                    CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
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
                    Errors = new() { ex.Message, ex.InnerException?.Message ?? "No inner exception" }
                });
            }
        }

        /// <summary>
        /// POST /api/products/brands - Thêm mới thương hiệu (brand)
        /// </summary>
        [HttpPost("brands")]
        [HasPermission(PermissionConstants.PRODUCT_CATEGORY_MANAGE)]
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
                    TaxonomyId = Guid.NewGuid(),
                    BranchId = branchId,
                    Name = trimmedName,
                    Type = "Brand",
                    CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
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
                    Errors = new() { ex.Message, ex.InnerException?.Message ?? "No inner exception" }
                });
            }
        }

        /// <summary>
        /// PUT /api/products/categories/rename - Đổi tên nhóm hàng (bulk update mọi SP đang dùng tên cũ)
        /// </summary>
        [HttpPut("categories/rename")]
        [HasPermission(PermissionConstants.PRODUCT_CATEGORY_MANAGE)]
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

                var affectedProductIds = await _context.BranchProducts
                    .Where(bp => bp.BranchId == branchId && bp.Product.CategoryName != null && bp.Product.CategoryName.ToLower() == oldName.ToLower())
                    .Where(bp => bp.IsActive != -1 && (bp.Product.Status == null || bp.Product.Status != -1) && (bp.Product.BranchId == null || bp.Product.BranchId == branchId))
                    .Select(bp => bp.ProductId)
                    .Distinct()
                    .ToListAsync();

                var affectedProducts = await _context.Products
                    .Where(p => affectedProductIds.Contains(p.ProductId))
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

                // Cập nhật tên nhóm hàng trong cấu hình đổi/trả (SystemConfigs)
                var config = await _context.SystemConfigs
                    .FirstOrDefaultAsync(c => c.BranchId == branchId && c.ConfigKey == "CATEGORY_RETURN_POLICIES");

                string debugBefore = config?.ConfigValue ?? "null";
                string debugAfter = "not updated";

                if (config != null && !string.IsNullOrWhiteSpace(config.ConfigValue))
                {
                    try
                    {
                        var caseInsOptions = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var camelOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
                        
                        var policies = System.Text.Json.JsonSerializer.Deserialize<List<CategoryPolicyDto>>(config.ConfigValue, caseInsOptions);
                        if (policies != null)
                        {
                            bool policyUpdated = false;
                            foreach (var policy in policies)
                            {
                                bool matchById = tax != null && policy.CategoryId == tax.TaxonomyId;
                                bool matchByName = policy.CategoryName != null && policy.CategoryName.Equals(oldName, StringComparison.OrdinalIgnoreCase);

                                if (matchById || matchByName)
                                {
                                    policy.CategoryName = newName;
                                    // Bổ sung CategoryId nếu trước đó bị thiếu
                                    if (tax != null && policy.CategoryId == null)
                                    {
                                        policy.CategoryId = tax.TaxonomyId;
                                    }
                                    policyUpdated = true;
                                }
                            }
                            if (policyUpdated)
                            {
                                config.ConfigValue = System.Text.Json.JsonSerializer.Serialize(policies, camelOptions);
                                debugAfter = config.ConfigValue;
                                _context.SystemConfigs.Update(config);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        debugAfter = "error: " + ex.Message;
                    }
                }

                await _context.SaveChangesAsync();

                // Trả về thông tin debug để theo dõi xem cấu hình có được cập nhật thành công hay không
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Đã đổi tên nhóm hàng '{oldName}' thành '{newName}' cho {affectedProducts.Count} sản phẩm",
                    Data = new 
                    { 
                        UpdatedCount = affectedProducts.Count,
                        DebugConfigBefore = debugBefore,
                        DebugConfigAfter = debugAfter 
                    }
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
        [HasPermission(PermissionConstants.PRODUCT_CATEGORY_MANAGE)]
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

                var affectedProductIds = await _context.BranchProducts
                    .Where(bp => bp.BranchId == branchId && bp.Product.BrandName != null && bp.Product.BrandName.ToLower() == oldName.ToLower())
                    .Where(bp => bp.IsActive != -1 && (bp.Product.Status == null || bp.Product.Status != -1) && (bp.Product.BranchId == null || bp.Product.BranchId == branchId))
                    .Select(bp => bp.ProductId)
                    .Distinct()
                    .ToListAsync();

                var affectedProducts = await _context.Products
                    .Where(p => affectedProductIds.Contains(p.ProductId))
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
        [HasPermission(PermissionConstants.PRODUCT_CATEGORY_MANAGE)]
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

                var affectedProductIds = await _context.BranchProducts
                    .Where(bp => bp.BranchId == branchId && bp.Product.CategoryName != null && bp.Product.CategoryName.ToLower() == trimmed.ToLower())
                    .Where(bp => bp.IsActive != -1 && (bp.Product.Status == null || bp.Product.Status != -1) && (bp.Product.BranchId == null || bp.Product.BranchId == branchId))
                    .Select(bp => bp.ProductId)
                    .Distinct()
                    .ToListAsync();

                var affectedProducts = await _context.Products
                    .Where(p => affectedProductIds.Contains(p.ProductId))
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

                // Don sach chinh sach doi tra theo nhom hang (CATEGORY_RETURN_POLICIES)
                // de tranh ton dong policy cua category da bi xoa
                var policyConfigs = await _context.SystemConfigs
                    .Where(c => c.ConfigKey == "CATEGORY_RETURN_POLICIES")
                    .ToListAsync();

                foreach (var config in policyConfigs)
                {
                    if (string.IsNullOrWhiteSpace(config.ConfigValue)) continue;
                    try
                    {
                        var policies = System.Text.Json.JsonSerializer.Deserialize<List<CategoryPolicyDto>>(config.ConfigValue);
                        if (policies == null || policies.Count == 0) continue;

                        var cleaned = policies
                            .Where(p => !string.Equals(p.CategoryName, trimmed, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (cleaned.Count != policies.Count)
                        {
                            config.ConfigValue = System.Text.Json.JsonSerializer.Serialize(cleaned);
                            _context.SystemConfigs.Update(config);
                        }
                    }
                    catch { }
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
        [HasPermission(PermissionConstants.PRODUCT_CATEGORY_MANAGE)]
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

                var affectedProductIds = await _context.BranchProducts
                    .Where(bp => bp.BranchId == branchId && bp.Product.BrandName != null && bp.Product.BrandName.ToLower() == trimmed.ToLower())
                    .Where(bp => bp.IsActive != -1 && (bp.Product.Status == null || bp.Product.Status != -1) && (bp.Product.BranchId == null || bp.Product.BranchId == branchId))
                    .Select(bp => bp.ProductId)
                    .Distinct()
                    .ToListAsync();

                var affectedProducts = await _context.Products
                    .Where(p => affectedProductIds.Contains(p.ProductId))
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
        // Lookup API - reference data cho forms
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
        public async Task<IActionResult> CreateLocation([FromBody] CreateTaxonomyDto dto)
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_CREATE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền thêm vị trí kho." });
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
        public async Task<IActionResult> RenameLocation([FromBody] RenameTaxonomyDto dto)
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_UPDATE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền quản lý vị trí kho." });

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
        public async Task<IActionResult> DeleteLocation(string locationName)
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_DELETE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền xóa vị trí kho." });
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
        // Lookup API - reference data cho forms
        public async Task<IActionResult> GetAttributeTypes()
        {
            try
            {
                var branchId = GetCurrentBranchId();

                var result = await _context.ProductAttributeTypes
                    .Where(t => t.IsActive == 1 && t.BranchId == branchId)
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
        public async Task<IActionResult> CreateAttributeType([FromBody] CreateAttributeTypeDto dto)
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_CREATE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền thêm thuộc tính." });
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

                var branchId = GetCurrentBranchId();

                var exists = await _context.ProductAttributeTypes
                    .AnyAsync(t => t.TypeName.ToLower() == dto.TypeName.Trim().ToLower() && t.IsActive == 1 && t.BranchId == branchId);

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

                var typeCode = !string.IsNullOrWhiteSpace(dto.TypeCode) 
                    ? dto.TypeCode.Trim().ToUpper() 
                    : "AT-" + Guid.NewGuid().ToString().Substring(0, 4).ToUpper();

                var attributeType = new ProductAttributeType
                {
                    TypeId = Guid.NewGuid(),
                    BranchId = branchId,
                    TypeName = dto.TypeName.Trim(),
                    TypeCode = typeCode,
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
        public async Task<IActionResult> UpdateAttributeType(Guid id, [FromBody] UpdateAttributeTypeDto dto)
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_UPDATE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền quản lý thuộc tính." });
            try
            {
                var branchId = GetCurrentBranchId();

                var attributeType = await _context.ProductAttributeTypes.FindAsync(id);
                if (attributeType == null || attributeType.IsActive == 0 || (attributeType.BranchId != null && attributeType.BranchId != branchId))
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
                        .AnyAsync(t => t.TypeId != id && t.TypeName.ToLower() == dto.TypeName.Trim().ToLower() && t.IsActive == 1 && (t.BranchId == branchId || t.BranchId == null));

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
        public async Task<IActionResult> DeleteAttributeType(Guid id)
        {
            if (!User.IsInRole("Owner") && !User.HasClaim("permission", PermissionConstants.PRODUCT_DELETE)
                && !User.HasClaim("permission", PermissionConstants.PRODUCT_CATEGORY_MANAGE))
                return StatusCode(403, new ApiResponse<object> { Success = false, Message = "Bạn không có quyền xóa thuộc tính." });
            try
            {
                var branchId = GetCurrentBranchId();

                var attributeType = await _context.ProductAttributeTypes.FindAsync(id);
                if (attributeType == null || attributeType.IsActive == 0 || (attributeType.BranchId != null && attributeType.BranchId != branchId))
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