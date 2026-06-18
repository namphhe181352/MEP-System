namespace MEP_Management_System.DTOs;

public class CommunityCategoryDto
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsArchived { get; set; }
    public int SortOrder { get; set; }
    public string? Slug { get; set; }
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    public int PostCount { get; set; }
}
