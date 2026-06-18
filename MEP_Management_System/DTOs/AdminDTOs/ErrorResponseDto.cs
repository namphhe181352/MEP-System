namespace MEP_Management_System.DTOs;

public class ErrorResponseDto
{
    public ErrorDetail Error { get; set; } = new();
}

public class ErrorDetail
{
    public string Code { get; set; } = null!;
    public string Message { get; set; } = null!;
}
