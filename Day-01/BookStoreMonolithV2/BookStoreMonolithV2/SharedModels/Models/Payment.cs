namespace SharedModels.Models;

public enum PaymentStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
    Refunded = 5,
    PartiallyRefunded = 6
}

public class Payment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime PaymentDate { get; set; }
    public PaymentStatus Status { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? GatewayTransactionId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public List<Transaction> Transactions { get; set; } = new();
}

public class Transaction
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public decimal Amount { get; set; }
    public string TransactionType { get; set; } = string.Empty; // Authorization, Capture, Refund, Void
    public string GatewayTransactionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? GatewayResponse { get; set; }
    public string? ErrorMessage { get; set; }
}

public class PaymentCreateDto
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentMethod { get; set; } = string.Empty;
    public string CardToken { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class PaymentResponseDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime PaymentDate { get; set; }
    public PaymentStatus Status { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? GatewayTransactionId { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TransactionDto> Transactions { get; set; } = new();
}

public class TransactionDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? GatewayResponse { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdatePaymentStatusDto
{
    public PaymentStatus Status { get; set; }
    public string? GatewayTransactionId { get; set; }
}

public class RefundRequestDto
{
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}