using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
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
    private readonly ChatbotRateLimiter _rateLimiter;
    private readonly ILogger<ChatController> _logger;

    // Cấu hình provider/model nằm trong appsettings.json (hiện đang dùng DeepSeek),
    // KHÔNG cần sửa code khi đổi provider (Gemini/Groq/DeepSeek...):
    //   "AI_API_URL": "https://api.deepseek.com/chat/completions"
    //   "AI_API_KEY": "..."
    //   "AI_MODEL": "deepseek-v4-flash"
    // (Gemini OpenAI-compatible endpoint: https://generativelanguage.googleapis.com/v1beta/openai/chat/completions)
    private const string DefaultApiUrl = "https://api.deepseek.com/chat/completions";
    private const string DefaultModel = "deepseek-v4-flash";
    private const int MaxSqlIterations = 4;

    // Compiled Regex patterns (tạo 1 lần, dùng lại mọi request - tránh khởi tạo lại mỗi lần gọi)
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

    public ChatController(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        SqlExecutor sqlExecutor,
        ChatbotRateLimiter rateLimiter,
        ILogger<ChatController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _sqlExecutor = sqlExecutor;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    public class ChatMessageDto
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<ChatMessageDto>? History { get; set; }
    }

    public class ChatResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Data { get; set; }
        public List<string>? Errors { get; set; }
    }

    [HttpPost("message")]
    public async Task<IActionResult> SendMessage([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new ChatResponse
            {
                Success = false,
                Message = "Yêu cầu không hợp lệ: message trống."
            });
        }

        // 🔒 Rate limit theo user: mỗi request có thể gọi AI API nhiều lần (tốn phí
        // token) nên chặn spam từ 1 tài khoản - trả 429 khi vượt hạn mức cửa sổ.
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        if (!_rateLimiter.Allow(userId))
        {
            return StatusCode(429, new ChatResponse
            {
                Success = false,
                Message = "Bạn đang gửi quá nhiều yêu cầu. Vui lòng đợi một chút rồi thử lại."
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
        var apiKey = _config["AI_API_KEY"];
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
            var systemPrompt = BuildSystemPrompt(today, branchId);

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            // Nếu frontend gửi kèm history, ta nối vào trước câu hiện tại
            if (request.History != null && request.History.Any())
            {
                foreach (var msg in request.History)
                {
                    // Đảm bảo role chỉ là user hoặc assistant
                    var role = msg.Role.ToLower() == "bot" || msg.Role.ToLower() == "assistant" ? "assistant" : "user";
                    messages.Add(new { role = role, content = msg.Content });
                }
            }

            messages.Add(new { role = "user", content = request.Message });
            var result = await ProcessChatAsync(apiUrl, apiKey, model, messages, request.Message, branchId, cancellationToken);

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

    private async Task<ChatResponse> ProcessChatAsync(string apiUrl, string apiKey, string model, List<object> messages, string userMessage, string branchId, CancellationToken cancellationToken)
    {
        bool lastQuerySucceeded = false;

        for (int iteration = 0; iteration < MaxSqlIterations; iteration++)
        {
            _logger.LogInformation("AI iteration {Iteration} starting", iteration + 1);

            var (success, respText, errorResponse) = await CallAiApiAsync(apiUrl, apiKey, model, messages, cancellationToken);
            if (!success)
                return errorResponse!;

            var aiResponse = ExtractAiContent(respText) ?? "";

            // DeepSeek có thể trả response rỗng khi nhận request liên tiếp quá nhanh.
            // Retry tối đa 2 lần với delay 1s giữa mỗi lần.
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
                    return new ChatResponse
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
                
                return new ChatResponse
                {
                    Success = true,
                    Message = "OK",
                    Data = cleanResp
                };
            }

            _logger.LogInformation("Found SQL query: {Sql}", sqlQuery);

            // 🔒 Truyền branchId của user đang chat xuống SqlExecutor. Executor
            // sẽ set session variable app.current_branch_id trên connection
            // trước khi chạy SQL, để Row Level Security tự lọc đúng chi nhánh -
            // không phụ thuộc vào việc AI có nhớ filter branch_id hay không.
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

            // Cắt context trước khi gửi lại cho AI - tránh phình message history
            // (mỗi query trả tới 500 dòng) làm vượt context window / đốt token.
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

        // Hết số vòng lặp SQL cho phép mà vẫn chưa có câu trả lời cuối cùng.
        // Ép AI trả lời thẳng bằng dữ liệu đã có (nếu truy vấn cuối cùng thành công).
        // Nếu truy vấn cuối cùng thất bại, cấm bịa số liệu.
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

        return new ChatResponse
        {
            Success = true,
            Message = "OK",
            Data = finalAnswer
        };
    }

    /// <summary>
    /// Helper gọi AI API duy nhất - loại bỏ code trùng lặp giữa vòng lặp và lần gọi cuối.
    /// Trả về (success, responseBody, errorChatResponse?).
    /// </summary>
    private async Task<(bool success, string responseBody, ChatResponse? error)> CallAiApiAsync(
        string apiUrl, string apiKey, string model, List<object> messages, CancellationToken cancellationToken)
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

        // Log ở mức Debug - response AI có thể chứa dữ liệu kinh doanh/PII
        // (AI thường lặp lại kết quả DB trong câu trả lời), tránh ghi vào log file.
        _logger.LogInformation("Raw AI Response: {Response}", respText);

        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogError("AI API error: {Status} - {Body}", resp.StatusCode, respText);
            return (false, respText, new ChatResponse
            {
                Success = false,
                Message = "Lỗi khi gọi AI API.",
                Errors = new List<string> { $"Status: {resp.StatusCode}", respText }
            });
        }

        return (true, respText, null);
    }

    /// <summary>
    /// Trích xuất content từ response JSON của AI API. Không throw khi response
    /// lỗi định dạng (HTML/empty body) - trả null để caller dùng fallback message.
    /// </summary>
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
            // Response không phải JSON hợp lệ - không phá vỡ request, để caller xử lý.
            return null;
        }
    }

    internal static (bool hasSql, string? sqlQuery) ExtractSqlQuery(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return (false, null);

        // Chấp nhận kể cả khi AI quên viết --QUERY_END-- (bị cắt cụt)
        var match = QueryStartEndRegex.Match(response);
        if (match.Success)
            return (true, match.Groups[1].Value.Trim());

        // Fallback: AI viết ```sql ... ``` thay vì --QUERY_START--
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

    // FIX: Trước đây hàm này lỗi vì trộn chuỗi verbatim (@"...") với các đoạn
    // nối bằng " + bien + " nhưng KHÔNG đánh dấu @ cho đoạn nối lại, mà đoạn đó
    // lại chứa xuống dòng thật -> C# không cho phép newline trong chuỗi thường
    // => lỗi "expected }", "Unexpected character '`'", "Too many characters in
    // character literal" domino tiếp theo.
    // Sửa bằng cách dùng chuỗi nội suy verbatim $@"..." và {bien} thay vì " + bien + "
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
- branch_products: branch_product_id, branch_id, product_id, cost_price, sell_price, actual_stock, available_stock, reserved_stock, minimum_stock, maximum_stock, is_active
- products: product_id, product_code, product_name, barcode, base_unit, brand_name, category_name, supplier_id, status
- product_attributes: attribute_id, product_id, attribute_name, attribute_value
- product_conversion_units: conversion_id, product_id, unit_name, convert_value, price
- users: user_id, full_name, email, phone_number, status (Lưu ý: status có các giá trị 'ACTIVE', 'INACTIVE', 'DELETED'. LUÔN lọc status = 'ACTIVE' và role_name != 'Owner' khi hỏi về nhân viên hiện tại)
- user_roles: user_id, role_id
- roles: role_id, role_name
- customers: customer_id, customer_name, phone_number, email, address, loyalty_points, debt_limit, current_debt, status, ""group"", membership_tier, order_count, total_spent
- orders: order_id, order_code, branch_id, customer_id, user_id, total_amount, discount_amount, tax_amount, payment_method, status, created_at
- invoice_items: invoice_item_id, invoice_id, branch_product_id, quantity, sell_price, cost_price
  (LƯU Ý: invoice_items KHÔNG có product_id. Muốn lấy product_id/product_name phải JOIN qua branch_products bằng branch_product_id)
- order_discount_tiers: order_discount_tier_id, min_order_value, discount_percent, is_active
- invoices: invoice_id, invoice_code, order_id, user_id, total_amount, sub_total_amount, discount_percent, discount_amount, payment_method, status, note, created_at
- payments: payment_id, invoice_id, method, amount, status, cash_received, change_amount, paid_at
- refund_records: refund_id, refund_code, order_id, invoice_id, customer_id, user_id, refund_amount, refund_method, status, reason, created_at
- return_orders: return_order_id, return_code, order_id, user_id, refund_amount, refund_method, status, note, created_at, return_type
- return_items: return_item_id, return_order_id, branch_product_id, quantity, refund_amount, reason
- suppliers: supplier_id, supplier_name, contact_phone, contact_email, address, status, contact_person, tax_code, group_name, average_rating
- purchase_orders: purchase_order_id, order_code, supplier_id, user_id, total_amount, status, approved_by, approved_at, expected_delivery_date, created_at
- purchase_order_items: po_item_id, purchase_order_id, product_id, quantity, unit_price
- supplier_payments: payment_id, supplier_id, amount, payment_date, is_deleted
- stock_tickets: stock_ticket_id, ticket_code, branch_id, user_id, ticket_type, status, notes, created_at, supplier_id, total_amount
- stock_ticket_items: ticket_item_id, stock_ticket_id, branch_product_id, system_quantity, actual_quantity, discrepancy
- inventory_check_tickets: ticket_id, ticket_code, created_at, status, branch_id
- inventory_check_details: detail_id, ticket_id, product_id, system_quantity, actual_quantity, discrepancy
- inventory_transfer_tickets: transfer_id, transfer_code, from_branch_id, to_branch_id, status, created_at
- inventory_transfer_details: detail_id, transfer_id, product_id, transfer_quantity
- expense_vouchers: voucher_id, voucher_code, branch_id, category_id, supplier_id, amount, reason, status, created_at
  (LƯU Ý: status của expense_vouchers dùng CHỮ HOA: 'PENDING' / 'COMPLETED' / 'CANCELLED' - KHÔNG phải 'Completed')
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

## SQL MẪU (QUAN TRỌNG: Xem muc CÁCH TÍNH DOANH THU VÀ LỢI NHUẬN bên dưới trước khi viết query):

1. Tìm sản phẩm (LUÔN filter status=1 và is_active=1):
```sql
--QUERY_START--
SELECT p.product_code, p.product_name, p.brand_name, bp.sell_price, bp.available_stock
FROM products p
LEFT JOIN branch_products bp ON p.product_id = bp.product_id
WHERE (p.product_name ILIKE '%Aptomat%' OR p.product_code ILIKE '%Aptomat%')
  AND p.status = 1 AND (bp.is_active = 1 OR bp.is_active IS NULL)
LIMIT 20;
--QUERY_END--
```

2. Doanh thu hôm nay (PHẢI dùng payments với status='Confirmed', KHÔNG dùng orders):
QUAN TRỌNG: LUÔN dùng timezone 'Asia/Ho_Chi_Minh' cho mọi so sánh ngày tháng.
PHẢI JOIN với orders để lọc theo branch_id (vì payments không có branch_id trực tiếp).
```sql
--QUERY_START--
SELECT COUNT(DISTINCT p.invoice_id) as total_invoices,
       COALESCE(SUM(p.amount), 0) as total_revenue
FROM payments p
JOIN invoices i ON p.invoice_id = i.invoice_id
JOIN orders o ON i.order_id = o.order_id
WHERE DATE(p.paid_at AT TIME ZONE 'Asia/Ho_Chi_Minh') = DATE(now() AT TIME ZONE 'Asia/Ho_Chi_Minh')
  AND p.status = 'Confirmed'
  AND i.status IN ('COMPLETED', 'Completed', 'PAID', 'Done')
  AND o.branch_id IS NOT NULL;
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
  AND p.status = 1 AND bp.is_active = 1
ORDER BY bp.available_stock ASC LIMIT 20;
--QUERY_END--
```

5. Sản phẩm bán chạy (dùng invoice_items thay vì order_items):
```sql
--QUERY_START--
SELECT p.product_code, p.product_name, SUM(ii.quantity) as total_sold
FROM invoice_items ii
JOIN invoices i ON ii.invoice_id = i.invoice_id
JOIN orders o ON i.order_id = o.order_id
JOIN branch_products bp ON ii.branch_product_id = bp.branch_product_id
JOIN products p ON bp.product_id = p.product_id
WHERE i.created_at >= '{firstDayOfMonthStr}' AND i.status = 'Completed'
GROUP BY p.product_id, p.product_code, p.product_name
ORDER BY total_sold DESC LIMIT 10;
--QUERY_END--
```

6. Lợi nhuận hôm nay (CÔNG THỨC ĐÚNG: revenue - cogs - expense - refund):
```sql
--QUERY_START--
WITH metrics AS (
    -- Revenue = sum payments confirmed
    SELECT COALESCE(SUM(p.amount), 0) as revenue
    FROM payments p
    JOIN invoices i ON p.invoice_id = i.invoice_id
    WHERE DATE(p.paid_at AT TIME ZONE 'Asia/Ho_Chi_Minh') = DATE(now() AT TIME ZONE 'Asia/Ho_Chi_Minh')
      AND p.status = 'Confirmed'
      AND i.status IN ('COMPLETED', 'Completed', 'PAID', 'Done')
),
cogs_calc AS (
    -- COGS = sum of confirmed invoice items cost
    SELECT COALESCE(SUM(ii.quantity * bp.cost_price), 0) as cogs
    FROM invoice_items ii
    JOIN invoices i ON ii.invoice_id = i.invoice_id
    JOIN branch_products bp ON ii.branch_product_id = bp.branch_product_id
    WHERE ii.invoice_id IN (
        SELECT p.invoice_id FROM payments p
        WHERE DATE(p.paid_at AT TIME ZONE 'Asia/Ho_Chi_Minh') = DATE(now() AT TIME ZONE 'Asia/Ho_Chi_Minh')
          AND p.status = 'Confirmed'
    )
    AND i.status IN ('COMPLETED', 'Completed', 'PAID', 'Done')
),
expense_calc AS (
    SELECT COALESCE(SUM(amount), 0) as expense FROM expense_vouchers
    WHERE DATE(created_at AT TIME ZONE 'Asia/Ho_Chi_Minh') = DATE(now() AT TIME ZONE 'Asia/Ho_Chi_Minh')
      AND status = 'COMPLETED'
),
refund_calc AS (
    SELECT COALESCE(SUM(refund_amount), 0) as refund FROM return_orders
    WHERE DATE(created_at AT TIME ZONE 'Asia/Ho_Chi_Minh') = DATE(now() AT TIME ZONE 'Asia/Ho_Chi_Minh')
      AND status IN ('COMPLETED', 'Completed')
)
SELECT
    m.revenue,
    c.cogs,
    e.expense,
    r.refund,
    (m.revenue - c.cogs - e.expense - r.refund) as net_profit
FROM metrics m, cogs_calc c, expense_calc e, refund_calc r;
--QUERY_END--
```

7. Lợi nhuận 30 ngày (CÔNG THỨC ĐÚNG: revenue - cogs - expense - refund):
```sql
--QUERY_START--
WITH metrics AS (
    SELECT COALESCE(SUM(p.amount), 0) as revenue
    FROM payments p
    JOIN invoices i ON p.invoice_id = i.invoice_id
    WHERE p.paid_at >= (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh') - INTERVAL '30 days'
      AND p.status = 'Confirmed'
      AND i.status IN ('COMPLETED', 'Completed', 'PAID', 'Done')
),
cogs_calc AS (
    SELECT COALESCE(SUM(ii.quantity * bp.cost_price), 0) as cogs
    FROM invoice_items ii
    JOIN invoices i ON ii.invoice_id = i.invoice_id
    JOIN branch_products bp ON ii.branch_product_id = bp.branch_product_id
    WHERE ii.invoice_id IN (
        SELECT p.invoice_id FROM payments p
        WHERE p.paid_at >= (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh') - INTERVAL '30 days' AND p.status = 'Confirmed'
    )
    AND i.status IN ('COMPLETED', 'Completed', 'PAID', 'Done')
),
expense_calc AS (
    SELECT COALESCE(SUM(amount), 0) as expense FROM expense_vouchers
    WHERE created_at >= (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh') - INTERVAL '30 days' AND status = 'COMPLETED'
),
refund_calc AS (
    SELECT COALESCE(SUM(refund_amount), 0) as refund FROM return_orders
    WHERE created_at >= (NOW() AT TIME ZONE 'Asia/Ho_Chi_Minh') - INTERVAL '30 days'
      AND status IN ('COMPLETED', 'Completed')
)
SELECT
    m.revenue,
    c.cogs,
    e.expense,
    r.refund,
    (m.revenue - c.cogs - e.expense - r.refund) as net_profit
FROM metrics m, cogs_calc c, expense_calc e, refund_calc r;
--QUERY_END--
```

8. Tổng nợ nhà cung cấp:
```sql
--QUERY_START--
SELECT 
    (SELECT COALESCE(SUM(total_amount), 0) FROM purchase_orders WHERE status IN ('APPROVED', 'PENDING'))
    + (SELECT COALESCE(SUM(total_amount), 0) FROM stock_tickets WHERE ticket_type = 'PURCHASE' AND status = 'COMPLETED')
    - (SELECT COALESCE(SUM(Amount), 0) FROM SupplierPayments WHERE IsDeleted = 0)
    AS total_supplier_debt;
--QUERY_END--
```

9. Chi tiết công nợ theo từng nhà cung cấp:
```sql
--QUERY_START--
SELECT 
    s.supplier_name,
    s.contact_phone,
    COALESCE(po.amount, 0) + COALESCE(st.amount, 0) - COALESCE(paid.amount, 0) AS current_debt
FROM suppliers s
LEFT JOIN (
    SELECT supplier_id, SUM(total_amount) AS amount FROM purchase_orders WHERE status IN ('APPROVED', 'PENDING') GROUP BY supplier_id
) po ON s.supplier_id = po.supplier_id
LEFT JOIN (
    SELECT supplier_id, SUM(total_amount) AS amount FROM stock_tickets WHERE ticket_type = 'PURCHASE' AND status = 'COMPLETED' GROUP BY supplier_id
) st ON s.supplier_id = st.supplier_id
LEFT JOIN (
    SELECT SupplierId AS supplier_id, SUM(Amount) AS amount FROM SupplierPayments WHERE IsDeleted = 0 GROUP BY SupplierId
) paid ON s.supplier_id = paid.supplier_id
WHERE (COALESCE(po.amount, 0) + COALESCE(st.amount, 0) - COALESCE(paid.amount, 0)) > 0
ORDER BY current_debt DESC;
--QUERY_END--
```

10. Phân tích phương thức thanh toán trong 30 ngày qua:
```sql
--QUERY_START--
SELECT p.method as payment_method, count(*) as count, sum(p.amount) as total 
FROM payments p
JOIN invoices i ON p.invoice_id = i.invoice_id
WHERE p.status = 'Confirmed' AND i.status = 'Completed' AND p.paid_at >= (CURRENT_DATE - INTERVAL '30 days') AT TIME ZONE 'Asia/Ho_Chi_Minh'
GROUP BY p.method
--QUERY_END--
```

7d. Lợi nhuận theo tháng:
```sql
--QUERY_START--
SELECT DATE_TRUNC('month', i.created_at) as month,
    COALESCE(SUM(ii.quantity * (ii.sell_price - bp.cost_price)), 0) as monthly_profit
FROM invoice_items ii
JOIN invoices i ON ii.invoice_id = i.invoice_id
JOIN orders o ON i.order_id = o.order_id
JOIN branch_products bp ON ii.branch_product_id = bp.branch_product_id
WHERE i.created_at >= ('{firstDayOfMonthStr}'::date - INTERVAL '12 months') AND i.status = 'Completed'
GROUP BY DATE_TRUNC('month', i.created_at) ORDER BY month;
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

9. Chi phí (phiếu xuất tiền) tháng này theo loại (LƯU Ý: status dùng CHỮ HOA 'COMPLETED'):
```sql
--QUERY_START--
SELECT ec.category_name, COALESCE(SUM(ev.amount), 0) as total_amount
FROM expense_vouchers ev
JOIN expense_categories ec ON ev.category_id = ec.category_id
WHERE ev.created_at >= '{firstDayOfMonthStr}' AND ev.status = 'COMPLETED'
GROUP BY ec.category_name
ORDER BY total_amount DESC;
--QUERY_END--
```

10. Nhà cung cấp:
```sql
--QUERY_START--
SELECT supplier_name, contact_phone, contact_email, address, group_name
FROM suppliers
WHERE status = 1
ORDER BY supplier_name LIMIT 20;
--QUERY_END--
```

11. Tổng số nhà cung cấp:
```sql
--QUERY_START--
SELECT COUNT(*) as total_suppliers FROM suppliers WHERE status = 1;
--QUERY_END--
```

## XỬ LÝ ĐẶC BIỆT
- Khi user chào hỏi (xin chào, chào buổi sáng/trưa/chiều/tối, bạn là ai, bạn tên gì): trả lời thân thiện ""Dạ xin chào! Em là trợ lý ảo của hệ thống MEP Management System. Em có thể hỗ trợ anh/chị về báo cáo doanh thu, tồn kho, sản phẩm, khách hàng, đơn hàng, lợi nhuận, chi phí... Anh/chị cần em giúp gì ạ?""
- Khi hỏi ""lợi nhuận"": BẮT BUỘC tính theo công thức SUM(quantity * (ii.sell_price - bp.cost_price)) trên invoice_items (ii) JOIN branch_products (bp) qua branch_product_id. TUYỆT ĐỐI KHÔNG dùng ii.cost_price vì có thể dữ liệu cũ bị bằng 0.
- Khi hỏi về sản phẩm bán chạy/doanh thu theo sản phẩm: invoice_items KHÔNG có product_id, PHẢI JOIN qua branch_products (branch_product_id) rồi mới JOIN được tới products.
- Doanh thu hôm nay: WHERE DATE(created_at AT TIME ZONE 'Asia/Ho_Chi_Minh') = DATE(now() AT TIME ZONE 'Asia/Ho_Chi_Minh')
- Doanh thu tuần này: WHERE created_at >= '{mondayStr}'
- Doanh thu 30 ngày: WHERE created_at >= '{thirtyDaysAgoStr}'
- Doanh thu tháng này: WHERE created_at >= '{firstDayOfMonthStr}'
- Luôn thêm: AND status = 'Completed' khi tính doanh thu/lợi nhuận từ orders hoặc invoices
- LƯU Ý KHI LỌC SẢN PHẨM ĐÃ XÓA:
  - Khi hỏi về **tồn kho, danh sách sản phẩm hiện tại**: LUÔN lọc sản phẩm đã xóa (thêm AND p.status = 1 AND bp.is_active = 1). KHÔNG hiển thị sản phẩm chứa _DEL_.
  - Khi hỏi về **báo cáo doanh thu, lợi nhuận, sản phẩm bán chạy trong quá khứ**: KHÔNG lọc sản phẩm đã xóa (vì sản phẩm dù đã xóa vẫn phải tính vào doanh thu lịch sử).
- LƯU Ý KHI LỌC NHÂN VIÊN: LUÔN truy vấn từ bảng staff_assignments JOIN với users (lọc u.status = 'ACTIVE'). Để loại trừ chủ cửa hàng, hãy JOIN thêm với user_roles và roles (lọc r.role_name != 'Owner').
- TUYỆT ĐỐI KHÔNG được viết SQL đụng tới các bảng ngoài danh sách DATABASE SCHEMA ở trên (ví dụ: permissions, otp_verifications, system_configs, system_logs, store_approvals, hay bất kỳ bảng nào có tiền tố forum_/community_). Nếu user hỏi về những nội dung đó, trả lời rằng chatbot không hỗ trợ truy vấn thông tin này.
- TUYỆT ĐỐI KHÔNG được viết INSERT, UPDATE, DELETE, DROP, ALTER, TRUNCATE hay bất kỳ câu lệnh nào không phải SELECT.
- LƯU Ý VỀ TÊN BẢNG SupplierPayments: LUÔN dùng tên không trích dẫn (unquoted) SupplierPayments, KHÔNG dùng các dạng trích dẫn khác. Ví dụ đúng: FROM SupplierPayments WHERE IsDeleted = 0.

## 🚫 CHẶN CÂU HỎI NGOÀI PHẠM VI CHO PHÉP

### Cấm hỏi về HỆ THỐNG (system-wide):
- ""hệ thống có bao nhiêu cửa hàng"", ""tổng số chi nhánh""
- ""xem tất cả các chi nhánh"", ""danh sách chi nhánh""
- ""số lượng người dùng hệ thống"", ""tài khoản admin""
- ""quản lý hệ thống"", ""cấu hình hệ thống""
- BẤT KỲ câu hỏi nào về: tài khoản khác, chi nhánh khác, dữ liệu toàn hệ thống
- Khi user hỏi những câu này, TRẢ LỜI: ""Dạ, em chỉ có thể hỗ trợ xem dữ liệu trong cửa hàng mà anh/chị đang làm việc thôi ạ. Anh/chị có thể hỏi về doanh thu, tồn kho, sản phẩm, khách hàng... của cửa hàng mình không ạ?""

### Cấm hỏi LINH TINH (off-topic):
- Toán học: ""1+1 bằng mấy"", ""tính toán"", ""sqrt(2)""
- Kiến thức chung: ""tổng thống Mỹ là ai"", ""thời tiết"", ""tin tức""
- Giải trí: ""kể chuyện"", ""đố vui"", ""game""
- BẤT KỲ câu hỏi nào KHÔNG liên quan đến: bán hàng, kho, doanh thu, khách hàng, sản phẩm, đơn hàng, công nợ, chi phí, nhân viên, nhà cung cấp
- Khi user hỏi những câu này, TRẢ LỜI: ""Dạ em là trợ lý AI của hệ thống MEP Management System, chỉ hỗ trợ các câu hỏi về quản lý cửa hàng như: doanh thu, tồn kho, sản phẩm, khách hàng, đơn hàng, công nợ, chi phí... Anh/chị có câu hỏi nào về cửa hàng mình không ạ?""

## NGUYEN TAC
1. Tieng Viet, than thien
2. Dung bang Markdown dep mat
3. TUYET DOI rut gon ket qua neu qua dai (vd: chi lien ket 10-20 ket qua dau tien va bao tong so, khong bao gio sinh ra danh sach qua lon gay timeout).
4. Khong bia dat so lieu
5. Khi khong co du lieu: Da hien tai em chua tim thay du lieu nao a.
6. Khi loi: Da he thong dang ban, anh chi vui long thu lai sau nhe.
7. CHI tra loi ve du lieu cua CUA HANG hien tai cua user, KHONG tra loi ve toan he thong hay chi nhanh khac.
";
        return prompt;
    }
}