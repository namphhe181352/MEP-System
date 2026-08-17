using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Exceptions;
using MEP_Management_System.Models;
using MEP_Management_System.Services.SaleServices;

namespace MEP_Management_System.Services;

/// <summary>
/// Triển khai <see cref="IGoodsIssueService"/>.
/// Ghi nhận phiếu xuất kho nội bộ vào các bảng GoodsIssues/GoodsIssueItems/GoodsIssueAttachments
/// (model code-first mới — cần chạy migration để tạo bảng trước khi dùng runtime).
/// </summary>
public class GoodsIssueServiceImpl : IGoodsIssueService
{
    private readonly MEPDatabaseContext _context;
    private readonly ICurrentBranchAccessor _currentBranch;
    private readonly ICustomerService _customerService;
    private readonly IWebHostEnvironment _env;

    public GoodsIssueServiceImpl(
        MEPDatabaseContext context,
        ICurrentBranchAccessor currentBranch,
        ICustomerService customerService,
        IWebHostEnvironment env)
    {
        _context = context;
        _currentBranch = currentBranch;
        _customerService = customerService;
        _env = env;
    }

    private Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst("userId")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out Guid userId) ? userId : null;
    }

    private bool IsOwner(ClaimsPrincipal user) => user.IsInRole("Owner");

    /// <summary>Giải quyết BranchId hiện hành, fallback sang chi nhánh active đầu tiên.</summary>
    private async Task<Guid> ResolveBranchIdAsync(Guid? branchId)
    {
        Guid? resolved = branchId ?? _currentBranch.GetBranchId();
        if (!resolved.HasValue || resolved == Guid.Empty)
        {
            var active = await _context.Branches
                .AsNoTracking()
                .Where(b => b.IsActive == 1)
                .OrderBy(b => b.CreatedAt)
                .Select(b => (Guid?)b.BranchId)
                .FirstOrDefaultAsync();
            resolved = active;
        }

        if (!resolved.HasValue || resolved == Guid.Empty)
            throw new BusinessException("Khong xac dinh duoc chi nhanh hien tai.", "Vui long chon chi nhanh truoc khi thuc hien.");

        return resolved.Value;
    }

    // ============== LIST ==============
    public async Task<ApiResponse<PaginatedResponse<GoodsIssueResponseDto>>> GetGoodsIssuesAsync(
        ClaimsPrincipal user, Guid? branchId, string? keyword, string? status,
        string? issueType, DateTime? fromDate, DateTime? toDate, int page, int pageSize)
    {
        var query = _context.GoodsIssues.AsNoTracking();

        if (!IsOwner(user))
        {
            var branch = await ResolveBranchIdAsync(branchId);
            query = query.Where(g => g.BranchId == branch);
        }
        else if (branchId.HasValue && branchId != Guid.Empty)
        {
            query = query.Where(g => g.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(g => g.Status == status.Trim());

        if (!string.IsNullOrWhiteSpace(issueType))
            query = query.Where(g => g.IssueType == issueType.Trim());

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(g =>
                g.IssueNumber.Contains(kw) ||
                (g.CustomerName != null && g.CustomerName.Contains(kw)) ||
                (g.Reference != null && g.Reference.Contains(kw)));
        }

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(g => g.IssueDate == null || g.IssueDate >= from);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date.AddDays(1);
            query = query.Where(g => g.IssueDate == null || g.IssueDate < to);
        }

        query = query.OrderByDescending(g => g.IssueDate).ThenByDescending(g => g.CreatedAt);

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : pageSize;

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new ApiResponse<PaginatedResponse<GoodsIssueResponseDto>>
        {
            Success = true,
            Message = "Goods issues retrieved successfully",
            Data = new PaginatedResponse<GoodsIssueResponseDto>
            {
                Items = rows.Select(g => MapToResponse(g, loadLines: false)).ToList(),
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                HasNextPage = page < totalPages,
                HasPreviousPage = page > 1
            }
        };
    }

    // ============== DETAIL ==============
    public async Task<ApiResponse<GoodsIssueResponseDto>> GetGoodsIssueByIdAsync(
        ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        IQueryable<GoodsIssue> query;
        if (!IsOwner(user))
        {
            var branch = await ResolveBranchIdAsync(branchId);
            query = _context.GoodsIssues.Where(g => g.BranchId == branch);
        }
        else
        {
            query = _context.GoodsIssues;
        }

        var goodsIssue = await query
            .Include(g => g.Items)
            .Include(g => g.Attachments)
            .FirstOrDefaultAsync(g => g.GoodsIssueId == id);
        if (goodsIssue == null)
            throw new KeyNotFoundException($"Khong tim thay phieu xuat kho voi ID: {id}");

        return new ApiResponse<GoodsIssueResponseDto>
        {
            Success = true,
            Message = "Goods issue retrieved successfully",
            Data = MapToResponse(goodsIssue, loadLines: true)
        };
    }

    // ============== CREATE ==============
    public async Task<ApiResponse<object>> CreateGoodsIssueAsync(
        ClaimsPrincipal user, GoodsIssueCreateDto dto, Guid? branchId)
    {
        var branch = await ResolveBranchIdAsync(branchId);

        var validLines = (dto?.Lines ?? new List<GoodsIssueItemCreateDto>())
            .Where(l => l != null && HasProductInfo(l))
            .ToList();

        if (validLines.Count == 0)
            throw new BusinessException("Phai co it nhat 1 dong san pham hop le.");

        foreach (var l in validLines)
        {
            if (l.Quantity <= 0)
                throw new BusinessException("So luong cac dong phai lon hon 0.");
        }

        var currentUserId = GetCurrentUserId(user);
        var createdAt = DateTime.UtcNow;

        var entity = new GoodsIssue
        {
            GoodsIssueId = Guid.NewGuid(),
            IssueNumber = string.IsNullOrWhiteSpace(dto?.IssueNumber)
                ? GenerateIssueNumber()
                : dto.IssueNumber.Trim(),
            IssueType = string.IsNullOrWhiteSpace(dto?.IssueType) ? "Xuất kho khác" : dto.IssueType.Trim(),
            CustomerId = dto?.CustomerId,
            CustomerName = dto?.CustomerName,
            Description = dto?.Description,
            Reference = dto?.Reference,
            IssueDate = dto?.Date,
            CreatedBy = dto?.CreatedBy,
            CreatedByUserId = currentUserId,
            PaymentMethod = dto?.PaymentMethod,
            TotalPayment = dto?.TotalPayment,
            BranchId = branch,
            Status = "PENDING",
            CreatedAt = createdAt
        };

        decimal total = 0;
        foreach (var l in validLines)
        {
            var lineTotal = l.TotalAmount > 0 ? l.TotalAmount : Math.Round(l.Quantity * (l.UnitPrice ?? 0));
            total += lineTotal;
            entity.Items.Add(new GoodsIssueItem
            {
                GoodsIssueItemId = Guid.NewGuid(),
                LineNumber = l.LineNumber,
                ProductId = l.ProductId,
                ProductCode = l.ProductCode,
                ProductName = l.ProductName,
                Unit = l.Unit,
                LotNumber = l.LotNumber,
                ExpiryDate = l.ExpiryDate,
                WarehouseId = l.WarehouseId,
                WarehouseName = l.WarehouseName,
                Location = l.Location,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                TotalAmount = lineTotal
            });
        }
        entity.TotalAmount = total;

        _context.GoodsIssues.Add(entity);
        await _context.SaveChangesAsync();

        return new ApiResponse<object>
        {
            Success = true,
            Message = "Tao phieu xuat kho thanh cong.",
            Data = new { GoodsIssueId = entity.GoodsIssueId, IssueNumber = entity.IssueNumber, Status = entity.Status }
        };
    }

    // ============== UPDATE ==============
    public async Task<ApiResponse<GoodsIssueResponseDto>> UpdateGoodsIssueAsync(
        ClaimsPrincipal user, Guid id, GoodsIssueCreateDto dto, Guid? branchId)
    {
        var query = _context.GoodsIssues
            .Include(g => g.Items)
            .Where(g => g.GoodsIssueId == id);

        if (!IsOwner(user))
        {
            var branch = await ResolveBranchIdAsync(branchId);
            query = query.Where(g => g.BranchId == branch);
        }

        var entity = await query.FirstOrDefaultAsync();
        if (entity == null)
            throw new KeyNotFoundException($"Khong tim thay phieu xuat kho voi ID: {id}");

        if (entity.Status == "CANCELLED")
            throw new BusinessException("Phieu xuat kho da bi huy. Khong the chinh sua.");

        if (!string.IsNullOrWhiteSpace(dto?.IssueNumber)) entity.IssueNumber = dto.IssueNumber.Trim();
        if (!string.IsNullOrWhiteSpace(dto?.IssueType)) entity.IssueType = dto.IssueType.Trim();
        entity.CustomerId = dto?.CustomerId;
        entity.CustomerName = dto?.CustomerName;
        entity.Description = dto?.Description;
        entity.Reference = dto?.Reference;
        if (dto?.Date.HasValue == true) entity.IssueDate = dto.Date;
        if (!string.IsNullOrWhiteSpace(dto?.CreatedBy)) entity.CreatedBy = dto.CreatedBy;
        entity.PaymentMethod = dto?.PaymentMethod;
        entity.TotalPayment = dto?.TotalPayment;

        if (entity.Status == "PENDING")
        {
            var validLines = (dto?.Lines ?? new List<GoodsIssueItemCreateDto>())
                .Where(l => l != null && HasProductInfo(l))
                .ToList();

            _context.GoodsIssueItems.RemoveRange(entity.Items);

            decimal total = 0;
            if (validLines.Count > 0)
            {
                foreach (var l in validLines)
                {
                    if (l.Quantity <= 0)
                        throw new BusinessException("So luong cac dong phai lon hon 0.");
                    var lineTotal = l.TotalAmount > 0 ? l.TotalAmount : Math.Round(l.Quantity * (l.UnitPrice ?? 0));
                    total += lineTotal;
                    entity.Items.Add(new GoodsIssueItem
                    {
                        GoodsIssueItemId = Guid.NewGuid(),
                        GoodsIssueId = entity.GoodsIssueId,
                        LineNumber = l.LineNumber,
                        ProductId = l.ProductId,
                        ProductCode = l.ProductCode,
                        ProductName = l.ProductName,
                        Unit = l.Unit,
                        LotNumber = l.LotNumber,
                        ExpiryDate = l.ExpiryDate,
                        WarehouseId = l.WarehouseId,
                        WarehouseName = l.WarehouseName,
                        Location = l.Location,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        TotalAmount = lineTotal
                    });
                }
                entity.TotalAmount = total;
            }
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var reloaded = await _context.GoodsIssues
            .AsNoTracking()
            .Include(g => g.Items)
            .Include(g => g.Attachments)
            .FirstOrDefaultAsync(g => g.GoodsIssueId == id);

        return new ApiResponse<GoodsIssueResponseDto>
        {
            Success = true,
            Message = "Cap nhat phieu xuat kho thanh cong.",
            Data = reloaded == null ? MapToResponse(entity, loadLines: true) : MapToResponse(reloaded, loadLines: true)
        };
    }

    // ============== DELETE ==============
    public async Task<ApiResponse<object>> DeleteGoodsIssueAsync(
        ClaimsPrincipal user, Guid id, Guid? branchId)
    {
        var query = _context.GoodsIssues
            .Include(g => g.Items)
            .Include(g => g.Attachments)
            .Where(g => g.GoodsIssueId == id);

        if (!IsOwner(user))
        {
            var branch = await ResolveBranchIdAsync(branchId);
            query = query.Where(g => g.BranchId == branch);
        }

        var entity = await query.FirstOrDefaultAsync();
        if (entity == null)
            throw new KeyNotFoundException($"Khong tim thay phieu xuat kho voi ID: {id}");

        foreach (var att in entity.Attachments)
        {
            if (!string.IsNullOrWhiteSpace(att.MediaUrl))
                TryDeleteFile(att.MediaUrl);
        }

        _context.GoodsIssueAttachments.RemoveRange(entity.Attachments);
        _context.GoodsIssueItems.RemoveRange(entity.Items);
        _context.GoodsIssues.Remove(entity);
        await _context.SaveChangesAsync();

        return new ApiResponse<object>
        {
            Success = true,
            Message = "Xoa phieu xuat kho thanh cong.",
            Data = new { GoodsIssueId = id }
        };
    }

    // ============== SEARCH PRODUCTS ==============
    public async Task<ApiResponse<List<GoodsIssueSearchProductDto>>> SearchProductsAsync(
        ClaimsPrincipal user, Guid? branchId, string? keyword)
    {
        var branch = await ResolveBranchIdAsync(branchId);

        var query = _context.BranchProducts
            .AsNoTracking()
            .Include(bp => bp.Product)
            .Where(bp => bp.BranchId == branch && (bp.Product.Status == 1 || bp.Product.Status == null));

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(bp =>
                bp.Product.ProductCode.Contains(kw) ||
                bp.Product.ProductName.Contains(kw) ||
                (bp.Product.Barcode != null && bp.Product.Barcode.Contains(kw)));
        }

        var branchName = await _context.Branches
            .AsNoTracking()
            .Where(b => b.BranchId == branch)
            .Select(b => b.BranchName)
            .FirstOrDefaultAsync();

        var rows = await query.OrderBy(bp => bp.Product.ProductName).Take(50).ToListAsync();

        var items = rows.Select(bp => new GoodsIssueSearchProductDto
        {
            Id = bp.ProductId.ToString(),
            Code = bp.Product.ProductCode,
            Name = bp.Product.ProductName,
            Unit = bp.Product.BaseUnit,
            Price = bp.SellPrice,
            Stock = bp.AvailableStock,
            WarehouseId = branch.ToString(),
            WarehouseName = branchName,
            HasLotControl = bp.Product.WarrantyPeriod > 0,
            HasExpiryControl = bp.Product.WarrantyPeriod > 0
        }).ToList();

        return new ApiResponse<List<GoodsIssueSearchProductDto>>
        {
            Success = true,
            Message = "Products retrieved successfully",
            Data = items
        };
    }

    // ============== CUSTOMERS ==============
    public async Task<ApiResponse<List<GoodsIssueCustomerDto>>> GetCustomersAsync(
        ClaimsPrincipal user, Guid? branchId, string? search)
    {
        var branch = await ResolveBranchIdAsync(branchId);
        var customers = await _customerService.GetAllCustomersAsync(branch, search);

        var items = customers.Select(c => new GoodsIssueCustomerDto
        {
            Id = c.CustomerId.ToString(),
            Code = c.CustomerId.ToString(),
            Name = c.CustomerName,
            Phone = c.PhoneNumber,
            Address = c.Address
        }).ToList();

        return new ApiResponse<List<GoodsIssueCustomerDto>>
        {
            Success = true,
            Message = "Customers retrieved successfully",
            Data = items
        };
    }

    public async Task<ApiResponse<GoodsIssueCustomerDto>> CreateCustomerAsync(
        ClaimsPrincipal user, Guid? branchId, CreateCustomerRequest request)
    {
        var branch = await ResolveBranchIdAsync(branchId);
        var userId = GetCurrentUserId(user) ?? Guid.Empty;

        if (request == null || string.IsNullOrWhiteSpace(request.CustomerName))
            throw new BusinessException("Vui long nhap ten khach hang.");

        var (customer, errorCode, errorMessage) = await _customerService.CreateCustomerAsync(branch, userId, request);
        if (customer == null)
            throw new BusinessException(errorMessage ?? "Khong the tao khach hang.");

        var dto = new GoodsIssueCustomerDto
        {
            Id = customer.CustomerId.ToString(),
            Code = customer.CustomerId.ToString(),
            Name = customer.CustomerName,
            Phone = customer.PhoneNumber,
            Address = customer.Address
        };

        return new ApiResponse<GoodsIssueCustomerDto>
        {
            Success = true,
            Message = "Tao khach hang thanh cong.",
            Data = dto
        };
    }

    // ============== WAREHOUSES ==============
    public async Task<ApiResponse<List<GoodsIssueWarehouseDto>>> GetWarehousesAsync(
        ClaimsPrincipal user, Guid? branchId)
    {
        var branches = await _context.Branches
            .AsNoTracking()
            .Where(b => b.IsActive == 1)
            .OrderBy(b => b.BranchName)
            .ToListAsync();

        var items = branches.Select(b => new GoodsIssueWarehouseDto
        {
            Id = b.BranchId.ToString(),
            Code = b.BranchCode,
            Name = b.BranchName
        }).ToList();

        return new ApiResponse<List<GoodsIssueWarehouseDto>>
        {
            Success = true,
            Message = "Warehouses retrieved successfully",
            Data = items
        };
    }

    // ============== ATTACHMENTS ==============
    public async Task<ApiResponse<GoodsIssueAttachmentResponseDto>> UploadAttachmentAsync(
        ClaimsPrincipal user, Guid? branchId, Guid id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            throw new BusinessException("Vui long chon file de dinh kem.");

        var branch = await ResolveBranchIdAsync(branchId);

        var exists = await _context.GoodsIssues
            .AsNoTracking()
            .AnyAsync(g => g.GoodsIssueId == id && g.BranchId == branch);
        if (!exists)
            throw new KeyNotFoundException($"Khong tim thay phieu xuat kho voi ID: {id}");

        var fileName = file.FileName.Replace("\\", "_").Replace("/", "_");
        var safeName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{fileName}";
        var relFolder = $"uploads/goods-issue/{branch}/{id}";
        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var folder = Path.Combine(webRoot, "uploads", "goods-issue", branch.ToString(), id.ToString());
        Directory.CreateDirectory(folder);

        var fullPath = Path.Combine(folder, safeName);
        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var mediaUrl = $"/{relFolder}/{safeName}";
        var userId = GetCurrentUserId(user);

        var attachment = new GoodsIssueAttachment
        {
            GoodsIssueAttachmentId = Guid.NewGuid(),
            GoodsIssueId = id,
            FileName = fileName,
            ContentType = file.ContentType,
            Size = file.Length,
            MediaUrl = mediaUrl,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = userId
        };

        _context.GoodsIssueAttachments.Add(attachment);
        await _context.SaveChangesAsync();

        return new ApiResponse<GoodsIssueAttachmentResponseDto>
        {
            Success = true,
            Message = "Upload file dinh kem thanh cong.",
            Data = new GoodsIssueAttachmentResponseDto
            {
                GoodsIssueAttachmentId = attachment.GoodsIssueAttachmentId,
                FileName = attachment.FileName,
                ContentType = attachment.ContentType,
                Size = attachment.Size,
                MediaUrl = mediaUrl,
                UploadedAt = attachment.UploadedAt
            }
        };
    }

    // ============== HELPERS ==============

    private static bool HasProductInfo(GoodsIssueItemCreateDto l)
        => !string.IsNullOrWhiteSpace(l.ProductId)
           || !string.IsNullOrWhiteSpace(l.ProductCode)
           || !string.IsNullOrWhiteSpace(l.ProductName);

    private static string GenerateIssueNumber()
        => $"XK{DateTime.UtcNow:yyMMddHHmmss}";

    private void TryDeleteFile(string mediaUrl)
    {
        try
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var path = Path.Combine(webRoot, mediaUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) File.Delete(path);
        }
        catch { /* best-effort */ }
    }

    private static GoodsIssueResponseDto MapToResponse(GoodsIssue g, bool loadLines)
    {
        var dto = new GoodsIssueResponseDto
        {
            Id = g.GoodsIssueId.ToString(),
            GoodsIssueId = g.GoodsIssueId,
            IssueNumber = g.IssueNumber,
            IssueType = g.IssueType,
            CustomerId = g.CustomerId,
            Customer = g.CustomerName,
            CustomerName = g.CustomerName,
            Description = g.Description,
            Reference = g.Reference,
            Date = g.IssueDate,
            CreatedBy = g.CreatedBy,
            SyncStatus = g.SyncStatus,
            SyncNumber = g.SyncNumber,
            TotalAmount = g.TotalAmount,
            TotalPayment = g.TotalPayment,
            PaymentMethod = g.PaymentMethod,
            Status = g.Status,
            BranchId = g.BranchId,
            CreatedAt = g.CreatedAt
        };

        if (loadLines)
        {
            dto.Lines = (g.Items ?? new List<GoodsIssueItem>())
                .OrderBy(i => i.LineNumber)
                .Select(i => new GoodsIssueItemResponseDto
                {
                    GoodsIssueItemId = i.GoodsIssueItemId,
                    LineNumber = i.LineNumber,
                    ProductId = i.ProductId,
                    ProductCode = i.ProductCode,
                    ProductName = i.ProductName,
                    Unit = i.Unit,
                    LotNumber = i.LotNumber,
                    ExpiryDate = i.ExpiryDate,
                    WarehouseId = i.WarehouseId,
                    WarehouseName = i.WarehouseName,
                    Location = i.Location,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalAmount = i.TotalAmount
                }).ToList();

            dto.Attachments = (g.Attachments ?? new List<GoodsIssueAttachment>())
                .OrderByDescending(a => a.UploadedAt)
                .Select(a => new GoodsIssueAttachmentResponseDto
                {
                    GoodsIssueAttachmentId = a.GoodsIssueAttachmentId,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    Size = a.Size,
                    MediaUrl = a.MediaUrl,
                    UploadedAt = a.UploadedAt
                }).ToList();
        }

        return dto;
    }
}