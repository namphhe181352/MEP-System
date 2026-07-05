using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MEP_Management_System.Models;
using System;
using System.Threading.Tasks;

namespace MEP_Management_System.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FixPermController : ControllerBase
    {
        private readonly MEPDatabaseContext _context;

        public FixPermController(MEPDatabaseContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> FixPermissions()
        {
            var roleId = Guid.Parse("10000000-0000-0000-0000-000000000004"); // InventoryStaff

            // Delete old
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM role_permissions WHERE role_id = {0}", roleId);

            // Insert new correct permissions for InventoryStaff
            // 7: PRODUCT_VIEW, 8: PRODUCT_CREATE, 10: PRODUCT_DELETE
            // 11: STOCK_VIEW, 12: STOCK_INWARD_CREATE, 15: STOCK_OUTWARD_CREATE
            // 18: STOCK_CHECK_VIEW, 19: STOCK_CHECK_CREATE
            var perms = new[] { 7, 8, 10, 11, 12, 15, 18, 19 };

            foreach (var p in perms)
            {
                var permId = Guid.Parse($"00000000-0000-0000-0000-0000000000{p:D2}");
                await _context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO role_permissions (role_id, permission_id) VALUES ({0}, {1})",
                    roleId, permId);
            }

            await _context.SaveChangesAsync();

            return Ok("Fixed InventoryStaff permissions successfully!");
        }
    }
}
