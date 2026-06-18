namespace MEP_Management_System.DTOs;

public class BulkActionResultDto
{
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<string> Errors { get; set; } = new();
}
