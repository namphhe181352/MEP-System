using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MEP_Management_System.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace MEP_Management_System.Controllers.Chatbot
{
    [ApiController]
    [Route("api/chatbot")]
    [Obsolete("Sử dụng ChatController thay thế với function calling")] // TODO: Xóa sau khi ChatController hoạt động ổn
    public class GroqTestController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public GroqTestController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public class GroqTestRequest
        {
            public string Message { get; set; } = string.Empty;
        }

        [AllowAnonymous]
        [HttpPost("test-connection")]
        public async Task<IActionResult> TestConnection([FromBody] GroqTestRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Message))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Yêu cầu không hợp lệ: message trống.",
                    Errors = new() { "Request body must include 'message'." }
                });
            }

            var apiKey = _config["GROQ_API_KEY"] ?? _config.GetValue<string>("GROQ_API_KEY");
            var model = _config["GROQ_MODEL"] ?? "gpt-3.5-turbo";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "GROQ_API_KEY chưa được cấu hình.",
                    Errors = new() { "Please set GROQ_API_KEY in appsettings or environment variables." }
                });
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = "https://api.groq.com/openai/v1/chat/completions";

                var payload = new
                {
                    model = model,
                    messages = new[] { new { role = "user", content = req.Message } },
                    max_tokens = 200
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var resp = await client.PostAsync(url, content);
                var respText = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Lỗi gọi Groq API.",
                        Errors = new() { $"Status: {resp.StatusCode}. Body: {respText}" }
                    });
                }

                // Try to extract the text content from typical OpenAI-style response
                try
                {
                    using var doc = JsonDocument.Parse(respText);
                    if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var first = choices[0];
                        if (first.TryGetProperty("message", out var messageProp) && messageProp.TryGetProperty("content", out var contentProp))
                        {
                            var text = contentProp.GetString();
                            return Ok(new ApiResponse<string> { Success = true, Message = "OK", Data = text });
                        }
                        if (first.TryGetProperty("text", out var textProp))
                        {
                            var text = textProp.GetString();
                            return Ok(new ApiResponse<string> { Success = true, Message = "OK", Data = text });
                        }
                    }
                }
                catch { /* ignore parse errors and return raw body below */ }

                // Fallback: return full raw response
                return Ok(new ApiResponse<string> { Success = true, Message = "Raw response", Data = respText });
            }
            catch (HttpRequestException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi kết nối tới Groq API.",
                    Errors = new() { ex.Message }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Lỗi nội bộ khi gọi Groq.",
                    Errors = new() { ex.Message }
                });
            }
        }
    }
}
