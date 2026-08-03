using MEP_Management_System.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MEP_Management_System.Services;

public class SignalRNotificationSender : INotificationSender
{
    private readonly IHubContext<MepHub> _hubContext;
    private readonly ILogger<SignalRNotificationSender> _logger;

    public SignalRNotificationSender(IHubContext<MepHub> hubContext, ILogger<SignalRNotificationSender> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendAsync(string target, string title, string content, bool isUrgent)
    {
        _logger.LogInformation("Broadcasting notification via SignalR to {Target}: {Title}", target, title);

        var payload = new 
        {
            title,
            content,
            isUrgent,
            target,
            timestamp = DateTime.UtcNow
        };

        // Broadcast to all connected clients.
        // The frontend components in Admin, Owner, and Staff layouts will listen 
        // to "SystemNotification" and display it if the target matches or if target is ALL.
        await _hubContext.Clients.All.SendAsync("SystemNotification", payload);
    }
}
