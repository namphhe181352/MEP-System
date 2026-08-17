using System.Threading;
using System.Threading.Tasks;
using MEP_Management_System.Services.ChatbotServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.Chatbot;

[ApiController]
[Route("api/chatbot")]
[Authorize]
[Tags("Chatbot AI")]
public class ChatController : ControllerBase
{
    private readonly IChatbotService _chatbotService;

    public ChatController(IChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    [HttpPost("message")]
    public async Task<IActionResult> SendMessage(
        [FromBody] ChatbotRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new ChatbotResponse
            {
                Success = false,
                Message = "Yêu cầu không hợp lệ: message trống."
            });
        }

        var response = await _chatbotService.ProcessMessageAsync(
            request.Message,
            request.History,
            User,
            cancellationToken);

        if (!response.Success)
        {
            if (response.Message.Contains("quá nhiều yêu cầu"))
            {
                return StatusCode(429, response);
            }
            if (response.Message.Contains("chưa được gán chi nhánh"))
            {
                return BadRequest(response);
            }
            if (response.Message.Contains("chưa được cấu hình"))
            {
                return StatusCode(500, response);
            }
            return StatusCode(503, response);
        }

        return Ok(response);
    }
}
