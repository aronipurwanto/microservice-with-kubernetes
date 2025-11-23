using PaymentData;
using PaymentModels;

namespace PaymentCore;
public class PaymentService : IPaymentService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(ITransactionRepository transactionRepository, ILogger<PaymentService> logger)
    {
        _transactionRepository = transactionRepository;
        _logger = logger;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest paymentRequest)
    {
        // Validate payment request
        var validationResult = ValidatePaymentRequest(paymentRequest);
        if (!validationResult.IsSuccess)
        {
            return validationResult;
        }

        // Create transaction record
        var transaction = new Transaction
        {
            CardNumber = MaskCardNumber(paymentRequest.CardNumber),
            Amount = paymentRequest.Amount,
            Currency = paymentRequest.Currency,
            MerchantId = paymentRequest.MerchantId,
            Status = "Processing"
        };

        try
        {
            // Save transaction
            await _transactionRepository.AddAsync(transaction);

            // Simulate payment processing with external gateway
            var paymentResult = await ProcessWithPaymentGateway(paymentRequest);

            // Update transaction status
            transaction.Status = paymentResult.IsSuccess ? "Completed" : "Failed";
            transaction.ProcessedAt = DateTime.UtcNow;
            transaction.AuthorizationCode = paymentResult.AuthorizationCode;
            
            await _transactionRepository.UpdateAsync(transaction);

            _logger.LogInformation("Payment processed for transaction {TransactionId}: {Status}", 
                transaction.Id, transaction.Status);

            return paymentResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment for transaction {TransactionId}", transaction.Id);
            transaction.Status = "Error";
            await _transactionRepository.UpdateAsync(transaction);

            return new PaymentResult
            {
                IsSuccess = false,
                ErrorMessage = "Payment processing failed due to technical error"
            };
        }
    }

    public async Task<Transaction?> GetTransactionAsync(string transactionId)
    {
        return await _transactionRepository.GetByIdAsync(transactionId);
    }

    public async Task<IEnumerable<Transaction>> GetMerchantTransactionsAsync(string merchantId)
    {
        return await _transactionRepository.GetByMerchantIdAsync(merchantId);
    }

    private PaymentResult ValidatePaymentRequest(PaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CardNumber) || request.CardNumber.Length < 13)
            return new PaymentResult { IsSuccess = false, ErrorMessage = "Invalid card number" };

        if (request.ExpiryYear < DateTime.Now.Year || 
            (request.ExpiryYear == DateTime.Now.Year && request.ExpiryMonth < DateTime.Now.Month))
            return new PaymentResult { IsSuccess = false, ErrorMessage = "Card has expired" };

        if (request.Amount <= 0)
            return new PaymentResult { IsSuccess = false, ErrorMessage = "Invalid amount" };

        if (string.IsNullOrWhiteSpace(request.MerchantId))
            return new PaymentResult { IsSuccess = false, ErrorMessage = "Merchant ID is required" };

        return new PaymentResult { IsSuccess = true };
    }

    private string MaskCardNumber(string cardNumber)
    {
        if (cardNumber.Length < 4) return cardNumber;
        return new string('*', cardNumber.Length - 4) + cardNumber[^4..];
    }

    private async Task<PaymentResult> ProcessWithPaymentGateway(PaymentRequest request)
    {
        // Simulate API call to payment gateway
        await Task.Delay(1000);

        // Simulate payment processing logic
        var random = new Random();
        var success = random.Next(0, 10) > 2; // 80% success rate

        if (success)
        {
            return new PaymentResult
            {
                IsSuccess = true,
                TransactionId = Guid.NewGuid().ToString(),
                AuthorizationCode = $"AUTH{random.Next(100000, 999999)}"
            };
        }

        return new PaymentResult
        {
            IsSuccess = false,
            ErrorMessage = "Payment declined: Insufficient funds"
        };
    }
}