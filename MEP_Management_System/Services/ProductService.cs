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
    public class ProductService : IProductService
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

        public async Task<ProductUpsertDto?> GetProductByIdAsync(Guid id, ClaimsPrincipal currentUser)
        {
            var branchId = await ResolveBranchIdAsync(currentUser);
            if (branchId == null) return null;

            var bp = await _context.BranchProducts
                .Include(bp => bp.Product).ThenInclude(p => p.Supplier)
                .Include(bp => bp.Product).ThenInclude(p => p.MediaResources)
                .Include(bp => bp.Product).ThenInclude(p => p.ProductAttributes)
                .Include(bp => bp.Product).ThenInclude(p => p.ProductConversionUnits)
                .Include(bp => bp.Product).ThenInclude(p => p.CreatedByUser)
                .Include(bp => bp.Product).ThenInclude(p => p.UpdatedByUser)
                .FirstOrDefaultAsync(bp => bp.ProductId == id && bp.BranchId == branchId);

            if (bp == null) return null;

            var dto = _mapper.Map<ProductUpsertDto>(bp);
            return dto;
        }

        public async Task<ProductPriceDto?> GetProductPriceAsync(Guid productId, ClaimsPrincipal currentUser)
        {
            var branchId = await ResolveBranchIdAsync(currentUser);
            if (branchId == null) return null;

            var bp = await _context.BranchProducts
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
                ConversionUnits = bp.Product?.ProductConversionUnits
                    ?.Where(u => u.DirectSale == 1 || u.DirectSale == null)
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

        public async Task<ProductStockDto?> GetProductStockAsync(Guid productId, ClaimsPrincipal currentUser)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return null;

            var query = _context.BranchProducts
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
    }
}
