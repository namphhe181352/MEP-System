namespace MEP_Management_System.DTOs.SaleDTOs;

public class ApiErrorResponse
{
    public ErrorDetail Error { get; set; } = null!;

    public class ErrorDetail
    {
        public string Code { get; set; } = null!;
        public string Message { get; set; } = null!;
    }

    public static ApiErrorResponse Create(string code, string message)
    {
        return new ApiErrorResponse
        {
            Error = new ErrorDetail { Code = code, Message = message }
        };
    }
}
