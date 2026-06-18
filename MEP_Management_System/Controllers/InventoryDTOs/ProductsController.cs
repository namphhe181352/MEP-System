using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using MEP_Management_System.Models;
using MEP_Management_System.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MEP_Management_System.Controllers;

[Authorize] // 🔐 Khóa bảo mật API: Bắt buộc phải đăng nhập (Cả Owner và Staff đều vào được)
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly MEPDatabaseContext _context;

    public ProductsController(MEPDatabaseContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Helper nội bộ: Tự động bóc tách mã BranchId từ mã Token JWT của người gọi API
    /// </summary>
    private Guid? GetUserBranchId()
    {
        var branchIdClaim = User.FindFirst("branchId")?.Value ?? User.FindFirst(ClaimTypes.GroupSid)?.Value;
        if (Guid.TryParse(branchIdClaim, out Guid branchId))
        {
            return branchId;
        }
        return null;
    }

    /// <summary>
    /// 1. CHỨC NĂNG: LẤY DANH SÁCH SẢN PHẨM THEO CHI NHÁNH ĐĂNG NHẬP
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] ProductParameters @params)
    {
        var branchId = GetUserBranchId();
        if (branchId == null) return Unauthorized(new { Message = "Không tìm thấy thông tin chi nhánh trong Token định danh." });

        // Chuyển trục truy vấn lấy từ bảng liên kết BranchProducts
        var query = _context.BranchProducts
            .Include(bp => bp.Product)
                .ThenInclude(p => p.MediaResources)
            .Where(bp => bp.BranchId == branchId.Value)
            .AsQueryable();

        // Lọc nâng cao: Tìm kiếm tương đối theo Mã sản phẩm hoặc Tên sản phẩm gốc
        if (!string.IsNullOrWhiteSpace(@params.SearchTerm))
        {
            string search = @params.SearchTerm.Trim().ToLower();
            query = query.Where(bp => bp.Product.ProductCode.ToLower().Contains(search)
                                   || bp.Product.ProductName.ToLower().Contains(search));
        }

        // Lọc nâng cao: Theo thương hiệu (Brand)
        if (!string.IsNullOrWhiteSpace(@params.BrandName))
        {
            string brand = @params.BrandName.Trim().ToLower();
            query = query.Where(bp => bp.Product.BrandName != null && bp.Product.BrandName.ToLower() == brand);
        }

        // Lọc nâng cao: Theo nhà cung cấp (Supplier)
        if (@params.SupplierId.HasValue)
        {
            query = query.Where(bp => bp.Product.SupplierId == @params.SupplierId.Value);
        }

        var totalCount = await query.CountAsync();

        // Trả ra cấu trúc dữ liệu gọn gàng sạch sẽ (Bỏ thuộc tính Product trỏ ngược để tránh lỗi JSON sập 500)
        var branchProducts = await query
            .OrderByDescending(bp => bp.Product.CreatedAt)
            .Skip((@params.PageNumber - 1) * @params.PageSize)
            .Take(@params.PageSize)
            .Select(bp => new
            {
                bp.BranchProductId,
                bp.BranchId,
                bp.ProductId,
                bp.ActualStock,     // Số lượng tồn tại quầy/kho chi nhánh này
                bp.AvailableStock,  // Số lượng sẵn sàng bán
                bp.ShelfLocation,   // Vị trí đặt trên kệ hàng
                ProductDetails = new
                {
                    bp.Product.ProductCode,
                    bp.Product.ProductName,
                    bp.Product.Barcode,
                    bp.Product.BaseUnit,
                    bp.Product.Specification,
                    bp.Product.ItemType,
                    bp.Product.BrandName,
                    bp.Product.CategoryName,
                    bp.Product.SalesChannelLinked,
                    Medias = bp.Product.MediaResources.Select(m => new
                    {
                        m.MediaId,
                        m.MediaUrl,
                        m.MediaType,
                        m.IsPrimary
                    }).ToList()
                }
            })
            .ToListAsync();

        return Ok(new
        {
            TotalCount = totalCount,
            PageSize = @params.PageSize,
            CurrentPage = @params.PageNumber,
            TotalPages = (int)Math.Ceiling(totalCount / (double)@params.PageSize),
            Data = branchProducts
        });
    }

    /// <summary>
    /// 2. CHỨC NĂNG: XEM CHI TIẾT SẢN PHẨM CỦA CHI NHÁNH
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProductById(Guid id)
    {
        var branchId = GetUserBranchId();
        if (branchId == null) return Unauthorized(new { Message = "Không tìm thấy thông tin chi nhánh." });

        var branchProduct = await _context.BranchProducts
            .Include(bp => bp.Product)
                .ThenInclude(p => p.MediaResources)
            .FirstOrDefaultAsync(bp => bp.ProductId == id && bp.BranchId == branchId.Value);

        if (branchProduct == null)
        {
            return NotFound(new { Message = "Sản phẩm không tồn tại hoặc không thuộc quyền quản lý của chi nhánh này." });
        }

        // Trả về dữ liệu chi tiết
        return Ok(new
        {
            branchProduct.BranchProductId,
            branchProduct.BranchId,
            branchProduct.ProductId,
            branchProduct.ActualStock,
            branchProduct.AvailableStock,
            branchProduct.ShelfLocation,
            Product = new
            {
                branchProduct.Product.ProductCode,
                branchProduct.Product.ProductName,
                branchProduct.Product.Barcode,
                branchProduct.Product.BaseUnit,
                branchProduct.Product.Specification,
                branchProduct.Product.ItemType,
                branchProduct.Product.BrandName,
                branchProduct.Product.CategoryName,
                branchProduct.Product.Weight,
                branchProduct.Product.Width,
                branchProduct.Product.Length,
                branchProduct.Product.Height,
                branchProduct.Product.SalesChannelLinked,
                Medias = branchProduct.Product.MediaResources.Select(m => new { m.MediaId, m.MediaUrl, m.IsPrimary }).ToList()
            }
        });
    }

    /// <summary>
    /// 3. CHỨC NĂNG: THÊM MỚI SẢN PHẨM 
    /// </summary>
    [Authorize(Roles = "Owner")]
    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductDto dto)
    {
        var branchId = GetUserBranchId();
        if (branchId == null) return Unauthorized(new { Message = "Yêu cầu đăng nhập tài khoản Owner hợp lệ." });

        bool isCodeExist = await _context.Products.AnyAsync(p => p.ProductCode.ToLower() == dto.ProductCode.Trim().ToLower());
        if (isCodeExist)
        {
            return BadRequest(new { Message = $"Mã sản phẩm '{dto.ProductCode}' đã tồn tại trong danh mục hệ thống." });
        }

        // Gán biến dùng chung cho đồng bộ thời gian
        var currentLocalTime = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // Bước 3.1: Khởi tạo sản phẩm gốc
        var product = new Product
        {
            ProductId = Guid.NewGuid(),
            ProductCode = dto.ProductCode.Trim(),
            ProductName = dto.ProductName.Trim(),
            Barcode = dto.Barcode?.Trim(),
            BaseUnit = dto.BaseUnit.Trim(),
            Specification = dto.Specification,
            ItemType = dto.ItemType,
            BrandName = dto.BrandName,
            CategoryName = dto.CategoryName,
            SupplierId = dto.SupplierId,
            SalesChannelLinked = 1,
            CreatedAt = currentLocalTime // 🎯 ĐÃ SỬA: Hợp gu PostgreSQL
        };

        if (dto.Medias != null && dto.Medias.Any())
        {
            foreach (var m in dto.Medias)
            {
                product.MediaResources.Add(new MediaResource
                {
                    MediaId = Guid.NewGuid(),
                    MediaUrl = m.MediaUrl.Trim(),
                    MediaType = m.MediaType ?? "IMAGE",
                    IsPrimary = m.IsPrimary,
                    CreatedAt = currentLocalTime // 🎯 ĐÃ SỬA: Hợp gu PostgreSQL
                });
            }
        }

        _context.Products.Add(product);

        // Bước 3.2: Tự động phân bổ vào chi nhánh hiện tại
        var branchProduct = new BranchProduct
        {
            BranchProductId = Guid.NewGuid(),
            BranchId = branchId.Value,
            ProductId = product.ProductId,
            ActualStock = 0,
            AvailableStock = 0,
            ShelfLocation = "Chưa xếp vị trí"
        };
        _context.BranchProducts.Add(branchProduct);

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProductById), new { id = product.ProductId }, product);
    }

    /// <summary>
    /// 4. CHỨC NĂNG: CẬP NHẬT THÔNG TIN SẢN PHẨM (Dành riêng cho Owner)
    /// </summary>
    [Authorize(Roles = "Owner")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, [FromBody] UpdateProductDto dto)
    {
        var branchId = GetUserBranchId();
        if (branchId == null) return Unauthorized(new { Message = "Không tìm thấy thông tin chi nhánh xác thực." });

        // Tìm xem sản phẩm có thuộc đúng chi nhánh của Owner này không thì mới cho sửa thông tin
        var hasAccess = await _context.BranchProducts.AnyAsync(bp => bp.ProductId == id && bp.BranchId == branchId.Value);
        if (!hasAccess) return Forbid(); // Trả về lỗi 403 từ chối quyền can thiệp

        var product = await _context.Products
            .Include(p => p.MediaResources)
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null) return NotFound(new { Message = "Không tìm thấy mặt hàng yêu cầu." });

        product.ProductName = dto.ProductName.Trim();
        product.Barcode = dto.Barcode?.Trim();
        product.Specification = dto.Specification;
        product.ItemType = dto.ItemType;
        product.BrandName = dto.BrandName;
        product.CategoryName = dto.CategoryName;
        product.SupplierId = dto.SupplierId;
        product.Weight = dto.Weight;
        product.WeightUnit = dto.WeightUnit;
        product.Width = dto.Width;
        product.Length = dto.Length;
        product.Height = dto.Height;
        product.SizeUnit = dto.SizeUnit;

        _context.MediaResources.RemoveRange(product.MediaResources);

        if (dto.Medias != null && dto.Medias.Any())
        {
            foreach (var m in dto.Medias)
            {
                product.MediaResources.Add(new MediaResource
                {
                    MediaId = Guid.NewGuid(),
                    MediaUrl = m.MediaUrl.Trim(),
                    MediaType = m.MediaType ?? "IMAGE",
                    IsPrimary = m.IsPrimary,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        _context.Products.Update(product);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Cập nhật dữ liệu hàng hóa và bộ sưu tập đa phương tiện thành công." });
    }

    /// <summary>
    /// 5. CHỨC NĂNG: NGỪNG KINH DOANH SẢN PHẨM (Owner quản trị)
    /// </summary>
    [Authorize(Roles = "Owner")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeactivateProduct(Guid id)
    {
        var branchId = GetUserBranchId();
        var hasAccess = await _context.BranchProducts.AnyAsync(bp => bp.ProductId == id && bp.BranchId == branchId!.Value);
        if (!hasAccess) return Forbid();

        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound(new { Message = "Mặt hàng không tồn tại." });

        product.SalesChannelLinked = 0; // Hạ trạng thái ngừng bán kinh doanh toàn chuỗi hoặc cục bộ

        _context.Products.Update(product);
        await _context.SaveChangesAsync();

        return Ok(new { Message = $"Sản phẩm '{product.ProductName}' đã được cấu hình NGỪNG KINH DOANH." });
    }

    /// <summary>
    /// 6. CHỨC NĂNG: KÍCH HOẠT LẠI SẢN PHẨM (Owner quản trị)
    /// </summary>
    [Authorize(Roles = "Owner")]
    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> ActivateProduct(Guid id)
    {
        var branchId = GetUserBranchId();
        var hasAccess = await _context.BranchProducts.AnyAsync(bp => bp.ProductId == id && bp.BranchId == branchId!.Value);
        if (!hasAccess) return Forbid();

        var product = await _context.Products.FindAsync(id);
        if (product == null) return NotFound(new { Message = "Mặt hàng không tồn tại." });

        product.SalesChannelLinked = 1; // Bật hoạt động trở lại

        _context.Products.Update(product);
        await _context.SaveChangesAsync();

        return Ok(new { Message = $"Đã tái kích hoạt trạng thái bán hàng cho mặt hàng '{product.ProductName}'." });
    }
}