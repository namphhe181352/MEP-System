using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.Services.Chatbot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace MEP_Management_System.Controllers.Chatbot;

[ApiController]
[Route("api/chatbot")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly SqlExecutor _sqlExecutor;
    private readonly ILogger<ChatController> _logger;

    // Mặc định trỏ về Groq với model llama-3.1-8b-instant - free tier 14,400 request/ngày
    // và ~500,000 token/ngày, cao hơn nhiều so với llama-3.3-70b-versatile (1,000 req/ngày,
    // 100,000 token/ngày) đã bị hết quota trước đó.
    // Muốn đổi provider (Gemini/Groq/khác) chỉ cần sửa 3 key dưới trong appsettings.json,
    // KHÔNG cần sửa code:
    //   "AI_API_URL": "https://api.groq.com/openai/v1/chat/completions"
    //   "AI_API_KEY": "..."
    //   "AI_MODEL": "llama-3.1-8b-instant"
    // (Gemini OpenAI-compatible endpoint: https://generativelanguage.googleapis.com/v1beta/openai/chat/completions)
    private const string DefaultApiUrl = "https://api.groq.com/openai/v1/chat/completions";
    private const string DefaultModel = "llama-3.1-8b-instant";
    private const int MaxSqlIterations = 4;

    public ChatController(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        SqlExecutor sqlExecutor,
        ILogger<ChatController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _sqlExecutor = sqlExecutor;
        _logger = logger;
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    public class ChatResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Data { get; set; }
        public List<string>? Errors { get; set; }
    }

    [HttpPost("message")]
    public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new ChatResponse
            {
                Success = false,
                Message = "Yêu cầu không hợp lệ: message trống."
            });
        }

        // 🔒 Lấy branch_id của user đang đăng nhập từ claim trong JWT (do
        // JwtHelper.GenerateToken thêm vào với claim type "branchId").
        // Đây là chi nhánh DUY NHẤT mà chatbot được phép trả dữ liệu về, bất kể
        // AI viết SQL có filter branch_id hay không - việc enforce thật sự nằm
        // ở Row Level Security phía Postgres (xem SqlExecutor.ExecuteSelectAsync).
        var branchId = User.FindFirst("branchId")?.Value;

        if (string.IsNullOrWhiteSpace(branchId))
        {
            _logger.LogWarning(
                "User {User} gọi chatbot nhưng token không có claim branchId - từ chối truy vấn.",
                User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");

            return BadRequest(new ChatResponse
            {
                Success = false,
                Message = "Tài khoản chưa được gán chi nhánh, không thể sử dụng chatbot."
            });
        }

        var apiUrl = _config["AI_API_URL"] ?? DefaultApiUrl;
        var apiKey = _config["AI_API_KEY"] ?? _config.GetValue<string>("AI_API_KEY");
        var model = _config["AI_MODEL"] ?? DefaultModel;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return StatusCode(500, new ChatResponse
            {
                Success = false,
                Message = "AI_API_KEY chưa được cấu hình."
            });
        }

        try
        {
            var today = DateTime.Now;
            var systemPrompt = BuildSystemPrompt(today);

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            var result = await ProcessChatAsync(apiUrl, apiKey, model, messages, request.Message, branchId);

            return Ok(result);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling AI API");
            return StatusCode(503, new ChatResponse
            {
                Success = false,
                Message = "Lỗi kết nối tới AI API.",
                Errors = new List<string> { ex.Message }
            });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout calling AI API");
            return StatusCode(504, new ChatResponse
            {
                Success = false,
                Message = "Timeout khi gọi AI API. Vui lòng thử lại.",
                Errors = new List<string> { ex.Message }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in ChatController");
            return StatusCode(500, new ChatResponse
            {
                Success = false,
                Message = "Lỗi nội bộ khi xử lý yêu cầu chatbot.",
                Errors = new List<string> { ex.Message }
            });
        }
    }

    private async Task<ChatResponse> ProcessChatAsync(string apiUrl, string apiKey, string model, List<object> messages, string userMessage, string branchId)
    {
        var client = _httpClientFactory.CreateClient();
        var url = apiUrl;

        messages.Add(new { role = "user", content = userMessage });

        for (int iteration = 0; iteration < MaxSqlIterations; iteration++)
        {
            var aiRequestPayload = new
            {
                model = model,
                messages = messages,
                max_tokens = 2000,
                temperature = 0.3
            };

            var jsonPayload = JsonSerializer.Serialize(aiRequestPayload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            _logger.LogInformation("AI iteration {Iteration}: {Payload}", iteration + 1, jsonPayload);

            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var resp = await client.PostAsync(url, httpContent);
            var respText = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("AI API error: {Status} - {Body}", resp.StatusCode, respText);
                return new ChatResponse
                {
                    Success = false,
                    Message = "Lỗi khi gọi AI API.",
                    Errors = new List<string> { $"Status: {resp.StatusCode}", respText }
                };
            }

            var aiApiResponse = JsonSerializer.Deserialize<JsonElement>(respText);
            var choices = aiApiResponse.GetProperty("choices");

            if (choices.GetArrayLength() == 0)
            {
                return new ChatResponse
                {
                    Success = true,
                    Message = "OK",
                    Data = "Không có phản hồi từ AI."
                };
            }

            var aiResponse = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            messages.Add(new { role = "assistant", content = aiResponse });

            _logger.LogInformation("AI response (iteration {Iteration}): {Response}", iteration + 1, aiResponse);

            var (hasSql, sqlQuery) = ExtractSqlQuery(aiResponse);

            if (!hasSql)
            {
                return new ChatResponse
                {
                    Success = true,
                    Message = "OK",
                    Data = aiResponse
                };
            }

            _logger.LogInformation("Found SQL query: {Sql}", sqlQuery);

            // 🔒 Truyền branchId của user đang chat xuống SqlExecutor. Executor
            // sẽ set session variable app.current_branch_id trên connection
            // trước khi chạy SQL, để Row Level Security tự lọc đúng chi nhánh -
            // không phụ thuộc vào việc AI có nhớ filter branch_id hay không.
            var execResult = await _sqlExecutor.ExecuteQueryAsync(sqlQuery!, branchId);

            if (!execResult.success)
            {
                var errorMessage = $"❌ Query lỗi: {execResult.message}. Vui lòng kiểm tra lại SQL.";
                messages.Add(new { role = "user", content = errorMessage });
                continue;
            }

            var dataContext = execResult.data != null
                ? JsonSerializer.Serialize(execResult.data)
                : "Không có dữ liệu.";

            var dataMessage = $"✅ Đây là kết quả từ database:\n```json\n{dataContext}\n```\n\nHãy trình bày kết quả này cho user bằng tiếng Việt, dùng bảng markdown nếu cần.";
            messages.Add(new { role = "user", content = dataMessage });

            _logger.LogInformation("Sent data context to AI, continuing to final response...");
        }

        // Hết số vòng lặp SQL cho phép mà vẫn chưa có câu trả lời cuối cùng.
        // Thay vì trả về câu chung chung, ép AI trả lời thẳng bằng dữ liệu đã có
        // (không cho viết SQL nữa) để tránh vòng lặp vô ích.
        messages.Add(new
        {
            role = "user",
            content = "Dựa vào toàn bộ dữ liệu đã có ở trên, hãy trả lời câu hỏi của user ngay bây giờ bằng tiếng Việt. " +
                      "KHÔNG được viết thêm SQL query nữa. Nếu chưa đủ dữ liệu để trả lời chính xác, hãy nói rõ là hệ thống chưa hỗ trợ truy vấn này."
        });

        var finalPayload = new
        {
            model = model,
            messages = messages,
            max_tokens = 2000,
            temperature = 0.3
        };

        var finalJson = JsonSerializer.Serialize(finalPayload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var finalHttpContent = new StringContent(finalJson, Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var finalResp = await client.PostAsync(url, finalHttpContent);
        var finalRespText = await finalResp.Content.ReadAsStringAsync();

        if (!finalResp.IsSuccessStatusCode)
        {
            _logger.LogError("AI API error (final forced call): {Status} - {Body}", finalResp.StatusCode, finalRespText);
            return new ChatResponse
            {
                Success = false,
                Message = "Lỗi khi gọi AI API.",
                Errors = new List<string> { $"Status: {finalResp.StatusCode}", finalRespText }
            };
        }

        var finalAiApiResponse = JsonSerializer.Deserialize<JsonElement>(finalRespText);
        var finalChoices = finalAiApiResponse.GetProperty("choices");
        var finalAnswer = finalChoices.GetArrayLength() > 0
            ? finalChoices[0].GetProperty("message").GetProperty("content").GetString() ?? ""
            : "Dạ hiện tại em chưa xử lý được câu hỏi này ạ, anh/chị vui lòng hỏi lại theo cách khác.";

        return new ChatResponse
        {
            Success = true,
            Message = "OK",
            Data = finalAnswer
        };
    }

    private (bool hasSql, string? sqlQuery) ExtractSqlQuery(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return (false, null);

        var pattern = @"--QUERY_START--\s*(.*?)\s*--QUERY_END--";
        var match = System.Text.RegularExpressions.Regex.Match(response, pattern, System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return (true, match.Groups[1].Value.Trim());
        }

        if (response.Contains("```sql") || response.Contains("```SQL"))
        {
            var sqlBlockMatch = System.Text.RegularExpressions.Regex.Match(response, @"```sql\s*(.*?)\s*```", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (sqlBlockMatch.Success)
            {
                return (true, sqlBlockMatch.Groups[1].Value.Trim());
            }
        }

        return (false, null);
    }

    // FIX: Trước đây hàm này lỗi vì trộn chuỗi verbatim (@"...") với các đoạn
    // nối bằng " + bien + " nhưng KHÔNG đánh dấu @ cho đoạn nối lại, mà đoạn đó
    // lại chứa xuống dòng thật -> C# không cho phép newline trong chuỗi thường
    // => lỗi "expected }", "Unexpected character '`'", "Too many characters in
    // character literal" domino tiếp theo.
    // Sửa bằng cách dùng chuỗi nội suy verbatim $@"..." và {bien} thay vì " + bien + "
    private string BuildSystemPrompt(DateTime today)
    {
        var currentYear = today.Year;
        var currentMonth = today.Month;
        var firstDayOfMonth = new DateTime(currentYear, currentMonth, 1);
        var dayOfWeek = (int)today.DayOfWeek;
        var monday = today.AddDays(-(dayOfWeek == 0 ? 6 : dayOfWeek - 1));
        var todayStr = today.ToString("yyyy-MM-dd");
        var mondayStr = monday.ToString("yyyy-MM-dd");
        var firstDayOfMonthStr = firstDayOfMonth.ToString("yyyy-MM-dd");
        var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd");

        var prompt = $@"Bạn là trợ lý ảo cho hệ thống quản lý kho và bán hàng MEP Management System.

## NHIỆM VỤ CHÍNH
- Trả lời các câu hỏi về báo cáo, tồn kho, doanh thu, công nợ, hóa đơn, sản phẩm...
- Khi cần thông tin cụ thể, VIẾT SQL QUERY để lấy dữ liệu từ database.
- QUAN TRỌNG: Dùng format SQL query đặc biệt dưới đây.
- Database đã tự động giới hạn kết quả trong đúng chi nhánh của user đang hỏi
  (thông qua Row Level Security ở tầng PostgreSQL). Bạn KHÔNG cần và KHÔNG nên
  tự thêm điều kiện branch_id vào WHERE trừ khi user hỏi so sánh nhiều chi nhánh
  cụ thể bằng tên - vì bạn không biết branch_id thật, tự thêm vào dễ gây sai lệch.

## DATABASE SCHEMA (PostgreSQL) - CHỈ được dùng đúng các bảng/cột dưới đây, KHÔNG được tự bịa bảng/cột khác

- branches: branch_id, branch_name, address, branch_code, city, is_active, type
- branch_products: branch_product_id, branch_id, product_id, cost_price, sell_price, actual_stock, available_stock, reserved_stock, minimum_stock, maximum_stock, is_active
- products: product_id, product_code, product_name, barcode, base_unit, brand_name, category_name, supplier_id, status
- product_attributes: attribute_id, product_id, attribute_name, attribute_value
- product_conversion_units: conversion_id, product_id, unit_name, convert_value, price
- customers: customer_id, customer_name, phone_number, email, address, loyalty_points, debt_limit, current_debt, status, ""group"", membership_tier, order_count, total_spent
- orders: order_id, order_code, branch_id, customer_id, user_id, total_amount, discount_amount, tax_amount, payment_method, status, created_at
- order_items: order_item_id, order_id, branch_product_id, quantity, sell_price
  (LƯU Ý: order_items KHÔNG có product_id hay unit_price. Muốn lấy product_id/product_name/cost_price phải JOIN qua branch_products bằng branch_product_id)
- order_discount_tiers: order_discount_tier_id, min_order_value, discount_percent, is_active
- invoices: invoice_id, invoice_code, order_id, user_id, total_amount, sub_total_amount, discount_percent, discount_amount, payment_method, status, note, created_at
- invoice_items: invoice_item_id, invoice_id, branch_product_id, quantity, sell_price
- payments: payment_id, invoice_id, method, amount, status, cash_received, change_amount, paid_at
- refund_records: refund_id, refund_code, order_id, invoice_id, customer_id, user_id, refund_amount, refund_method, status, reason, created_at
- return_orders: return_order_id, return_code, order_id, user_id, refund_amount, refund_method, status, note, created_at, return_type
- return_items: return_item_id, return_order_id, branch_product_id, quantity, refund_amount, reason
- suppliers: supplier_id, supplier_name, contact_phone, contact_email, address, status, contact_person, tax_code, group_name, average_rating
- purchase_orders: purchase_order_id, order_code, supplier_id, user_id, total_amount, status, approved_by, approved_at, expected_delivery_date, created_at
- purchase_order_items: po_item_id, purchase_order_id, product_id, quantity, unit_price
- stock_tickets: stock_ticket_id, ticket_code, branch_id, user_id, ticket_type, status, notes, created_at, supplier_id, total_amount
- stock_ticket_items: ticket_item_id, stock_ticket_id, branch_product_id, system_quantity, actual_quantity, discrepancy
- inventory_check_tickets: ticket_id, ticket_code, created_at, status, branch_id
- inventory_check_details: detail_id, ticket_id, product_id, system_quantity, actual_quantity, discrepancy
- inventory_transfer_tickets: transfer_id, transfer_code, from_branch_id, to_branch_id, status, created_at
- inventory_transfer_details: detail_id, transfer_id, product_id, transfer_quantity
- expense_vouchers: voucher_id, voucher_code, branch_id, category_id, supplier_id, amount, reason, status, created_at
- expense_categories: category_id, category_name, is_active
- shifts: shift_id, shift_code, user_id, branch_id, status, started_at, ended_at, opening_balance, expected_cash, actual_cash, variance, total_revenue, total_orders
- staff_assignments: assignment_id, user_id, branch_id, assigned_at

## CÁCH VIẾT SQL QUERY
Khi cần lấy dữ liệu, VIẾT SQL query trong block:
```sql
--QUERY_START--
[SQL QUERY]
--QUERY_END--
```

## SQL MẪU:

1. Tìm sản phẩm:
```sql
--QUERY_START--
SELECT p.product_code, p.product_name, p.brand_name, bp.sell_price, bp.available_stock
FROM products p
LEFT JOIN branch_products bp ON p.product_id = bp.product_id
WHERE p.product_name ILIKE '%Aptomat%' OR p.product_code ILIKE '%Aptomat%'
LIMIT 20;
--QUERY_END--
```

2. Doanh thu hôm nay:
```sql
--QUERY_START--
SELECT COUNT(*) as total_orders, COALESCE(SUM(total_amount), 0) as total_revenue
FROM orders
WHERE DATE(created_at) = CURRENT_DATE AND status = 'Completed';
--QUERY_END--
```

3. Doanh thu tháng này:
```sql
--QUERY_START--
SELECT DATE(created_at) as date, COUNT(*) as orders, COALESCE(SUM(total_amount), 0) as revenue
FROM orders
WHERE created_at >= '{firstDayOfMonthStr}' AND status = 'Completed'
GROUP BY DATE(created_at) ORDER BY date;
--QUERY_END--
```

4. Tồn kho sắp hết:
```sql
--QUERY_START--
SELECT p.product_code, p.product_name, bp.available_stock, bp.minimum_stock
FROM products p
JOIN branch_products bp ON p.product_id = bp.product_id
WHERE bp.available_stock <= bp.minimum_stock
ORDER BY bp.available_stock ASC LIMIT 20;
--QUERY_END--
```

5. Sản phẩm bán chạy (order_items KHÔNG có product_id -> phải JOIN qua branch_products):
```sql
--QUERY_START--
SELECT p.product_code, p.product_name, SUM(oi.quantity) as total_sold
FROM order_items oi
JOIN orders o ON oi.order_id = o.order_id
JOIN branch_products bp ON oi.branch_product_id = bp.branch_product_id
JOIN products p ON bp.product_id = p.product_id
WHERE o.created_at >= '{firstDayOfMonthStr}' AND o.status = 'Completed'
GROUP BY p.product_id, p.product_code, p.product_name
ORDER BY total_sold DESC LIMIT 10;
--QUERY_END--
```

6. Lợi nhuận hôm nay (doanh thu - giá vốn, dùng sell_price của order_items và cost_price của branch_products qua branch_product_id):
```sql
--QUERY_START--
SELECT
    COALESCE(SUM(oi.quantity * oi.sell_price), 0) as total_revenue,
    COALESCE(SUM(oi.quantity * bp.cost_price), 0) as total_cost,
    COALESCE(SUM(oi.quantity * (oi.sell_price - bp.cost_price)), 0) as total_profit
FROM order_items oi
JOIN orders o ON oi.order_id = o.order_id
JOIN branch_products bp ON oi.branch_product_id = bp.branch_product_id
WHERE DATE(o.created_at) = CURRENT_DATE AND o.status = 'Completed';
--QUERY_END--
```

7. Lợi nhuận tháng này:
```sql
--QUERY_START--
SELECT
    COALESCE(SUM(oi.quantity * oi.sell_price), 0) as total_revenue,
    COALESCE(SUM(oi.quantity * bp.cost_price), 0) as total_cost,
    COALESCE(SUM(oi.quantity * (oi.sell_price - bp.cost_price)), 0) as total_profit
FROM order_items oi
JOIN orders o ON oi.order_id = o.order_id
JOIN branch_products bp ON oi.branch_product_id = bp.branch_product_id
WHERE o.created_at >= '{firstDayOfMonthStr}' AND o.status = 'Completed';
--QUERY_END--
```

8. Công nợ khách hàng cao nhất:
```sql
--QUERY_START--
SELECT customer_name, phone_number, current_debt, debt_limit
FROM customers
WHERE current_debt > 0
ORDER BY current_debt DESC LIMIT 10;
--QUERY_END--
```

9. Chi phí (phiếu xuất tiền) tháng này theo loại:
```sql
--QUERY_START--
SELECT ec.category_name, COALESCE(SUM(ev.amount), 0) as total_amount
FROM expense_vouchers ev
JOIN expense_categories ec ON ev.category_id = ec.category_id
WHERE ev.created_at >= '{firstDayOfMonthStr}' AND ev.status = 'Completed'
GROUP BY ec.category_name
ORDER BY total_amount DESC;
--QUERY_END--
```

## XỬ LÝ ĐẶC BIỆT
- Khi hỏi ""lợi nhuận"": luôn tính theo công thức SUM(quantity * (sell_price - cost_price)) trên order_items JOIN branch_products qua branch_product_id.
- Khi hỏi về sản phẩm bán chạy/doanh thu theo sản phẩm: order_items KHÔNG có product_id, PHẢI JOIN qua branch_products (branch_product_id) rồi mới JOIN được tới products.
- Doanh thu hôm nay: WHERE DATE(created_at) = CURRENT_DATE
- Doanh thu tuần này: WHERE created_at >= '{mondayStr}'
- Doanh thu tháng này: WHERE created_at >= '{firstDayOfMonthStr}'
- Luôn thêm: AND status = 'Completed' khi tính doanh thu/lợi nhuận từ orders
- TUYỆT ĐỐI KHÔNG được viết SQL đụng tới các bảng ngoài danh sách DATABASE SCHEMA ở trên (ví dụ: users, roles, permissions, otp_verifications, system_configs, system_logs, store_approvals, hay bất kỳ bảng nào có tiền tố forum_/community_). Nếu user hỏi về những nội dung đó, trả lời rằng chatbot không hỗ trợ truy vấn thông tin này.
- TUYỆT ĐỐI KHÔNG được viết INSERT, UPDATE, DELETE, DROP, ALTER, TRUNCATE hay bất kỳ câu lệnh nào không phải SELECT.

## NGUYÊN TẮC
1. Tiếng Việt, thân thiện
2. Dùng bảng Markdown đẹp mắt
3. Không bịa đặt số liệu
4. Khi không có dữ liệu: ""Dạ hiện tại em chưa tìm thấy dữ liệu nào ạ.""
5. Khi lỗi: ""Dạ hệ thống đang bận, anh/chị vui lòng thử lại sau nhé.""
";
        return prompt;
    }
}