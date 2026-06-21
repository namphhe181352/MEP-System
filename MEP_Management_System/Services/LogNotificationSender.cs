namespace MEP_Management_System.Services;

public class LogNotificationSender : INotificationSender
{
    private readonly ILogger<LogNotificationSender> _logger;

    public LogNotificationSender(ILogger<LogNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string target, string title, string content, bool isUrgent)
    {
        var urgentFlag = isUrgent ? "[URGENT] " : "";
        _logger.LogInformation(
            "[NotificationSender] {Urgent}Sending to [{Target}] | Title: {Title} | Content: {Content}",
            urgentFlag, target, title, content);
        return Task.CompletedTask;
    }
}
