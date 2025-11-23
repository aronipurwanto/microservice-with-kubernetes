using PaymentModels;

namespace PaymentCore;
public interface IPaymentService
{
    Task<PaymentResult> ProcessPaymentAsync(PaymentRequest paymentRequest);
    Task<Transaction?> GetTransactionAsync(string transactionId);
    Task<IEnumerable<Transaction>> GetMerchantTransactionsAsync(string merchantId);
}