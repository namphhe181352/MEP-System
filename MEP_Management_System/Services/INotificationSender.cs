namespace MEP_Management_System.Services;

public interface INotificationSender
{
    Task SendAsync(string target, string title, string content, bool isUrgent);
}
