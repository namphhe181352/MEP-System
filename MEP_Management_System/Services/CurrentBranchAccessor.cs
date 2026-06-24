using System;
using System.Linq;
using System.Security.Claims;
using MEP_Management_System.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MEP_Management_System.Services
{
 /// <summary>
 /// Helper truy xuất BranchId linh hoạt, theo thứ tự ưu tiên:
 /// 1. Claim "branchId" / ClaimTypes.GroupSid trong JWT.
 /// 2. Nếu user là Owner và không có claim:
 /// - Header "X-Branch-Id"
 /// - Query string "branchId"
 /// 3. Fallback cuối cùng: đọc users.default_branch_id trong DB
 /// (áp dụng cho mọi user có default_branch_id, không riêng Owner).
 ///
 /// FE có thể truyền: GET /api/products?branchId=&lt;guid&gt;
 /// hoặc header X-Branch-Id: &lt;guid&gt;
 /// </summary>
 public interface ICurrentBranchAccessor
 {
 Guid? GetBranchId();
 }

 public class CurrentBranchAccessor : ICurrentBranchAccessor
 {
 private readonly IHttpContextAccessor _httpContextAccessor;
 private readonly MEPDatabaseContext _context;

 public CurrentBranchAccessor(IHttpContextAccessor httpContextAccessor, MEPDatabaseContext context)
 {
 _httpContextAccessor = httpContextAccessor;
 _context = context;
 }

 public Guid? GetBranchId()
 {
 var httpContext = _httpContextAccessor.HttpContext;
 var user = httpContext?.User;
 if (user == null) return null;

 // 1. Ưu tiên claim trong JWT
 var branchIdClaim = user.FindFirst("branchId")?.Value
 ?? user.FindFirst(ClaimTypes.GroupSid)?.Value;
 if (Guid.TryParse(branchIdClaim, out Guid branchIdFromClaim))
 return branchIdFromClaim;

 // 2. Owner fallback: header hoặc query string
 if (user.IsInRole("Owner"))
 {
 var request = httpContext?.Request;
 if (request != null)
 {
 var headerBranchId = request.Headers["X-Branch-Id"].FirstOrDefault();
 if (Guid.TryParse(headerBranchId, out Guid hb)) return hb;

 var queryBranchId = request.Query["branchId"].FirstOrDefault();
 if (Guid.TryParse(queryBranchId, out Guid qb)) return qb;
 }
 }

 // 3. Fallback cuối cùng: đọc default_branch_id từ DB
 var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
 if (Guid.TryParse(userIdClaim, out Guid userId))
 {
 var defaultBranchId = _context.Users
 .Where(u => u.UserId == userId)
 .Select(u => u.DefaultBranchId)
 .FirstOrDefault();

 if (defaultBranchId.HasValue) return defaultBranchId;
 }

 return null;
 }
 }
}