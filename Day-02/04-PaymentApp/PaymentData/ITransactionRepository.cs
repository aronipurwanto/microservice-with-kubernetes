using PaymentModels;

namespace PaymentData;
public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(string id);
    Task<IEnumerable<Transaction>> GetByMerchantIdAsync(string merchantId);
    Task<Transaction> AddAsync(Transaction transaction);
    Task UpdateAsync(Transaction transaction);
}