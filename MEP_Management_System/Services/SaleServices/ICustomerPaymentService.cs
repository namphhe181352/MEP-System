namespace MEP_Management_System.Services.SaleServices;

public interface ICustomerPaymentService
{
    Task<(Guid PaymentId, decimal CurrentDebt, decimal AdvanceBalance)> RecordPaymentAsync(
        Guid customerId, RecordCustomerPaymentRequest request, Guid branchId, Guid userId);
}

public class RecordCustomerPaymentRequest
{
    public decimal Amount { get; set; }
    public string? Method { get; set; }
    public string? Note { get; set; }
    public bool IsAdvance { get; set; }
}
