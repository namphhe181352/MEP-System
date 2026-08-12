using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Models;
using Microsoft.EntityFrameworkCore;
using AutoMapper;

namespace MEP_Management_System.Services
{
    public class ProductService
    {
        private readonly MEPDatabaseContext _context;
        private readonly ICurrentBranchAccessor _currentBranch;
        private readonly IMapper _mapper;

        public ProductService(MEPDatabaseContext context, ICurrentBranchAccessor currentBranch, IMapper mapper)
        {
            _context = context;
            _currentBranch = currentBranch;
            _mapper = mapper;
        }

        private Guid? GetUserId(ClaimsPrincipal user)
        {
            var userClaim = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;
            return Guid.TryParse(userClaim, out var userId) ? userId : null;
        }

        private async Task<Guid?> ResolveBranchIdAsync(ClaimsPrincipal user)
        {
            var userId = GetUserId(user);
            if (userId == null) return null;

            var userEntity = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId.Value);
            if (userEntity?.DefaultBranchId != null && userEntity.DefaultBranchId != Guid.Empty)
                return userEntity.DefaultBranchId;

            var jwtBranchId = _currentBranch.GetBranchId();
            if (jwtBranchId.HasValue) return jwtBranchId.Value;

            var activeBranch = await _context.Branches.AsNoTracking().Where(b => b.IsActive == 1).FirstOrDefaultAsync();
            return activeBranch?.BranchId;
        }

        private static Guid? GetUserIdStatic(ClaimsPrincipal user)
        {
            var userClaim = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;
            return Guid.TryParse(userClaim, out var userId) ? userId : null;
        }

        private static async Task<Guid?> ResolveBranchIdAsyncStatic(ClaimsPrincipal user, MEPDatabaseContext context, ICurrentBranchAccessor currentBranch)
        {
            var userId = GetUserIdStatic(user);
            if (userId == null) return null;

            var userEntity = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId.Value);
            if (userEntity?.DefaultBranchId != null && userEntity.DefaultBranchId != Guid.Empty)
                return userEntity.DefaultBranchId;

            var jwtBranchId = currentBranch.GetBranchId();
            if (jwtBranchId.HasValue) return jwtBranchId.Value;

            var activeBranch = await context.Branches.AsNoTracking().Where(b => b.IsActive == 1).FirstOrDefaultAsync();
            return activeBranch?.BranchId;
        }

        public static async Task<ProductUpsertDto?> GetProductByIdAsync(Guid id, ClaimsPrincipal currentUser, MEPDatabaseContext context, ICurrentBranchAccessor currentBranch, IMapper mapper)
        {
            var branchId = await ResolveBranchIdAsyncStatic(currentUser, context, currentBranch);
            if (branchId == null) return null;

            var bp = await context.BranchProducts
                .Include(bp => bp.Product).ThenInclude(p => p.Supplier)
                .Include(bp => bp.Product).ThenInclude(p => p.MediaResources)
                .Include(bp => bp.Product).ThenInclude(p => p.ProductAttributes)
                .Include(bp => bp.Product).ThenInclude(p => p.ProductConversionUnits)
                .Include(bp => bp.Product).ThenInclude(p => p.CreatedByUser)
                .Include(bp => bp.Product).ThenInclude(p => p.UpdatedByUser)
                .FirstOrDefaultAsync(bp => bp.ProductId == id && bp.BranchId == branchId);

            if (bp == null) return null;

            // Manual mapping instead of AutoMapper (BranchProduct -> ProductUpsertDto has no map)
            var dto = new ProductUpsertDto
            {
                ProductId = bp.Product.ProductId,
                BranchProductId = bp.BranchProductId,
                Id = bp.Product.ProductId.ToString(),
                ProductCode = bp.Product.ProductCode,
                ProductName = bp.Product.ProductName,
                Barcode = bp.Product.Barcode,
                Unit = bp.Product.BaseUnit ?? "",
                BrandName = bp.Product.BrandName,
                CategoryName = bp.Product.CategoryName,
                SupplierId = bp.Product.SupplierId,
                SupplierName = bp.Product.Supplier?.SupplierName,
                ItemType = bp.Product.ItemType,
                CostPrice = bp.CostPrice,
                SalePrice = bp.SellPrice,
                ActualStock = bp.ActualStock,
                AvailableStock = bp.AvailableStock,
                ReservedStock = bp.ReservedStock,
                MinimumStock = bp.MinimumStock ?? 0,
                MaximumStock = bp.MaximumStock ?? 0,
                ShelfLocation = bp.ShelfLocation,
                ShelfLocations = string.IsNullOrEmpty(bp.ShelfLocation) ? new() : new() { bp.ShelfLocation },
                Weight = bp.Product.Weight,
                WeightUnit = bp.Product.WeightUnit,
                Width = bp.Product.Width,
                Length = bp.Product.Length,
                Height = bp.Product.Height,
                SizeUnit = bp.Product.SizeUnit,
                Specification = bp.Product.Specification,
                SpecificationDetail = $"{bp.Product.Width ?? 0}x{bp.Product.Length ?? 0}x{bp.Product.Height ?? 0}",
                ImageUrl = bp.Product.MediaResources.FirstOrDefault(m => m.IsPrimary == 1)?.MediaUrl
                    ?? bp.Product.MediaResources.FirstOrDefault()?.MediaUrl,
                Images = bp.Product.MediaResources.Select(m => m.MediaUrl).ToList(),
                DirectSale = bp.DirectSale == 1,
                SalesChannelLinked = bp.Product.SalesChannelLinked == 1,
                IsActive = bp.IsActive == 1,
                Attributes = bp.Product.ProductAttributes.Select(a => new ProductAttributeDto
                {
                    Id = a.AttributeId,
                    Name = a.AttributeName ?? "",
                    Value = a.AttributeValue ?? ""
                }).ToList(),
                ConversionUnits = bp.Product.ProductConversionUnits.Select(c => new ProductConversionUnitDto
                {
                    Id = c.ConversionId,
                    Name = c.UnitName,
                    Rate = c.ConvertValue,
                    ConvertValue = c.ConvertValue,
                    ConvertFrom = bp.Product.BaseUnit,
                    Price = c.Price,
                    DirectSale = c.DirectSale == 1
                }).ToList(),
                EstimatedOutAt = bp.Product.CreatedAt?.AddDays(30),
                CreatedAt = bp.Product.CreatedAt,
                UpdatedAt = bp.Product.UpdatedAt,
                CreatedBy = bp.Product.CreatedBy,
                CreatedByName = bp.Product.CreatedByUser?.FullName ?? "Hệ thống",
                UpdatedBy = bp.Product.UpdatedBy,
                UpdatedByName = bp.Product.UpdatedByUser?.FullName ?? "Chưa cập nhật"
            };
            return dto;
        }

        public static async Task<ProductPriceDto?> GetProductPriceAsync(Guid productId, ClaimsPrincipal currentUser, MEPDatabaseContext context, ICurrentBranchAccessor currentBranch)
        {
            var branchId = await ResolveBranchIdAsyncStatic(currentUser, context, currentBranch);
            if (branchId == null) return null;

            var bp = await context.BranchProducts
                .AsNoTracking()
                .Include(b => b.Product)
                    .ThenInclude(p => p.ProductConversionUnits)
                .Include(b => b.Product)
                    .ThenInclude(p => p.MediaResources)
                .Where(b => b.BranchId == branchId && b.ProductId == productId && b.IsActive == 1)
                .FirstOrDefaultAsync();

            if (bp == null) return null;

            var dto = new ProductPriceDto
            {
                ProductId = bp.ProductId,
                ProductName = bp.Product?.ProductName,
                ProductCode = bp.Product?.ProductCode,
                Barcode = bp.Product?.Barcode,
                RetailPrice = bp.SellPrice,
                FloorPrice = bp.CostPrice,
                Unit = bp.Product?.BaseUnit,
                AvailableStock = bp.AvailableStock,
                ImageUrl = bp.Product?.MediaResources?.Where(m => m.IsPrimary == 1).Select(m => m.MediaUrl).FirstOrDefault(),
                DirectSale = bp.DirectSale == 1,
                ConversionUnits = bp.Product?.ProductConversionUnits
                    ?.Where(u => u.DirectSale == 1)
                    .Select(u => new ConversionUnitDto
                    {
                        ConversionId = u.ConversionId,
                        UnitName = u.UnitName,
                        ConvertValue = u.ConvertValue,
                        Price = u.Price,
                        DirectSale = u.DirectSale == 1
                    }).ToList() ?? new System.Collections.Generic.List<ConversionUnitDto>()
            };

            return dto;
        }

        public static async Task<ProductStockDto?> GetProductStockAsync(Guid productId, ClaimsPrincipal currentUser, MEPDatabaseContext context)
        {
            var product = await context.Products.FindAsync(productId);
            if (product == null) return null;

            var query = context.BranchProducts
                .AsNoTracking()
                .Where(b => b.ProductId == productId && b.IsActive == 1);

            var allBp = await query
                .Select(b => new ProductStockDto.BranchStockDto
                {
                    BranchId = b.BranchId,
                    BranchName = b.Branch != null ? b.Branch.BranchName : null,
                    AvailableStock = b.AvailableStock,
                    InTransitStock = b.ReservedStock
                })
                .ToListAsync();

            var dto = new ProductStockDto
            {
                ProductId = productId,
                ProductName = product.ProductName,
                ProductCode = product.ProductCode,
                InStockQty = allBp.Sum(b => b.AvailableStock),
                InTransitQty = allBp.Sum(b => b.InTransitStock),
                ByBranch = allBp
            };

            return dto;
        }

        public async Task<ProductUpsertDto?> GetProductByCodeAsync(string productCode, ClaimsPrincipal currentUser)
        {
            var branchId = await ResolveBranchIdAsync(currentUser);
            if (!branchId.HasValue) return null;

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductCode == productCode);

            if (product == null) return null;

            var branchProduct = await _context.BranchProducts
                .Where(bp => bp.BranchId == branchId.Value && bp.ProductId == product.ProductId)
                .FirstOrDefaultAsync();

            var dto = new ProductUpsertDto
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                ProductCode = product.ProductCode,
                Barcode = product.Barcode,
                Unit = product.BaseUnit,
                BrandName = product.BrandName,
                CategoryName = product.CategoryName,
                SupplierId = product.SupplierId,
                ItemType = product.ItemType,
                ActualStock = branchProduct?.ActualStock ?? 0,
                ReservedStock = branchProduct?.ReservedStock ?? 0,
                AvailableStock = branchProduct?.AvailableStock ?? 0,
                MinimumStock = branchProduct?.MinimumStock,
                MaximumStock = branchProduct?.MaximumStock
            };

            return dto;
        }

        public async Task<List<ProductUpsertDto>> SearchProductsAsync(string keyword, ClaimsPrincipal currentUser)
        {
            var branchId = await ResolveBranchIdAsync(currentUser);
            if (!branchId.HasValue) return new List<ProductUpsertDto>();

            var lowerKeyword = keyword.ToLower();

            var products = await _context.Products
                .Where(p => (p.ProductName != null && p.ProductName.ToLower().Contains(lowerKeyword)) ||
                           (p.ProductCode != null && p.ProductCode.ToLower().Contains(lowerKeyword)) ||
                           (p.BrandName != null && p.BrandName.ToLower().Contains(lowerKeyword)) ||
                           (p.CategoryName != null && p.CategoryName.ToLower().Contains(lowerKeyword)))
                .Take(20)
                .ToListAsync();

            var productIds = products.Select(p => p.ProductId).ToList();

            var branchProducts = await _context.BranchProducts
                .Where(bp => bp.BranchId == branchId.Value && productIds.Contains(bp.ProductId))
                .ToListAsync();

            var result = products.Select(p =>
            {
                var bp = branchProducts.FirstOrDefault(b => b.ProductId == p.ProductId);
                return new ProductUpsertDto
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName ?? "",
                    ProductCode = p.ProductCode ?? "",
                    Barcode = p.Barcode,
                    Unit = p.BaseUnit ?? "",
                    BrandName = p.BrandName,
                    CategoryName = p.CategoryName,
                    SupplierId = p.SupplierId,
                    ItemType = p.ItemType,
                    ActualStock = bp?.ActualStock ?? 0,
                    ReservedStock = bp?.ReservedStock ?? 0,
                    AvailableStock = bp?.AvailableStock ?? 0,
                    MinimumStock = bp?.MinimumStock,
                    MaximumStock = bp?.MaximumStock
                };
            }).ToList();

            return result;
        }
    }
}
