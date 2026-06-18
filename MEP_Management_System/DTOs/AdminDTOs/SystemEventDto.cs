namespace MEP_Management_System.DTOs;

public class SystemEventDto
{
    public Guid LogId { get; set; }
    public string Action { get; set; } = null!;
    public string? Description { get; set; }
    public string? TableName { get; set; }
    public string? Level { get; set; }
    public string? Source { get; set; }
    public string? UserName { get; set; }
    public string? IpAddress { get; set; }
    public DateTime? Timestamp { get; set; }
}
