using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using MEP_Management_System.Services.Chatbot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace MEP_Management_System.Controllers.Chatbot;

/// <summary>
/// Chatbot Controller tích hợp Groq với function calling.
/// </summary>
[ApiController]
[Route("api/chatbot")]
[Authorize] // Yêu cầu xác thực JWT
public class ChatController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ToolDispatcher _toolDispatcher;
    private readonly ILogger<ChatController> _logger;

    // Groq API config keys
    private const string DefaultModel = "llama-3.1-70b-versatile";

    public ChatController(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ToolDispatcher toolDispatcher,
        ILogger<ChatController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _toolDispatcher = toolDispatcher;
        _logger = logger;
    }

    /// <summary>
    /// Request body cho chatbot message.
    /// </summary>
    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response từ chatbot.
    /// </summary>
    public class ChatResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Data { get; set; }
        public List<string>? Errors { get; set; }
    }

    /// <summary>
    /// Gửi tin nhắn cho chatbot - tích hợp Groq với function calling.
    /// POST /api/chatbot/message
    /// </summary>
    [HttpPost("message")]
    public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
    {
        // Validate request
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new ChatResponse
            {
                Success = false,
                Message = "Yêu cầu không hợp lệ: message trống.",
                Errors = new List<string> { "Request body must include 'message'." }
            });
        }

        // Get Groq config
        var apiKey = _config["GROQ_API_KEY"] ?? _config.GetValue<string>("GROQ_API_KEY");
        var model = _config["GROQ_MODEL"] ?? DefaultModel;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return StatusCode(500, new ChatResponse
            {
                Success = false,
                Message = "GROQ_API_KEY chưa được cấu hình.",
                Errors = new List<string> { "Please set GROQ_API_KEY in appsettings.json or environment variables." }
            });
        }

        try
        {
            // Get current user
            var currentUser = User;

            // Build system prompt
            var systemPrompt = BuildSystemPrompt();

            // Get tool definitions
            var tools = ToolDefinitions.GetGroqTools();

            // ==== STEP 1: Gửi message + tools cho Groq ====
            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = request.Message }
            };

            var groqRequestPayload = new
            {
                model = model,
                messages = messages,
                tools = tools,
                tool_choice = "auto",
                max_tokens = 1000,
                temperature = 0.3
            };

            var client = _httpClientFactory.CreateClient();
            var url = "https://api.groq.com/openai/v1/chat/completions";

            var jsonPayload = JsonSerializer.Serialize(groqRequestPayload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            _logger.LogInformation("Groq request payload: {Payload}", jsonPayload);

            var httpContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var toolNames = string.Join(", ", tools.Select(t => t.Function.Name));
            _logger.LogInformation("Calling Groq API with message: {Message}, Tools count: {Count}, Tool names: {Names}",
                request.Message, tools.Count, toolNames);

            // Log tool definitions for debugging
            _logger.LogDebug("Tool definitions: {Tools}", JsonSerializer.Serialize(tools));
            var resp = await client.PostAsync(url, httpContent);
            var respText = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogError("Groq API error: {Status} - {Body}", resp.StatusCode, respText);
                return StatusCode(502, new ChatResponse
                {
                    Success = false,
                    Message = "Lỗi khi gọi Groq API.",
                    Errors = new List<string> { $"Status: {resp.StatusCode}", respText }
                });
            }

            // Parse Groq response
            var groqResponse = JsonSerializer.Deserialize<JsonElement>(respText);
            var choices = groqResponse.GetProperty("choices");

            if (choices.GetArrayLength() == 0)
            {
                return Ok(new ChatResponse
                {
                    Success = true,
                    Message = "OK",
                    Data = "Không có phản hồi từ Groq."
                });
            }

            var firstChoice = choices[0];
            var assistantMessage = firstChoice.GetProperty("message");

            // ==== STEP 2: Kiểm tra nếu Groq muốn gọi tool ====
            if (assistantMessage.TryGetProperty("tool_calls", out var toolCalls))
            {
                // ==== STEP 4: Gửi lại cho Groq với kết quả tool ====
                var messagesWithTools = new List<object>
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = request.Message }
                };

                // Add assistant's tool calls
                foreach (var tc in toolCalls.EnumerateArray())
                {
                    messagesWithTools.Add(new
                    {
                        role = "assistant",
                        tool_calls = new[] { new {
                            id = tc.GetProperty("id").GetString(),
                            type = "function",
                            function = new {
                                name = tc.GetProperty("function").GetProperty("name").GetString(),
                                arguments = tc.GetProperty("function").GetProperty("arguments").GetString()
                            }
                        }}
                    });
                }

                // Execute tools and add results
                foreach (var toolCall in toolCalls.EnumerateArray())
                {
                    var function = toolCall.GetProperty("function");
                    var toolName = function.GetProperty("name").GetString()!;
                    var arguments = function.GetProperty("arguments").GetString()!;

                    _logger.LogInformation("Groq wants to call tool: {ToolName} with args: {Args}", toolName, arguments);

                    string toolResult;
                    try
                    {
                        toolResult = await _toolDispatcher.DispatchAsync(toolName, arguments, currentUser);
                        _logger.LogInformation("Tool {ToolName} result: {Result}", toolName, toolResult);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error calling tool {ToolName}", toolName);
                        toolResult = JsonSerializer.Serialize(new { error = ex.Message, stackTrace = ex.StackTrace });
                    }

                    messagesWithTools.Add(new
                    {
                        role = "tool",
                        tool_call_id = toolCall.GetProperty("id").GetString(),
                        content = toolResult
                    });
                }

                // Second call to Groq
                var secondRequestPayload = new
                {
                    model = model,
                    messages = messagesWithTools,
                    max_tokens = 1000,
                    temperature = 0.3
                };

                var secondJsonPayload = JsonSerializer.Serialize(secondRequestPayload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var secondHttpContent = new StringContent(secondJsonPayload, Encoding.UTF8, "application/json");

                _logger.LogInformation("Calling Groq API second time with tool results");
                var secondResp = await client.PostAsync(url, secondHttpContent);
                var secondRespText = await secondResp.Content.ReadAsStringAsync();

                if (!secondResp.IsSuccessStatusCode)
                {
                    _logger.LogError("Groq API error on second call: {Status} - {Body}", secondResp.StatusCode, secondRespText);
                    return StatusCode(502, new ChatResponse
                    {
                        Success = false,
                        Message = "Lỗi khi xử lý kết quả từ tool.",
                        Errors = new List<string> { $"Status: {secondResp.StatusCode}", secondRespText }
                    });
                }

                // Parse second response
                var secondGroqResponse = JsonSerializer.Deserialize<JsonElement>(secondRespText);
                var secondChoices = secondGroqResponse.GetProperty("choices");

                if (secondChoices.GetArrayLength() == 0)
                {
                    return Ok(new ChatResponse
                    {
                        Success = true,
                        Message = "OK",
                        Data = "Không có phản hồi từ Groq sau khi xử lý tool."
                    });
                }

                var secondAssistantMessage = secondChoices[0].GetProperty("message");
                var finalContent = secondAssistantMessage.GetProperty("content").GetString();

                return Ok(new ChatResponse
                {
                    Success = true,
                    Message = "OK",
                    Data = finalContent
                });
            }
            else
            {
                // ==== Groq không cần gọi tool - trả lời trực tiếp ====
                var content = assistantMessage.GetProperty("content").GetString();

                return Ok(new ChatResponse
                {
                    Success = true,
                    Message = "OK",
                    Data = content
                });
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Groq API");
            return StatusCode(503, new ChatResponse
            {
                Success = false,
                Message = "Lỗi kết nối tới Groq API.",
                Errors = new List<string> { ex.Message }
            });
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timeout calling Groq API");
            return StatusCode(504, new ChatResponse
            {
                Success = false,
                Message = "Timeout khi gọi Groq API. Vui lòng thử lại.",
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

    /// <summary>
    /// Xây dựng system prompt cho Groq - hướng dẫn AI扮演 trợ lý cho hệ thống quản lý kho/bán hàng.
    /// </summary>
    private string BuildSystemPrompt()
    {
        return @"Bạn là trợ lý ảo cho hệ thống quản lý kho và bán hàng MEP Management System.

NHIỆM VỤ:
- Trả lời các câu hỏi về báo cáo, tồn kho, doanh thu, công nợ, hóa đơn...
- Khi cần thông tin cụ thể (số liệu, chi tiết), HÃY SỬ DỤNG CÁC TOOL ĐƯỢC CUNG CẤP để tra cứu dữ liệu THỰC TỪ HỆ THỐNG.
- TUYỆT ĐỐI KHÔNG được bịa đặt số liệu hoặc thông tin không có trong hệ thống.
- Nếu không tìm thấy dữ liệu, hãy thông báo cho user biết và đề nghị kiểm tra lại thông tin đầu vào.

NGUYÊN TẮC TRẢ LỜI:
1. Trả lời bằng TIẾNG VIỆT, thân thiện, ngắn gọn.
2. Khi có số liệu, trình bày rõ ràng: tổng doanh thu, số đơn, chi tiết...
3. Nếu user hỏi về sản phẩm cụ thể mà không cung cấp ID, hãy hỏi lại mã sản phẩm hoặc tên sản phẩm.
4. Nếu cần lọc theo ngày/tháng mà user không nói rõ, mặc định lấy dữ liệu 30 ngày gần nhất hoặc hôm nay nếu là báo cáo cuối ngày.

VÍ DỤ CÂU HỎI VÀ CÁCH XỬ LÝ:
- 'Cho tôi xem báo cáo hôm nay' → gọi get_daily_end_report(date=hôm nay)
- 'Doanh thu tháng này' → gọi get_revenue_by_time_report(fromDate=đầu tháng, toDate=hôm nay)
- 'Sản phẩm SP028 còn bao nhiêu' → Cần ID sản phẩm, nếu user cung cấp mã SP028 thì cần chuyển sang ID để gọi get_product_stock
- 'Tồn kho sắp hết' → gọi get_low_stock_report
- 'Công nợ nhà cung cấp X' → Cần ID nhà cung cấp để gọi get_supplier_debt_detail
- 'Trong kho có những loại Aptomat nào' → gọi search_products(keyword='Aptomat')
- 'Tìm sản phẩm điện' → gọi search_products(keyword='điện')
- 'Cho xem danh sách sản phẩm Laptop' → gọi search_products(keyword='Laptop')

KHÔNG ĐƯỢC LÀM:
- Không bịa số liệu khi không có dữ liệu
- Không hứa hẹn tính năng không có trong hệ thống
- Không tiết lộ cấu trúc database hoặc code nội bộ";
    }
}
