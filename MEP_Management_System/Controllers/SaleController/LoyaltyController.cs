using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.Models;
using MEP_Management_System.DTOs.SaleDTOs;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/customers/{customerId:guid}/points")]
[Authorize(Roles = "SalesStaff,Owner")]
[Tags("Quản lý bán hàng (Sale)")]
public class LoyaltyController : ControllerBase
{
    private readonly Models.MEPDatabaseContext _context;
    private readonly IMapper _mapper;

    public LoyaltyController(Models.MEPDatabaseContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    // GET /api/pos/customers/{customerId}/points
    [HttpGet]
    public async Task<ActionResult<LoyaltyPointsDto>> GetPoints(Guid customerId)
    {
        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null) return NotFound(ApiErrorResponse.Create("MSG-01", "Customer not found"));

        var recentTxns = await _context.LoyaltyTransactions
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .ToListAsync();

        return Ok(new LoyaltyPointsDto
        {
            CustomerId = customerId,
            CurrentPoints = customer.LoyaltyPoints ?? 0,
            MembershipTier = CalculateTier(customer.TotalSpent),
            TotalSpent = customer.TotalSpent,
            OrderCount = customer.OrderCount,
            LastVisit = customer.LastVisit,
            RecentTransactions = recentTxns.Select(_mapper.Map<LoyaltyPointsDto.LoyaltyHistoryDto>).ToList()
        });
    }

    private static string? CalculateTier(decimal? totalSpent)
    {
        if (!totalSpent.HasValue) return "Bronze";
        if (totalSpent >= 50000000) return "Platinum";
        if (totalSpent >= 20000000) return "Gold";
        if (totalSpent >= 5000000) return "Silver";
        return "Bronze";
    }
}
