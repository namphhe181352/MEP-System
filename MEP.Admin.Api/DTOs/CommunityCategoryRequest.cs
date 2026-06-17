namespace MEP.Admin.Api.DTOs;

public class CreateCategoryRequest
{
    public string CategoryName { get; set; } = null!;
    public string? Description { get; set; }
    public int SortOrder { get; set; } = 0;
    public Guid? ParentId { get; set; }
}

public class UpdateCategoryRequest
{
    public string? CategoryName { get; set; }
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
}

public class ReorderCategoryRequest
{
    public int NewOrder { get; set; }
}
