using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using MEP_Management_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MEP_Management_System.Controllers.InventoryController
{
    [Authorize]
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

        /// <summary>
        /// 1. GET /api/products - List all products with pagination, filtering, and sorting
        /// For Owner role: provide branchId via query param (?branchId=guid) or X-Branch-Id header
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] DTOs.InventoryDTOs.ProductParameters @params, [FromQuery] Guid? branchId = null)
        {
            try
            {
                var resolvedBranchId = branchId ?? _currentBranch.GetBranchId();
                if (resolvedBranchId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "JWT token is missing or invalid",
                        Errors = new() { "Authorization header required" }
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
                    .Where(bp => bp.BranchId == resolvedBranchId.Value).AsQueryable();

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
                    query = query.Where(bp => bp.Product.Status == statusValue);
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
        /// 2. GET /api/products/{id} - Get product details by ID
        /// For Owner role: provide branchId via query param (?branchId=guid) or X-Branch-Id header
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetProductById(Guid id, [FromQuery] Guid? branchId = null)
        {
            try
            {
                var resolvedBranchId = branchId ?? _currentBranch.GetBranchId();
                if (resolvedBranchId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "JWT token is missing or invalid",
                        Errors = new() { "Authorization header required" }
                    });
                }

                var bp = await _context.BranchProducts
                    .Include(bp => bp.Product).ThenInclude(p => p.Supplier)
                    .Include(bp => bp.Product).ThenInclude(p => p.MediaResources)
                    .Include(bp => bp.Product).ThenInclude(p => p.ProductAttributes)
                    .Include(bp => bp.Product).ThenInclude(p => p.ProductConversionUnits)
                    .FirstOrDefaultAsync(bp => bp.ProductId == id && bp.BranchId == resolvedBranchId.Value);

                if (bp == null)
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
                    Data = MapToProductDto(bp)
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
        [Authorize(Roles = "Owner")]
[HttpPost]
public async Task<IActionResult> CreateProduct([FromBody] ProductUpsertDto dto, [FromQuery] Guid? branchId = null)
{
    try
    {
        var resolvedBranchId = branchId ?? _currentBranch.GetBranchId();
        if (resolvedBranchId == null)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không xác định được chi nhánh. Vui lòng truyền param '?branchId=...' khi tạo sản phẩm.",
                Errors = new() { "BranchId is required" }
            });
        }

        // --- Validate data giữ nguyên ---
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.ProductCode)) errors.Add("productCode is required");
        if (string.IsNullOrWhiteSpace(dto.ProductName)) errors.Add("productName is required");
        if (string.IsNullOrWhiteSpace(dto.Unit)) errors.Add("unit is required");
        if (dto.CostPrice <= 0) errors.Add("costPrice must be greater than 0");
        if (dto.SalePrice <= 0) errors.Add("salePrice must be greater than 0");
        if (dto.SalePrice < dto.CostPrice) errors.Add("salePrice should be >= costPrice");

        if (errors.Any())
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Validation failed", Errors = errors });
        }

        bool isCodeExist = await _context.Products.AnyAsync(p => p.ProductCode.ToLower() == dto.ProductCode.Trim().ToLower());
        if (isCodeExist)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Product code already exists", Errors = new() { $"Code '{dto.ProductCode}' exists" } });
        }

        var currentLocalTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        
        // 🎯 LẤY USER ID TỪ JWT TOKEN
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? currentUserId = string.IsNullOrEmpty(userIdStr) ? null : Guid.Parse(userIdStr);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var product = _mapper.Map<Product>(dto);
            product.ProductId = Guid.NewGuid();
            product.CreatedAt = currentLocalTime;
            
            // 🎯 GÁN NGƯỜI TẠO Ở ĐÂY
            product.CreatedBy = currentUserId; 
            
            product.ProductConversionUnits = new List<ProductConversionUnit>();
            product.BranchProducts = new List<BranchProduct>();

            _context.Products.Add(product);

            // --- Giữ nguyên logic thêm Images, Attributes, ConversionUnits cũ của bạn ---
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

            var branchProduct = new BranchProduct
            {
                BranchProductId = Guid.NewGuid(),
                BranchId = resolvedBranchId.Value,
                ProductId = product.ProductId,
                ActualStock = dto.ActualStock,
                AvailableStock = dto.AvailableStock,
                ReservedStock = dto.ReservedStock,
                ShelfLocation = dto.ShelfLocation ?? "Chưa xếp vị trí",
                CostPrice = dto.CostPrice,
                SellPrice = dto.SalePrice,
                Product = null!
            };
            _context.BranchProducts.Add(branchProduct);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(nameof(GetProductById), new { id = product.ProductId, branchId = resolvedBranchId.Value },
                new ApiResponse<ProductUpsertDto> { Success = true, Message = "Product created successfully", Data = MapToProductDto(branchProduct) });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw ex;
        }
    }
    catch (Exception ex)
    {
        return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Error creating product", Errors = new() { ex.Message } });
    }
}

        /// <summary>
        /// 4. PUT /api/products/{id} - Update product
        /// </summary>
        [Authorize(Roles = "Owner")]
[HttpPut("{id:guid}")]
public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] ProductUpsertDto dto, [FromQuery] Guid? branchId = null)
{
    try
    {
        // 🎯 ĐÃ SỬA: Linh hoạt lấy chi nhánh để Owner sửa đúng kho
        var resolvedBranchId = branchId ?? _currentBranch.GetBranchId();
        if (resolvedBranchId == null)
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Không xác định được chi nhánh. Vui lòng truyền param '?branchId=...' khi cập nhật sản phẩm.",
                Errors = new() { "BranchId is required" }
            });
        }

        var bp = await _context.BranchProducts
            .Include(b => b.Product).ThenInclude(p => p.MediaResources)
            .Include(b => b.Product).ThenInclude(p => p.ProductAttributes)
            .Include(b => b.Product).ThenInclude(p => p.ProductConversionUnits)
            .FirstOrDefaultAsync(b => b.ProductId == id && b.BranchId == resolvedBranchId.Value);

        if (bp == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "Product not found in this branch", Errors = new() { $"No product with ID: {id}" } });
        }

        // --- Validate data giữ nguyên ---
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.ProductName)) errors.Add("productName is required");
        if (dto.CostPrice < 0) errors.Add("costPrice cannot be negative");
        if (dto.SalePrice < 0) errors.Add("salePrice cannot be negative");
        if (dto.SalePrice > 0 && dto.CostPrice > 0 && dto.SalePrice < dto.CostPrice) errors.Add("salePrice should be >= costPrice");

        if (errors.Any())
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Validation failed", Errors = errors });
        }

        var currentLocalTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        
        // 🎯 LẤY USER ID TỪ JWT TOKEN
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? currentUserId = string.IsNullOrEmpty(userIdStr) ? null : Guid.Parse(userIdStr);

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _mapper.Map(dto, bp.Product);
            
            // 🎯 CẬP NHẬT THÔNG TIN NGƯỜI SỬA VÀ THỜI GIAN SỬA
            bp.Product.UpdatedAt = currentLocalTime;
            bp.Product.UpdatedBy = currentUserId;
            
            _context.Products.Update(bp.Product);

            bp.ActualStock = dto.ActualStock;
            bp.AvailableStock = dto.AvailableStock;
            bp.ReservedStock = dto.ReservedStock;
            bp.ShelfLocation = dto.ShelfLocation ?? "Chưa xếp vị trí";
            bp.CostPrice = dto.CostPrice;
            bp.SellPrice = dto.SalePrice;
            _context.BranchProducts.Update(bp);

            // --- Giữ nguyên logic xóa/thêm mới các bảng liên quan cũ của bạn ---
            _context.MediaResources.RemoveRange(bp.Product.MediaResources);
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

            _context.ProductAttributes.RemoveRange(bp.Product.ProductAttributes);
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

            _context.ProductConversionUnits.RemoveRange(bp.Product.ProductConversionUnits);
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
                        DirectSale = (short?)(unit.DirectSale ? 1 : 0)
                    });
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new ApiResponse<ProductUpsertDto> { Success = true, Message = "Product updated successfully", Data = MapToProductDto(bp) });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw ex;
        }
    }
    catch (Exception ex)
    {
        return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Error updating product", Errors = new() { ex.Message } });
    }
}
        /// <summary>
        /// 5. DELETE /api/products/{id} - Delete product (soft delete)
        /// </summary>
        [Authorize(Roles = "Owner")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            try
            {
                var branchId = _currentBranch.GetBranchId();
                if (branchId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "JWT token is missing or invalid",
                        Errors = new() { "Authorization header required" }
                    });
                }

                var bp = await _context.BranchProducts
                    .Include(b => b.Product).FirstOrDefaultAsync(b => b.ProductId == id && b.BranchId == branchId.Value);

                if (bp == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Product not found",
                        Errors = new() { $"No product with ID: {id}" }
                    });
                }

                bp.IsActive = 0;
                if (bp.Product != null)
                {
                    bp.Product.Status = 0;
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
        [Authorize(Roles = "Owner")]
        [HttpPatch("{id:guid}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(Guid id, [FromQuery] bool isActive)
        {
            try
            {
                var branchId = _currentBranch.GetBranchId();
                if (branchId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "JWT token is missing or invalid",
                        Errors = new() { "Authorization header required" }
                    });
                }

                var bp = await _context.BranchProducts
 .Include(b => b.Product) // ← THÊM DÒNG NÀY
  .FirstOrDefaultAsync(b => b.ProductId == id && b.BranchId == branchId.Value);

                if (bp == null)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Product not found",
                        Errors = new() { $"No product with ID: {id}" }
                    });
                }

                bp.IsActive = (short?)(isActive ? 1 : 0);
                if (bp.Product != null)
                {
                    bp.Product.Status = (short?)(isActive ? 1 : 0);
                    _context.Products.Update(bp.Product);
                }
                _context.BranchProducts.Update(bp);
                await _context.SaveChangesAsync();
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Product status updated to: {(isActive ? "Active" : "Inactive")}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error updating product status",
                    Errors = new() { ex.Message }
                });
            }
        }
        /// <summary>
        /// 7. PATCH /api/products/toggle-status-bulk - Toggle status for many products at once
        /// </summary>
        [Authorize(Roles = "Owner")]
        [HttpPatch("toggle-status-bulk")]
        public async Task<IActionResult> ToggleStatusBulk([FromBody] BulkToggleStatusRequest request)
        {
            try
            {
                var branchId = _currentBranch.GetBranchId();
                if (branchId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "JWT token is missing or invalid",
                        Errors = new() { "Authorization header required" }
                    });
                }

                if (request?.Ids == null || !request.Ids.Any())
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = new() { "ids must not be empty" }
                    });
                }

                // Giới hạn 500 id / lần để tránh quá tải request
                if (request.Ids.Count > 500)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = new() { "Maximum 500 ids per request" }
                    });
                }

                // Lấy ra các BranchProduct thuộc branch hiện tại
                var bps = await _context.BranchProducts
                .Include(b => b.Product)
                .Where(b => b.BranchId == branchId.Value && request.Ids.Contains(b.ProductId))
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
                    bp.IsActive = (short?)(request.IsActive ? 1 : 0);
                    if (bp.Product != null)
                    {
                        bp.Product.Status = (short?)(request.IsActive ? 1 : 0);
                        _context.Products.Update(bp.Product);
                    }
                    _context.BranchProducts.Update(bp);
                    updatedIds.Add(bp.ProductId);
                }

                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<BulkToggleStatusResponse>
                {
                    Success = true,
                    Message = $"Updated {updatedIds.Count} product(s) successfully",
                    Data = new BulkToggleStatusResponse
                    {
                        UpdatedCount = updatedIds.Count,
                        UpdatedIds = updatedIds,
                        NotFoundIds = notFoundIds,
                        NewStatus = request.IsActive ? "Active" : "Inactive"
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error updating product statuses",
                    Errors = new() { ex.Message }
                });
            }
        }
        // ========== HELPER METHODS ==========

        /// <summary>
        /// Apply sorting to the query
        /// </summary>
        private IQueryable<BranchProduct> ApplySorting(IQueryable<BranchProduct> query, string? sort, string? order)
        {
            var sortOrder = order?.ToLower() == "desc" ? "desc" : "asc";

            return sort?.ToLower() switch
            {
                "name" => sortOrder == "desc"
                    ? query.OrderByDescending(bp => bp.Product.ProductName)
                    : query.OrderBy(bp => bp.Product.ProductName),
                "code" => sortOrder == "desc"
                    ? query.OrderByDescending(bp => bp.Product.ProductCode)
                    : query.OrderBy(bp => bp.Product.ProductCode),
                "saleprice" => sortOrder == "desc"
                    ? query.OrderByDescending(bp => bp.SellPrice)
                    : query.OrderBy(bp => bp.SellPrice),
                "costprice" => sortOrder == "desc"
                    ? query.OrderByDescending(bp => bp.CostPrice)
                    : query.OrderBy(bp => bp.CostPrice),
                "stock" => sortOrder == "desc"
                    ? query.OrderByDescending(bp => bp.ActualStock)
                    : query.OrderBy(bp => bp.ActualStock),
                "createdat" => sortOrder == "desc"
                    ? query.OrderByDescending(bp => bp.Product.CreatedAt)
                    : query.OrderBy(bp => bp.Product.CreatedAt),
                _ => query.OrderByDescending(bp => bp.Product.CreatedAt)
            };
        }

        /// <summary>
        /// Convert BranchProduct entity to ProductUpsertDto response
        /// </summary>
        private ProductUpsertDto MapToProductDto(BranchProduct bp)
        {
            var p = bp.Product;

            // Determine product status
            string productStatus = p.Status == 0 ? "inactive" : "active";

            // Determine stock status
            string stockStatus = bp.ActualStock > 0
                ? (bp.ActualStock > (p.Weight ?? 0) ? "Sẵn hàng" : "Sắp hết")
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
                MinimumStock = null,
                MaximumStock = null,

                // Location & Dimensions
                ShelfLocation = bp.ShelfLocation,
                ShelfLocations = string.IsNullOrEmpty(bp.ShelfLocation) ? new() : new() { bp.ShelfLocation },
                Weight = p.Weight,
                WeightUnit = p.WeightUnit,
                Width = p.Width,
                Length = p.Length,
                Height = p.Height,
                Specification = p.Specification,
                SpecificationDetail = $"{p.Width ?? 0}x{p.Length ?? 0}x{p.Height ?? 0}",

                // Media
                ImageUrl = p.MediaResources.FirstOrDefault(m => m.IsPrimary == 1)?.MediaUrl
                    ?? p.MediaResources.FirstOrDefault()?.MediaUrl,
                Images = p.MediaResources.Select(m => m.MediaUrl).ToList(),

                // Sales & Status
                DirectSale = true,
                SalesChannelLinked = p.SalesChannelLinked == 1,
                IsActive = p.Status == 1,

                // Nested DTOs
                Attributes = p.ProductAttributes.Select(a => new ProductAttributeDto
                {
                    Id = a.AttributeId,
                    Name = a.AttributeName ?? "",
                    Value = a.AttributeValue ?? ""
                }).ToList(),

                ConversionUnits = p.ProductConversionUnits.Select(c => new ConversionUnitDto
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
                UpdatedAt = p.CreatedAt
            };
        }
    }
}
