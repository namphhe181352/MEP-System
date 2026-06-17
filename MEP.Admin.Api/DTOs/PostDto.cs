namespace MEP.Admin.Api.DTOs;

public class PostDto
{
    public Guid PostId { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public string Status { get; set; } = null!;
    public short IsPinned { get; set; }
    public int ViewCount { get; set; }
    public string CategoryName { get; set; } = null!;
    public string AuthorName { get; set; } = null!;
    public string AuthorEmail { get; set; } = null!;
    public DateTime? CreatedAt { get; set; }
    public int CommentCount { get; set; }
    public int ReportCount { get; set; }
}
