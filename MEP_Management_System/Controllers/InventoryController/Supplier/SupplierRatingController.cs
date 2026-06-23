using System.Security.Claims;
using MEP_Management_System.DTOs;
using MEP_Management_System.DTOs.InventoryDTOs;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Controllers.InventoryController.Supplier
{
    [Authorize]
    [ApiController]
    [Route("api/supplierratings")]
    [Tags("Quản lý nhà cung cấp (Inventory)")]
    public class SupplierRatingController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;

        public SupplierRatingController(MEPDatabaseContext context)
        {
            _context = context;
        }

        private static string ToCode(Guid id) => id.ToString()[..8].ToUpper();

        private Guid? GetUserId()
        {
            var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value
                  ?? User.FindFirst("userId")?.Value;
            return Guid.TryParse(id, out Guid u) ? u : null;
        }

        // Helper: tính điểm tổng (trung bình 4 tiêu chí) - 2 chữ số thập phân
        private static decimal ComputeOverall(short q, short d, short p, short s)
            => Math.Round((decimal)(q + d + p + s) / 4m, 2);

        // Helper: cập nhật cache AverageRating/RatingCount/LastRatedAt trên Supplier
        private async Task RefreshSupplierRatingCache(Guid supplierId)
        {
            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == supplierId);
            if (supplier == null) return;

            var stats = await _context.SupplierRatings
                .Where(r => r.SupplierId == supplierId)
                .GroupBy(r => r.SupplierId)
                .Select(g => new
                {
                    Avg = g.Average(r => r.OverallRating),
                    Count = g.Count(),
                    Last = g.Max(r => r.RatedAt)
                })
                .FirstOrDefaultAsync();

            if (stats == null || stats.Count == 0)
            {
                supplier.AverageRating = null;
                supplier.RatingCount = 0;
                supplier.LastRatedAt = null;
            }
            else
            {
                supplier.AverageRating = Math.Round(stats.Avg, 2);
                supplier.RatingCount = stats.Count;
                supplier.LastRatedAt = stats.Last;
            }
            await _context.SaveChangesAsync();
        }

        // ============================================================
        // GET /api/supplierratings
        // Lấy danh sách lượt đánh giá có phân trang + filter.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> GetRatings([FromQuery] SupplierRatingQueryParameters p)
        {
            try
            {
                var query = _context.SupplierRatings
                    .Include(r => r.Supplier)
                    .Include(r => r.PurchaseOrder)
                    .Include(r => r.RatedByUser)
                    .AsQueryable();

                if (p.SupplierId.HasValue)
                    query = query.Where(r => r.SupplierId == p.SupplierId.Value);

                if (p.MinRating.HasValue)
                    query = query.Where(r => r.OverallRating >= p.MinRating.Value);

                if (p.FromDate.HasValue)
                    query = query.Where(r => r.RatedAt >= p.FromDate.Value);

                if (p.ToDate.HasValue)
                {
                    var to = p.ToDate.Value.Date.AddDays(1);
                    query = query.Where(r => r.RatedAt < to);
                }

                if (!string.IsNullOrWhiteSpace(p.SearchTerm))
                {
                    var term = p.SearchTerm.Trim().ToLower();
                    query = query.Where(r =>
                        r.Supplier.SupplierName.ToLower().Contains(term) ||
                        (r.Comment != null && r.Comment.ToLower().Contains(term)));
                }

                var ratings = await query
                    .OrderByDescending(r => r.RatedAt)
                    .ToListAsync();

                var items = ratings.Select(r => new SupplierRatingListItemDto
                {
                    Id = r.RatingId,
                    SupplierId = r.SupplierId,
                    SupplierCode = ToCode(r.SupplierId),
                    SupplierName = r.Supplier?.SupplierName ?? "",
                    PurchaseOrderId = r.PurchaseOrderId,
                    PurchaseOrderCode = r.PurchaseOrder?.OrderCode,
                    QualityScore = r.QualityScore,
                    DeliveryScore = r.DeliveryScore,
                    PriceScore = r.PriceScore,
                    ServiceScore = r.ServiceScore,
                    OverallRating = r.OverallRating,
                    Comment = r.Comment,
                    RatedBy = r.RatedBy,
                    RatedByName = r.RatedByUser?.FullName,
                    RatedAt = r.RatedAt,
                }).ToList();

                var totalCount = items.Count;
                var totalPages = (int)Math.Ceiling(totalCount / (double)p.PageSize);
                var paged = items
                    .Skip((p.PageNumber - 1) * p.PageSize)
                    .Take(p.PageSize)
                    .ToList();

                return Ok(new ApiResponse<SupplierRatingPagedResponse>
                {
                    Success = true,
                    Message = "Supplier ratings retrieved successfully",
                    Data = new SupplierRatingPagedResponse
                    {
                        Items = paged,
                        TotalCount = totalCount,
                        PageNumber = p.PageNumber,
                        PageSize = p.PageSize,
                        TotalPages = totalPages,
                        HasNextPage = p.PageNumber < totalPages,
                        HasPreviousPage = p.PageNumber > 1,
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

        // ============================================================
        // GET /api/supplierratings/{id}
        // ============================================================
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetRating(Guid id)
        {
            try
            {
                var r = await _context.SupplierRatings
                    .Include(x => x.Supplier)
                    .Include(x => x.PurchaseOrder)
                    .Include(x => x.RatedByUser)
                    .FirstOrDefaultAsync(x => x.RatingId == id);

                if (r == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Rating not found"
                    });

                return Ok(new ApiResponse<SupplierRatingDetailDto>
                {
                    Success = true,
                    Message = "Rating retrieved successfully",
                    Data = new SupplierRatingDetailDto
                    {
                        Id = r.RatingId,
                        SupplierId = r.SupplierId,
                        SupplierName = r.Supplier?.SupplierName ?? "",
                        PurchaseOrderId = r.PurchaseOrderId,
                        PurchaseOrderCode = r.PurchaseOrder?.OrderCode,
                        PurchaseOrderTotal = r.PurchaseOrder?.TotalAmount,
                        QualityScore = r.QualityScore,
                        DeliveryScore = r.DeliveryScore,
                        PriceScore = r.PriceScore,
                        ServiceScore = r.ServiceScore,
                        OverallRating = r.OverallRating,
                        Comment = r.Comment,
                        RatedBy = r.RatedBy,
                        RatedByName = r.RatedByUser?.FullName,
                        RatedAt = r.RatedAt,
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

        // ============================================================
        // GET /api/supplierratings/bysupplier/{supplierId}
        // Lịch sử đánh giá của 1 NCC.
        // ============================================================
        [HttpGet("bysupplier/{supplierId:guid}")]
        public async Task<IActionResult> GetRatingsBySupplier(Guid supplierId)
        {
            try
            {
                var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == supplierId);
                if (supplier == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Supplier not found"
                    });

                var ratings = await _context.SupplierRatings
                    .Include(r => r.PurchaseOrder)
                    .Include(r => r.RatedByUser)
                    .Where(r => r.SupplierId == supplierId)
                    .OrderByDescending(r => r.RatedAt)
                    .ToListAsync();

                var items = ratings.Select(r => new SupplierRatingListItemDto
                {
                    Id = r.RatingId,
                    SupplierId = r.SupplierId,
                    SupplierCode = ToCode(r.SupplierId),
                    SupplierName = supplier.SupplierName,
                    PurchaseOrderId = r.PurchaseOrderId,
                    PurchaseOrderCode = r.PurchaseOrder?.OrderCode,
                    QualityScore = r.QualityScore,
                    DeliveryScore = r.DeliveryScore,
                    PriceScore = r.PriceScore,
                    ServiceScore = r.ServiceScore,
                    OverallRating = r.OverallRating,
                    Comment = r.Comment,
                    RatedBy = r.RatedBy,
                    RatedByName = r.RatedByUser?.FullName,
                    RatedAt = r.RatedAt,
                }).ToList();

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Supplier rating history retrieved successfully",
                    Data = new
                    {
                        SupplierId = supplier.SupplierId,
                        SupplierName = supplier.SupplierName,
                        AverageRating = supplier.AverageRating,
                        RatingCount = supplier.RatingCount ?? 0,
                        LastRatedAt = supplier.LastRatedAt,
                        Items = items,
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

        // ============================================================
        // GET /api/supplierratings/summary
        // KPI + Top/Bottom NCC (cho dashboard).
        // ============================================================
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            try
            {
                var allRatings = await _context.SupplierRatings.ToListAsync();
                var total = allRatings.Count;
                var avg = total > 0 ? Math.Round(allRatings.Average(r => r.OverallRating), 2) : 0m;

                var ratedSuppliers = await _context.Suppliers
                    .Where(s => s.RatingCount != null && s.RatingCount > 0)
                    .ToListAsync();

                var top = ratedSuppliers
                    .OrderByDescending(s => s.AverageRating)
                    .ThenByDescending(s => s.RatingCount)
                    .Take(5)
                    .Select(s => new SupplierRatingTopDto
                    {
                        SupplierId = s.SupplierId,
                        SupplierName = s.SupplierName,
                        GroupName = s.GroupName,
                        AverageRating = s.AverageRating ?? 0,
                        RatingCount = s.RatingCount ?? 0,
                        LastRatedAt = s.LastRatedAt,
                    }).ToList();

                var bottom = ratedSuppliers
                    .OrderBy(s => s.AverageRating)
                    .ThenBy(s => s.RatingCount)
                    .Take(5)
                    .Select(s => new SupplierRatingTopDto
                    {
                        SupplierId = s.SupplierId,
                        SupplierName = s.SupplierName,
                        GroupName = s.GroupName,
                        AverageRating = s.AverageRating ?? 0,
                        RatingCount = s.RatingCount ?? 0,
                        LastRatedAt = s.LastRatedAt,
                    }).ToList();

                return Ok(new ApiResponse<SupplierRatingSummaryDto>
                {
                    Success = true,
                    Message = "Summary retrieved successfully",
                    Data = new SupplierRatingSummaryDto
                    {
                        TotalRatings = total,
                        AverageOverallRating = avg,
                        RatedSupplierCount = ratedSuppliers.Count,
                        TopSuppliers = top,
                        BottomSuppliers = bottom,
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

        // ============================================================
        // POST /api/supplierratings
        // ============================================================
        [HttpPost]
        public async Task<IActionResult> CreateRating([FromBody] SupplierRatingUpsertRequest req)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == req.SupplierId);
                if (supplier == null)
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Supplier not found",
                        Errors = new() { $"No supplier with ID: {req.SupplierId}" }
                    });

                if (req.PurchaseOrderId.HasValue)
                {
                    var po = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.PurchaseOrderId == req.PurchaseOrderId.Value);
                    if (po == null)
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Purchase order not found"
                        });
                    if (po.SupplierId != req.SupplierId)
                        return BadRequest(new ApiResponse<object>
                        {
                            Success = false,
                            Message = "Purchase order does not belong to this supplier"
                        });
                }

                var r = new SupplierRating
                {
                    RatingId = Guid.NewGuid(),
                    SupplierId = req.SupplierId,
                    PurchaseOrderId = req.PurchaseOrderId,
                    QualityScore = req.QualityScore,
                    DeliveryScore = req.DeliveryScore,
                    PriceScore = req.PriceScore,
                    ServiceScore = req.ServiceScore,
                    OverallRating = ComputeOverall(req.QualityScore, req.DeliveryScore, req.PriceScore, req.ServiceScore),
                    Comment = req.Comment?.Trim(),
                    RatedBy = GetUserId(),
                    RatedAt = DateTime.UtcNow,
                };

                _context.SupplierRatings.Add(r);
                await _context.SaveChangesAsync();

                // Cập nhật cache trên Supplier
                await RefreshSupplierRatingCache(req.SupplierId);

                return Ok(new ApiResponse<SupplierRatingDetailDto>
                {
                    Success = true,
                    Message = "Rating created successfully",
                    Data = new SupplierRatingDetailDto
                    {
                        Id = r.RatingId,
                        SupplierId = r.SupplierId,
                        SupplierName = supplier.SupplierName,
                        PurchaseOrderId = r.PurchaseOrderId,
                        QualityScore = r.QualityScore,
                        DeliveryScore = r.DeliveryScore,
                        PriceScore = r.PriceScore,
                        ServiceScore = r.ServiceScore,
                        OverallRating = r.OverallRating,
                        Comment = r.Comment,
                        RatedBy = r.RatedBy,
                        RatedAt = r.RatedAt,
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

        // ============================================================
        // PUT /api/supplierratings/{id}
        // Sửa đánh giá - cập nhật lại cache.
        // ============================================================
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateRating(Guid id, [FromBody] SupplierRatingUpsertRequest req)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Validation failed",
                        Errors = errors
                    });
                }

                var r = await _context.SupplierRatings.FirstOrDefaultAsync(x => x.RatingId == id);
                if (r == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Rating not found"
                    });

                r.QualityScore = req.QualityScore;
                r.DeliveryScore = req.DeliveryScore;
                r.PriceScore = req.PriceScore;
                r.ServiceScore = req.ServiceScore;
                r.OverallRating = ComputeOverall(req.QualityScore, req.DeliveryScore, req.PriceScore, req.ServiceScore);
                r.Comment = req.Comment?.Trim();
                if (req.PurchaseOrderId.HasValue) r.PurchaseOrderId = req.PurchaseOrderId;

                await _context.SaveChangesAsync();
                await RefreshSupplierRatingCache(r.SupplierId);

                return Ok(new ApiResponse<SupplierRatingDetailDto>
                {
                    Success = true,
                    Message = "Rating updated successfully",
                    Data = new SupplierRatingDetailDto
                    {
                        Id = r.RatingId,
                        SupplierId = r.SupplierId,
                        PurchaseOrderId = r.PurchaseOrderId,
                        QualityScore = r.QualityScore,
                        DeliveryScore = r.DeliveryScore,
                        PriceScore = r.PriceScore,
                        ServiceScore = r.ServiceScore,
                        OverallRating = r.OverallRating,
                        Comment = r.Comment,
                        RatedBy = r.RatedBy,
                        RatedAt = r.RatedAt,
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

        // ============================================================
        // DELETE /api/supplierratings/{id}
        // Xóa 1 lượt đánh giá (chỉ người tạo hoặc admin).
        // ============================================================
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteRating(Guid id)
        {
            try
            {
                var r = await _context.SupplierRatings.FirstOrDefaultAsync(x => x.RatingId == id);
                if (r == null)
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Rating not found"
                    });

                var supplierId = r.SupplierId;
                _context.SupplierRatings.Remove(r);
                await _context.SaveChangesAsync();
                await RefreshSupplierRatingCache(supplierId);

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Rating deleted successfully"
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
    }
}
