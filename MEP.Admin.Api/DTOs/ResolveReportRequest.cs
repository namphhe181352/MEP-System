namespace MEP.Admin.Api.DTOs;

public class ResolveReportRequest
{
    public string Action { get; set; } = null!; // "hide_post" | "suspend_3_days" | "ban_permanent" | "dismiss"
    public string? AdminNote { get; set; }
}
