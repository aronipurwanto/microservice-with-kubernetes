namespace PaymentModels;
public class Transaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CardNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "Pending";
    public string MerchantId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string AuthorizationCode { get; set; } = string.Empty;
    public string LastFourDigits => CardNumber.Length >= 4 ? CardNumber[^4..] : CardNumber;
}