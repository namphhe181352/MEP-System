using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace MEP_Management_System.Services.ChatbotServices;

public class ChatbotRequest
{
    public string Message { get; set; } = string.Empty;
    public System.Collections.Generic.List<ChatbotMessageDto>? History { get; set; }
}

public class ChatbotMessageDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ChatbotResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Data { get; set; }
    public System.Collections.Generic.List<string>? Errors { get; set; }
}

public interface IChatbotService
{
    Task<ChatbotResponse> ProcessMessageAsync(
        string message,
        System.Collections.Generic.List<ChatbotMessageDto>? history,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
