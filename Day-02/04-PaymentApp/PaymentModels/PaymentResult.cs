namespace PaymentModels;
public class PaymentResult
{
    public bool IsSuccess { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string AuthorizationCode { get; set; } = string.Empty;
}