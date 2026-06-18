namespace MEP_Management_System.DTOs;

public class ViolationReportDto
{
    public Guid ReportId { get; set; }
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = null!;
    public Guid PostId { get; set; }
    public string? PostTitle { get; set; }
    public string? CommentText { get; set; }
    public string ReporterName { get; set; } = null!;
    public Guid ReportedUserId { get; set; }
    public string ReporterEmail { get; set; } = null!;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByName { get; set; }
    public string? AdminNote { get; set; }
    public DateTime? CreatedAt { get; set; }
}
