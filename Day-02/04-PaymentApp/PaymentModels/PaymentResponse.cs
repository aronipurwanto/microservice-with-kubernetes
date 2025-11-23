namespace PaymentModels;
public class PaymentResponse
{
    public string TransactionId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}