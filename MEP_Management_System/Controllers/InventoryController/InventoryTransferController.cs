//using System;
//using System.Linq;
//using System.Security.Claims;
//using System.Threading.Tasks;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.SignalR;
//using Microsoft.EntityFrameworkCore;
//using MEP_Management_System.DTOs;
//using MEP_Management_System.DTOs.InventoryDTOs;
//using MEP_Management_System.Hubs;
//using MEP_Management_System.Models;

//namespace MEP_Management_System.Controllers.InventoryController;

//[Authorize]
//[ApiController]
//[Route("api/[controller]")]
//[Tags("Quản lý Kho (Inventory)")]
//public class InventoryTransferController : ControllerBase
//{
//    private readonly MEPDatabaseContext _context;
//    private readonly IHubContext<InventoryHub> _hub;

//    public InventoryTransferController(MEPDatabaseContext context, IHubContext<InventoryHub> hub)
//    {
//        _context = context;
//        _hub = hub;
//    }

//    private Guid? GetUserId()
//    {
//        var userClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
//        return Guid.TryParse(userClaim, out var id) ? id : null;
//    }

//    [HttpPost]
//    public async Task<IActionResult> CreateTransfer([FromBody] CreateInventoryTransferDto dto)
//    {
//        try
//        {
//            if (dto == null || dto.Details == null || !dto.Details.Any())
//                return BadRequest(new ApiResponse<object> { Success = false, Message = "Dữ liệu không hợp lệ.", Errors = new() { "Details required" } });

//            if (dto.FromBranchId == dto.ToBranchId)
//                return BadRequest(new ApiResponse<object> { Success = false, Message = "Không thể chuyển cùng kho.", Errors = new() { "FromBranchId and ToBranchId must differ." } });

//            var userId = GetUserId();
//            if (userId == null) return Unauthorized(new ApiResponse<object> { Success = false, Message = "User not found in token." });

//            var transfer = new InventoryTransferTicket
//            {
//                TransferId = Guid.NewGuid(),
//                TransferCode = await GenerateTransferCodeAsync(),
//                FromBranchId = dto.FromBranchId,
//                ToBranchId = dto.ToBranchId,
//                Status = InventoryTransferStatus.Pending,
//                CreatedByUserId = userId.Value,
//                CreatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
//                Notes = dto.Notes
//            };

//            foreach (var d in dto.Details)
//            {
//                if (d.TransferQuantity <= 0)
//                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Số lượng chuyển phải > 0." });

//                transfer.Details.Add(new InventoryTransferDetail
//                {
//                    DetailId = Guid.NewGuid(),
//                    ProductId = d.ProductId,
//                    TransferQuantity = d.TransferQuantity
//                });
//            }

//            await _context.InventoryTransferTickets.AddAsync(transfer);
//            await _context.SaveChangesAsync();

//            return CreatedAtAction(nameof(GetTransferById), new { id = transfer.TransferId }, new ApiResponse<InventoryTransferResponseDto>
//            {
//                Success = true,
//                Message = "Phiếu điều chuyển đã được tạo.",
//                Data = MapToDto(transfer)
//            });
//        }
//        catch (Exception ex)
//        {
//            return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi máy chủ khi tạo phiếu.", Errors = new() { ex.Message } });
//        }
//    }

//    [HttpPost("{id:guid}/ship")]
//    public async Task<IActionResult> ShipTransfer(Guid id)
//    {
//        using var tx = await _context.Database.BeginTransactionAsync();
//        try
//        {
//            var transfer = await _context.InventoryTransferTickets
//                .Include(t => t.Details)
//                .ThenInclude(d => d.Product)
//                .FirstOrDefaultAsync(t => t.TransferId == id);

//            if (transfer == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Phiếu không tồn tại." });
//            if (transfer.Status != InventoryTransferStatus.Pending)
//                return BadRequest(new ApiResponse<object> { Success = false, Message = "Chỉ có phiếu Pending mới có thể xuất." });

//            var productIds = transfer.Details.Select(d => d.ProductId).Distinct().ToList();
//            var fromBranchProducts = await _context.BranchProducts
//                .Where(bp => bp.BranchId == transfer.FromBranchId && productIds.Contains(bp.ProductId))
//                .ToDictionaryAsync(bp => bp.ProductId);

//            foreach (var detail in transfer.Details)
//            {
//                if (!fromBranchProducts.TryGetValue(detail.ProductId, out var bp))
//                {
//                    return BadRequest(new ApiResponse<object> { Success = false, Message = "Dữ liệu tồn kho không đầy đủ.", Errors = new() { $"Không tìm thấy tồn kho cho sản phẩm {detail.ProductId} tại kho xuất." } });
//                }

//                if (bp.ActualStock < detail.TransferQuantity)
//                {
//                    var productName = detail.Product?.ProductName ?? detail.ProductId.ToString();
//                    return BadRequest(new ApiResponse<object> { Success = false, Message = $"Sản phẩm {productName} không đủ tồn kho thực tế để xuất" });
//                }
//            }

//            // All checks passed: deduct stocks and set status to Shipping
//            foreach (var detail in transfer.Details)
//            {
//                var bp = fromBranchProducts[detail.ProductId];
//                bp.ActualStock = bp.ActualStock - detail.TransferQuantity;
//                bp.AvailableStock = bp.AvailableStock - detail.TransferQuantity;

//                // Send SignalR update for FromBranch
//                await _hub.Clients.Group($"Branch_{transfer.FromBranchId}").SendAsync("UpdateStock", new
//                {
//                    BranchId = transfer.FromBranchId,
//                    ProductId = detail.ProductId,
//                    ActualStock = bp.ActualStock
//                });
//            }

//            transfer.Status = InventoryTransferStatus.Shipping;
//            await _context.SaveChangesAsync();
//            await tx.CommitAsync();

//            return Ok(new ApiResponse<InventoryTransferResponseDto> { Success = true, Message = "Hàng đã xuất, trạng thái Shipping.", Data = MapToDto(transfer) });
//        }
//        catch (Exception ex)
//        {
//            await tx.RollbackAsync();
//            return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi khi xuất kho.", Errors = new() { ex.Message } });
//        }
//    }

//    [HttpPost("{id:guid}/receive")]
//    public async Task<IActionResult> ReceiveTransfer(Guid id)
//    {
//        using var tx = await _context.Database.BeginTransactionAsync();
//        try
//        {
//            var transfer = await _context.InventoryTransferTickets
//                .Include(t => t.Details)
//                .ThenInclude(d => d.Product)
//                .FirstOrDefaultAsync(t => t.TransferId == id);

//            if (transfer == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Phiếu không tồn tại." });
//            if (transfer.Status != InventoryTransferStatus.Shipping)
//                return BadRequest(new ApiResponse<object> { Success = false, Message = "Chỉ có phiếu Shipping mới có thể nhận hàng." });

//            var productIds = transfer.Details.Select(d => d.ProductId).Distinct().ToList();
//            var toBranchProducts = await _context.BranchProducts
//                .Where(bp => bp.BranchId == transfer.ToBranchId && productIds.Contains(bp.ProductId))
//                .ToDictionaryAsync(bp => bp.ProductId);

//            foreach (var detail in transfer.Details)
//            {
//                if (!toBranchProducts.TryGetValue(detail.ProductId, out var bp))
//                {
//                    // create a new BranchProduct entry if not exist
//                    var newBp = new BranchProduct
//                    {
//                        BranchProductId = Guid.NewGuid(),
//                        BranchId = transfer.ToBranchId,
//                        ProductId = detail.ProductId,
//                        ActualStock = detail.TransferQuantity,
//                        AvailableStock = detail.TransferQuantity,
//                    };
//                    await _context.BranchProducts.AddAsync(newBp);
//                    toBranchProducts[detail.ProductId] = newBp;
//                }
//                else
//                {
//                    bp.ActualStock = bp.ActualStock + detail.TransferQuantity;
//                    bp.AvailableStock = bp.AvailableStock + detail.TransferQuantity;
//                }

//                // SignalR update for ToBranch
//                var updated = toBranchProducts[detail.ProductId];
//                await _hub.Clients.Group($"Branch_{transfer.ToBranchId}").SendAsync("UpdateStock", new
//                {
//                    BranchId = transfer.ToBranchId,
//                    ProductId = detail.ProductId,
//                    ActualStock = updated.ActualStock
//                });
//            }

//            transfer.Status = InventoryTransferStatus.Completed;
//            await _context.SaveChangesAsync();
//            await tx.CommitAsync();

//            return Ok(new ApiResponse<InventoryTransferResponseDto> { Success = true, Message = "Đã nhận hàng và hoàn tất.", Data = MapToDto(transfer) });
//        }
//        catch (Exception ex)
//        {
//            await tx.RollbackAsync();
//            return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi khi nhận hàng.", Errors = new() { ex.Message } });
//        }
//    }

//    [HttpPost("{id:guid}/cancel")]
//    public async Task<IActionResult> CancelTransfer(Guid id)
//    {
//        try
//        {
//            var transfer = await _context.InventoryTransferTickets.FindAsync(id);
//            if (transfer == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Phiếu không tồn tại." });
//            if (transfer.Status != InventoryTransferStatus.Pending)
//                return BadRequest(new ApiResponse<object> { Success = false, Message = "Chỉ có phiếu Pending mới có thể hủy." });

//            transfer.Status = InventoryTransferStatus.Cancelled;
//            await _context.SaveChangesAsync();

//            return Ok(new ApiResponse<object> { Success = true, Message = "Phiếu đã được hủy." });
//        }
//        catch (Exception ex)
//        {
//            return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi khi hủy phiếu.", Errors = new() { ex.Message } });
//        }
//    }

//    [HttpGet]
//    public async Task<IActionResult> GetTransfers([FromQuery] Guid? fromBranchId, [FromQuery] Guid? toBranchId, [FromQuery] string? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
//    {
//        try
//        {
//            if (pageNumber < 1 || pageSize < 1 || pageSize > 500)
//                return BadRequest(new ApiResponse<object> { Success = false, Message = "Invalid pagination parameters." });

//            var query = _context.InventoryTransferTickets.AsQueryable();
//            if (fromBranchId.HasValue) query = query.Where(t => t.FromBranchId == fromBranchId.Value);
//            if (toBranchId.HasValue) query = query.Where(t => t.ToBranchId == toBranchId.Value);
//            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InventoryTransferStatus>(status, true, out var st)) query = query.Where(t => t.Status == st);

//            var total = await query.CountAsync();
//            var items = await query.OrderByDescending(t => t.CreatedAt).Skip((pageNumber - 1) * pageSize).Take(pageSize)
//                .Select(t => new InventoryTransferListItemDto
//                {
//                    TransferId = t.TransferId,
//                    TransferCode = t.TransferCode,
//                    FromBranchId = t.FromBranchId,
//                    ToBranchId = t.ToBranchId,
//                    Status = t.Status.ToString(),
//                    DetailCount = t.Details.Count,
//                    CreatedAt = t.CreatedAt
//                }).ToListAsync();

//            var paged = new PaginatedResponse<InventoryTransferListItemDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize, TotalPages = (int)Math.Ceiling(total / (double)pageSize) };
//            return Ok(new ApiResponse<PaginatedResponse<InventoryTransferListItemDto>> { Success = true, Message = "Danh sách phiếu điều chuyển.", Data = paged });
//        }
//        catch (Exception ex)
//        {
//            return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi khi tải danh sách.", Errors = new() { ex.Message } });
//        }
//    }

//    [HttpGet("{id:guid}")]
//    public async Task<IActionResult> GetTransferById(Guid id)
//    {
//        try
//        {
//            var transfer = await _context.InventoryTransferTickets.Include(t => t.Details).ThenInclude(d => d.Product).FirstOrDefaultAsync(t => t.TransferId == id);
//            if (transfer == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Phiếu không tồn tại." });
//            return Ok(new ApiResponse<InventoryTransferResponseDto> { Success = true, Message = "Chi tiết phiếu.", Data = MapToDto(transfer) });
//        }
//        catch (Exception ex)
//        {
//            return StatusCode(500, new ApiResponse<object> { Success = false, Message = "Lỗi khi tải chi tiết.", Errors = new() { ex.Message } });
//        }
//    }

//    private InventoryTransferResponseDto MapToDto(InventoryTransferTicket t)
//    {
//        return new InventoryTransferResponseDto
//        {
//            TransferId = t.TransferId,
//            TransferCode = t.TransferCode,
//            FromBranchId = t.FromBranchId,
//            ToBranchId = t.ToBranchId,
//            Status = t.Status.ToString(),
//            CreatedByUserId = t.CreatedByUserId,
//            CreatedAt = t.CreatedAt,
//            Notes = t.Notes,
//            Details = t.Details.Select(d => new InventoryTransferDetailDto { DetailId = d.DetailId, ProductId = d.ProductId, ProductCode = d.Product?.ProductCode, ProductName = d.Product?.ProductName, TransferQuantity = d.TransferQuantity }).ToList()
//        };
//    }

//    private async Task<string> GenerateTransferCodeAsync()
//    {
//        const string prefix = "DC";
//        var last = await _context.InventoryTransferTickets.Where(t => t.TransferCode.StartsWith(prefix)).OrderByDescending(t => t.TransferCode).Select(t => t.TransferCode).FirstOrDefaultAsync();
//        var next = 1;
//        if (!string.IsNullOrWhiteSpace(last) && last.Length > prefix.Length)
//        {
//            var num = last[prefix.Length..];
//            if (int.TryParse(num, out var n)) next = n + 1;
//        }
//        return prefix + next.ToString("D5");
//    }
//}
