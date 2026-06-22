using AutoMapper;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
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
    public class ProductsController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;
        private readonly IMapper _mapper;

        public ProductsController(MEPDatabaseContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        private Guid? GetUserBranchId()
        {
            var branchIdClaim = User.FindFirst("branchId")?.Value ?? User.FindFirst(ClaimTypes.GroupSid)?.Value;
            return Guid.TryParse(branchIdClaim, out Guid branchId) ? branchId : null;
        }

        /// <summary>
        /// 1. GET /api/products - List all products with pagination, filtering, and sorting
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] DTOs.InventoryDTOs.ProductParameters @params)
        {
            try
            {
                var branchId = GetUserBranchId();
                if (branchId == null)
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
                    .Where(bp => bp.BranchId == branchId.Value).AsQueryable();

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
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetProductById(Guid id)
        {
            try
            {
                var branchId = GetUserBranchId();
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
                    .Include(bp => bp.Product).ThenInclude(p => p.Supplier)
                    .Include(bp => bp.Product).ThenInclude(p => p.MediaResources)
                    .Include(bp => bp.Product).ThenInclude(p => p.ProductAttributes)
                    .Include(bp => bp.Product).ThenInclude(p => p.ProductConversionUnits)
                    .FirstOrDefaultAsync(bp => bp.ProductId == id && bp.BranchId == branchId.Value);

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
        public async Task<IActionResult> CreateProduct([FromBody] ProductUpsertDto dto)
        {
            try
            {
                var branchId = GetUserBranchId();
                if (branchId == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "JWT token is missing or invalid",
                        Errors = new() { "Authorization header required" }
                    });
                }

                // Validate required fields
                var errors = new List<string>();
                if (string.IsNullOrWhiteSpace(dto.ProductCode)) errors.Add("productCode is required");
                if (string.IsNullOrWhiteSpace(dto.ProductName)) errors.Add("productName is required");
                if (string.IsNullOrWhiteSpace(dto.Unit)) errors.Add("unit is required");
                if (dto.CostPrice <= 0) errors.Add("costPrice must be greater than 0");
                if (dto.SalePrice <= 0) errors.Add("salePrice must be greater than 0");
                if (dto.SalePrice < dto.CostPrice) errors.Add("salePrice should be >= costPrice");

                if (errors.Any())
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                // Check if product code already exists
                bool isCodeExist = await _context.Products
                    .AnyAsync(p => p.ProductCode.ToLower() == dto.ProductCode.Trim().ToLower());
                if (isCodeExist)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Product creation failed",
                        Errors = new() { $"Product code '{dto.ProductCode}' already exists" }
                    });
                }

                var currentLocalTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var product = _mapper.Map<Product>(dto);
                    product.ProductId = Guid.NewGuid();
                    product.CreatedAt = currentLocalTime;
                    product.ProductConversionUnits = new List<ProductConversionUnit>();
                    product.BranchProducts = new List<BranchProduct>();

                    _context.Products.Add(product);

                    // Handle images
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

                    // Handle attributes
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

                    // Handle conversion units
                    if (dto.ConversionUnits != null && dto.ConversionUnits.Any())
                    {
                        foreach (var unit in dto.ConversionUnits)
                        {
                            var convUnit = new ProductConversionUnit
                            {
                                ConversionId = Guid.NewGuid(),
                                ProductId = product.ProductId,
                                UnitName = unit.Name,
                                ConvertValue = unit.Rate > 0 ? unit.Rate : unit.ConvertValue,
                                Price = unit.Price,
                                DirectSale = (short?)(unit.DirectSale ? 1 : 0),
                                Product = null!
                            };
                            _context.ProductConversionUnits.Add(convUnit);
                            _context.Entry(convUnit).State = EntityState.Added;
                        }
                    }

                    // Handle branch product
                    var branchProduct = new BranchProduct
                    {
                        BranchProductId = Guid.NewGuid(),
                        BranchId = branchId.Value,
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
                    _context.Entry(branchProduct).State = EntityState.Added;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var createdDto = MapToProductDto(branchProduct);
                    return CreatedAtAction(nameof(GetProductById), new { id = product.ProductId },
                        new ApiResponse<ProductUpsertDto>
                        {
                            Success = true,
                            Message = "Product created successfully",
                            Data = createdDto
                        });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error creating product",
                    Errors = new() { ex.Message }
                });
            }
        }

        /// <summary>
        /// 4. PUT /api/products/{id} - Update product
        /// </summary>
        [Authorize(Roles = "Owner")]
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] ProductUpsertDto dto)
        {
            try
            {
                var branchId = GetUserBranchId();
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
                    .Include(b => b.Product).ThenInclude(p => p.MediaResources)
                    .Include(b => b.Product).ThenInclude(p => p.ProductAttributes)
                    .Include(b => b.Product).ThenInclude(p => p.ProductConversionUnits)
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

                // Validate required fields
                var errors = new List<string>();
                if (string.IsNullOrWhiteSpace(dto.ProductName)) errors.Add("productName is required");
                if (dto.CostPrice < 0) errors.Add("costPrice cannot be negative");
                if (dto.SalePrice < 0) errors.Add("salePrice cannot be negative");
                if (dto.SalePrice > 0 && dto.CostPrice > 0 && dto.SalePrice < dto.CostPrice)
                    errors.Add("salePrice should be >= costPrice");

                if (errors.Any())
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                var currentLocalTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Update Product entity
                    _mapper.Map(dto, bp.Product);
                    bp.Product.CreatedAt = bp.Product.CreatedAt ?? currentLocalTime;
                    _context.Products.Update(bp.Product);

                    // Update BranchProduct
                    bp.ActualStock = dto.ActualStock;
                    bp.AvailableStock = dto.AvailableStock;
                    bp.ReservedStock = dto.ReservedStock;
                    bp.ShelfLocation = dto.ShelfLocation ?? "Chưa xếp vị trí";
                    bp.CostPrice = dto.CostPrice;
                    bp.SellPrice = dto.SalePrice;
                    _context.BranchProducts.Update(bp);

                    // Update images
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

                    // Update attributes
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

                    // Update conversion units
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

                    return Ok(new ApiResponse<ProductUpsertDto>
                    {
                        Success = true,
                        Message = "Product updated successfully",
                        Data = MapToProductDto(bp)
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error updating product",
                    Errors = new() { ex.Message }
                });
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
                var branchId = GetUserBranchId();
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

                bp.IsActive = 0;
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
                var branchId = GetUserBranchId();
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
