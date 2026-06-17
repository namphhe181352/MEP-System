namespace MEP.Admin.Api.DTOs;

public class SystemLogDto
{
    public Guid LogId { get; set; }
    public string Action { get; set; } = null!;
    public string? Description { get; set; }
    public string? TableName { get; set; }
    public string? EntityType { get; set; }
    public string? Level { get; set; }
    public string? Source { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string UserName { get; set; } = null!;
    public DateTime? Timestamp { get; set; }
}
