using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MEP_Management_System.Services.Chatbot;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MEP_Management_System.Services.ChatbotServices;

public class ChatbotService : IChatbotService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly SqlExecutor _sqlExecutor;
    private readonly ChatbotRateLimiter _rateLimiter;
    private readonly ILogger<ChatbotService> _logger;

    private const string DefaultApiUrl = "https://api.deepseek.com/chat/completions";
    private const string DefaultModel = "deepseek-v4-flash";
    private const int MaxSqlIterations = 4;

    private static readonly Regex QueryStartEndRegex = new(
        @"--QUERY_START--\s*(.*?)(?:--QUERY_END--|$)",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SqlCodeBlockRegex = new(
        @"```sql\s*(.*?)(?:```|$)",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CleanQueryBlockRegex = new(
        @"--QUERY_START--.*?(?:--QUERY_END--|$)",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CleanSqlBlockRegex = new(
        @"```sql.*?(?:```|$)",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ChatbotService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        SqlExecutor sqlExecutor,
        ChatbotRateLimiter rateLimiter,
        ILogger<ChatbotService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _sqlExecutor = sqlExecutor;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public async Task<ChatbotResponse> ProcessMessageAsync(
        string message,
        List<ChatbotMessageDto>? history,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        if (!_rateLimiter.Allow(userId))
        {
            return new ChatbotResponse
            {
                Success = false,
                Message = "Bạn đang gửi quá nhiều yêu cầu. Vui lòng đợi một chút rồi thử lại."
            };
        }

        var branchId = user.FindFirst("branchId")?.Value;
        if (string.IsNullOrWhiteSpace(branchId))
        {
            _logger.LogWarning(
                "User {User} gọi chatbot nhưng token không có claim branchId - từ chối truy vấn.",
                user.FindFirst(ClaimTypes.Name)?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown");

            return new ChatbotResponse
            {
                Success = false,
                Message = "Tài khoản chưa được gán chi nhánh, không thể sử dụng chatbot."
            };
        }

        var apiUrl = _config["AI_API_URL"] ?? DefaultApiUrl;
        var apiKey = _config["AI_API_KEY"];
        var model = _config["AI_MODEL"] ?? DefaultModel;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new ChatbotResponse
            {
                Success = false,
                Message = "AI_API_KEY chưa được cấu hình."
            };
        }

        var today = DateTime.Now;
        var systemPrompt = BuildSystemPrompt(today, branchId);

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        if (history != null && history.Any())
        {
            foreach (var msg in history)
            {
                var role = msg.Role.ToLower() == "bot" || msg.Role.ToLower() == "assistant" ? "assistant" : "user";
                messages.Add(new { role = role, content = msg.Content });
            }
        }

        messages.Add(new { role = "user", content = message });

        return await ProcessChatAsync(apiUrl, apiKey, model, messages, message, branchId, cancellationToken);
    }

    private async Task<ChatbotResponse> ProcessChatAsync(
        string apiUrl,
        string apiKey,
        string model,
        List<object> messages,
        string userMessage,
        string branchId,
        CancellationToken cancellationToken)
    {
        bool lastQuerySucceeded = false;

        for (int iteration = 0; iteration < MaxSqlIterations; iteration++)
        {
            _logger.LogInformation("AI iteration {Iteration} starting", iteration + 1);

            var (success, respText, errorResponse) = await CallAiApiAsync(apiUrl, apiKey, model, messages, cancellationToken);
            if (!success)
                return errorResponse!;

            var aiResponse = ExtractAiContent(respText) ?? "";

            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                const int maxRetries = 2;
                for (int retry = 0; retry < maxRetries; retry++)
                {
                    _logger.LogWarning("AI returned empty response (retry {Retry}/{Max}), waiting 1s before retrying...", retry + 1, maxRetries);
                    await Task.Delay(1000, cancellationToken);

                    var (retrySuccess, retryRespText, retryError) = await CallAiApiAsync(apiUrl, apiKey, model, messages, cancellationToken);
                    if (!retrySuccess) return retryError!;

                    aiResponse = ExtractAiContent(retryRespText) ?? "";
                    if (!string.IsNullOrWhiteSpace(aiResponse)) break;
                }

                if (string.IsNullOrWhiteSpace(aiResponse))
                {
                    return new ChatbotResponse
                    {
                        Success = true,
                        Message = "OK",
                        Data = "Dạ hệ thống AI đang bận, anh/chị vui lòng thử lại sau vài giây nhé."
                    };
                }
            }

            messages.Add(new { role = "assistant", content = aiResponse });

            var (hasSql, sqlQuery) = ExtractSqlQuery(aiResponse);

            if (!hasSql)
            {
                var cleanResp = CleanAiResponse(aiResponse);
                if (string.IsNullOrWhiteSpace(cleanResp))
                {
                    cleanResp = "Dạ, câu trả lời của em đang bị gián đoạn. Anh/chị vui lòng thử lại nhé.";
                }

                return new ChatbotResponse
                {
                    Success = true,
                    Message = "OK",
                    Data = cleanResp
                };
            }

            _logger.LogInformation("Found SQL query: {Sql}", sqlQuery);

            var execResult = await _sqlExecutor.ExecuteQueryAsync(sqlQuery!, branchId);
            lastQuerySucceeded = execResult.success;

            if (!execResult.success)
            {
                var errorMessage = $"❌ Query lỗi: {execResult.message}. Vui lòng kiểm tra lại SQL.";
                messages.Add(new { role = "user", content = errorMessage });
                continue;
            }

            var dataContext = execResult.data != null
                ? JsonSerializer.Serialize(execResult.data)
                : "Không có dữ liệu.";

            const int maxDataContextChars = 8000;
            if (dataContext.Length > maxDataContextChars)
            {
                dataContext = dataContext.Substring(0, maxDataContextChars)
                    + $"\n... (bị cắt: chỉ hiển thị {maxDataContextChars}/{dataContext.Length} ký tự đầu)";
            }

            var dataMessage = $"✅ Đây là kết quả từ database:\n```json\n{dataContext}\n```\n\nHãy trình bày kết quả này cho user bằng tiếng Việt, dùng bảng markdown nếu cần.";
            messages.Add(new { role = "user", content = dataMessage });

            _logger.LogInformation("Sent data context to AI, continuing to final response...");
        }

        if (!lastQuerySucceeded)
        {
            messages.Add(new
            {
                role = "user",
                content = "Quá trình truy vấn SQL đã bị lỗi hoặc không lấy được dữ liệu. BẠN TUYỆT ĐỐI KHÔNG ĐƯỢC BỊA SỐ LIỆU (hallucinate). Hãy trả lời user bằng đúng một câu duy nhất: 'Dạ hệ thống hiện chưa có đủ dữ liệu để trả lời hoặc truy vấn đang gặp lỗi, anh/chị vui lòng hỏi lại theo cách khác giúp em ạ.' KHÔNG viết thêm bất kỳ câu nào khác và KHÔNG sinh SQL nữa."
            });
        }

        var (finalSuccess, finalRespText, finalErrorResponse) = await CallAiApiAsync(apiUrl, apiKey, model, messages, cancellationToken);
        if (!finalSuccess)
            return finalErrorResponse!;

        var finalAnswer = ExtractAiContent(finalRespText)
            ?? "Dạ hiện tại em chưa xử lý được câu hỏi này ạ, anh/chị vui lòng hỏi lại theo cách khác.";

        finalAnswer = CleanAiResponse(finalAnswer);
        if (string.IsNullOrWhiteSpace(finalAnswer))
        {
            finalAnswer = "Dạ hệ thống đang xử lý dữ liệu hơi chậm hoặc câu trả lời quá dài, anh/chị vui lòng thử lại nhé.";
        }

        return new ChatbotResponse
        {
            Success = true,
            Message = "OK",
            Data = finalAnswer
        };
    }

    private async Task<(bool success, string responseBody, ChatbotResponse? error)> CallAiApiAsync(
        string apiUrl,
        string apiKey,
        string model,
        List<object> messages,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(180);

        var payload = new
        {
            model = model,
            messages = messages,
            max_tokens = 10000,
            temperature = 0.3
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var resp = await client.PostAsync(apiUrl, httpContent, cancellationToken);
        var respText = await resp.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation("Raw AI Response: {Response}", respText);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("AI API error: {Status} - {Body}", resp.StatusCode, respText);
            return (false, respText, new ChatbotResponse
            {
                Success = false,
                Message = "Lỗi khi gọi AI API.",
                Errors = new List<string> { $"Status: {resp.StatusCode}", respText }
            });
        }

        return (true, respText, null);
    }

    internal static string? ExtractAiContent(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (!doc.RootElement.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return null;
            }

            if (!choices[0].TryGetProperty("message", out var message)
                || !message.TryGetProperty("content", out var content))
            {
                return null;
            }

            return content.ValueKind == JsonValueKind.String ? content.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static (bool hasSql, string? sqlQuery) ExtractSqlQuery(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return (false, null);

        var match = QueryStartEndRegex.Match(response);
        if (match.Success)
            return (true, match.Groups[1].Value.Trim());

        if (response.Contains("```sql", StringComparison.OrdinalIgnoreCase))
        {
            var sqlBlockMatch = SqlCodeBlockRegex.Match(response);
            if (sqlBlockMatch.Success)
                return (true, sqlBlockMatch.Groups[1].Value.Trim());
        }

        return (false, null);
    }

    internal static string CleanAiResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return "";

        var cleaned = CleanQueryBlockRegex.Replace(response, "");
        cleaned = CleanSqlBlockRegex.Replace(cleaned, "");

        return cleaned.Trim();
    }

    internal string BuildSystemPrompt(DateTime today, string branchId)
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
        var thirtyDaysAgoStr = today.AddDays(-30).ToString("yyyy-MM-dd");

        var prompt = $@"## NHIỆM VỤ CHÍNH
Bạn là trợ lý ảo cho hệ thống quản lý kho và bán hàng MEP Management System.
- Trả lời các câu hỏi về báo cáo, tồn kho, doanh thu, công nợ, hóa đơn, sản phẩm...
- Khi cần thông tin cụ thể, VIẾT SQL QUERY để lấy dữ liệu từ database.
- QUAN TRỌNG: Dùng format SQL query đặc biệt dưới đây.
- LƯU Ý VỀ BẢO MẬT CHI NHÁNH (RLS):
  + Các bảng `orders` và `expense_vouchers` đã có Row Level Security (tự động lọc). Bạn KHÔNG CẦN thêm điều kiện branch_id.
  + CÁC BẢNG KHÁC (như payments, invoices, invoice_items, customers, return_orders,...) CHƯA CÓ RLS. Bạn BẮT BUỘC phải TỰ LỌC chi nhánh trong MỌI câu lệnh (kể cả subquery) bằng điều kiện: `branch_id = current_setting('app.current_branch_id', true)::uuid` (hoặc JOIN qua bảng có branch_id). NẾU QUÊN, SẼ GÂY LỘ DỮ LIỆU CHI NHÁNH KHÁC!

## ⚠️ QUY TẮC BẮT BUỘC - NẾU VI PHẠM SẼ TRẢ SAI DỮ LIỆU:
1. Khi JOIN với bảng **products**: LUÔN LUÔN thêm `p.status = 1`
2. Khi JOIN với bảng **branch_products**: LUÔN LUÔN thêm `bp.is_active = 1` (Lưu ý cột này là smallint, TUYỆT ĐỐI KHÔNG dùng = true)
3. KHÔNG BAO GIỜ được trả thông tin sản phẩm đã xóa (status ≠ 1) hoặc đã ngừng kinh doanh (is_active ≠ 1)
4. Khi hỏi số lượng tồn kho: PHẢI lấy từ cột **available_stock** trong bảng **branch_products**, KHÔNG phải từ bảng products
5. Khi hỏi giá: PHẢI lấy từ **sell_price** trong bảng **branch_products** (vì mỗi chi nhánh có giá khác nhau)
6. KHÔNG BAO GIỜ dùng `SELECT *` - luôn liệt kê cột cụ thể cần thiết (SELECT * bị hệ thống chặn)
7. TẤT CẢ các cột ID (branch_id, product_id, category_id, user_id...) đều là kiểu UUID. TUYỆT ĐỐI không bao giờ so sánh ID với số nguyên.
8. ⚠️ KHÔNG ĐƯỢC TỰ BỊA DỮ LIỆU: Nếu query SQL bị lỗi hoặc hệ thống trả về kết quả rỗng/null, TUYỆT ĐỐI KHÔNG tự tạo ra dữ liệu giả (như bịa số liệu thanh toán, doanh thu). Hãy trả lời trung thực là ""Không tìm thấy dữ liệu"" hoặc ""Hệ thống đang gặp lỗi truy vấn, xin vui lòng thử lại"".
9. ⚠️ BẮT BUỘC TUÂN THỦ QUY TRÌNH 2 BƯỚC:
   BẠN KHÔNG ĐƯỢC PHÉP TỰ TRẢ LỜI VÀ TỰ BỊA DỮ LIỆU TRONG CÙNG 1 LƯỢT CHAT (VÌ BẠN KHÔNG CÓ DATABASE).
   - BƯỚC 1: Khi cần số liệu, PHẦN CONTENT (TRẢ LỜI CHÍNH THỨC) CỦA BẠN CHỈ ĐƯỢC CHỨA DUY NHẤT 1 BLOCK `--QUERY_START--...--QUERY_END--`. Không được viết thêm bất kỳ chữ nào khác. ĐỂ TRỐNG KẾT QUẢ VÀ DỪNG LẠI! (Hệ thống backend C# sẽ tự động parse block SQL của bạn, đem đi chạy trên Database thật).
   - BƯỚC 2: Hệ thống sẽ tự động gửi lại cho bạn 1 tin nhắn chứa ""✅ Đây là kết quả từ database: ..."". CHỈ KHI NÀY bạn mới được phép đọc dữ liệu thật và xuất ra bảng markdown cho user.
   TUYỆT ĐỐI KHÔNG ĐƯỢC GIẤU SQL VÀO PHẦN SUY LUẬN (REASONING) VÀ BỊA KẾT QUẢ VÀO CONTENT! TẤT CẢ SQL PHẢI Ở TRONG PHẦN CONTENT ĐỂ HỆ THỐNG PARSE.
10. ⚠️ BẢO MẬT DỮ LIỆU CHI NHÁNH: Hệ thống tự động giới hạn (khóa) dữ liệu trả về ĐÚNG với chi nhánh mà user đang đăng nhập. Nếu user yêu cầu lấy dữ liệu của một ""cửa hàng X"" hoặc ""chi nhánh Y"" NÀO ĐÓ KHÁC, bạn TUYỆT ĐỐI KHÔNG ĐƯỢC giả vờ truy vấn rồi báo cáo rằng đó là dữ liệu của cửa hàng X/Y (vì thực chất dữ liệu nhận được vẫn luôn là của cửa hàng hiện tại). Hãy từ chối khéo léo: ""Dạ, em chỉ có quyền truy cập và cung cấp dữ liệu của cửa hàng hiện tại mà anh/chị đang quản lý thôi ạ.""

## DATABASE SCHEMA (PostgreSQL) - CHỈ được dùng đúng các bảng/cột dưới đây, KHÔNG được tự bịa bảng/cột khác

- branches: branch_id, branch_name, address, branch_code, city, is_active, type
- branch_products: branch_id, product_id, cost_price, sell_price, available_stock, actual_stock, reserved_stock, minimum_stock, maximum_stock, shelf_location, is_active
- products: product_id, product_code, product_name, barcode, base_unit, category_name, brand_name, supplier_id, weight, weight_unit, status, created_at
- categories: category_id, category_name, parent_id, type
- suppliers: supplier_id, supplier_name, supplier_code, phone, email, address, debt, is_active
- customers: customer_id, customer_name, phone, email, address, customer_type, debt, loyalty_points, is_active, branch_id
- orders: order_id, order_code, customer_id, branch_id, total_amount (LƯU Ý: LÀ TỔNG TIỀN SAU CHIẾT KHẤU/DOANH THU THỰC TẾ, KHÔNG ĐƯỢC TRỪ THÊM discount_amount), discount_amount, payment_method, status, created_at, notes, user_id
- order_items: order_item_id, order_id, product_id, quantity, unit_price, discount_amount, total_amount
- payments: payment_id, order_id, customer_id, amount, payment_method, payment_date, notes, user_id, branch_id
- invoices: invoice_id, invoice_code, order_id, shift_id, sub_total_amount (tiền trước chiết khấu), discount_amount, total_amount (tiền sau chiết khấu - LÀ DOANH THU THỰC TẾ, KHÔNG TRỪ THÊM discount_amount), payment_method, status, created_at
- invoice_items: invoice_item_id, invoice_id, product_id, quantity, sell_price, return_quantity, total_amount
- return_orders: return_order_id, return_code, order_id, customer_id, branch_id, return_amount, reason, return_date, status, user_id
- return_order_items: return_order_item_id, return_order_id, product_id, quantity, unit_price, total_amount
- users: user_id, username, email, full_name, phone, role, branch_id, is_active
- expenses: expense_id, expense_code, expense_type, amount, expense_date, description, user_id, branch_id, is_active, supplier_id
- expense_vouchers: voucher_id, voucher_code, voucher_type, amount, voucher_date, description, user_id, branch_id, created_at
- supplier_debts: supplier_debt_id, supplier_id, amount, debt_type, reference_type, reference_id, due_date, notes, branch_id, created_at
- customer_debts: customer_debt_id, customer_id, amount, debt_type, reference_type, reference_id, due_date, notes, branch_id, created_at
- inventory_checks: check_id, check_code, check_date, status, notes, user_id, branch_id, created_at
- inventory_check_items: check_item_id, check_id, product_id, system_qty, actual_qty, difference, notes
- product_conversion_units: conversion_id, product_id, unit_name, convert_value, price, direct_sale
- media_resources: media_id, product_id, media_url, is_primary

## THÔNG TIN NGÀY THÁNG:
- Hôm nay: {todayStr}
- Thứ 2 tuần này: {mondayStr}
- Ngày đầu tháng: {firstDayOfMonthStr}
- Ngày cuối tháng: {lastDayOfMonth}
- 30 ngày trước: {thirtyDaysAgoStr}

## VÍ DỤ SQL HỢP LỆ:
```sql
--QUERY_START--
SELECT
    p.product_name,
    bp.available_stock,
    bp.sell_price,
    bp.cost_price
FROM branch_products bp
JOIN products p ON bp.product_id = p.product_id
WHERE bp.branch_id = current_setting('app.current_branch_id', true)::uuid
  AND p.status = 1
  AND bp.is_active = 1
ORDER BY p.product_name
LIMIT 10;
--QUERY_END--
```

## Format SQL Query bắt buộc:
```
--QUERY_START--
[SQL QUERY CỦA BẠN Ở ĐÂY]
--QUERY_END--
```

Hãy trả lời câu hỏi của user bằng tiếng Việt. Nếu cần dữ liệu cụ thể, hãy viết SQL query sử dụng format bên trên.";

        return prompt;
    }
}
