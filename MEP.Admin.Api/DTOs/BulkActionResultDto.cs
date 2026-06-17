namespace MEP.Admin.Api.DTOs;

public class BulkActionResultDto
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
