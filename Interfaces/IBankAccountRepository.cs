using core_banking_lite.Common;
using core_banking_lite.Entities;

namespace core_banking_lite.Interfaces
{
    public interface IBankAccountRepository
    {
        Task<decimal?> GetBalanceAsync(long accountId, CancellationToken ct = default);

        /// <summary>
        /// Atomically deducts the balance and writes an outbox message
        /// in a single database transaction.
        /// </summary>
        Task<Result<WithdrawalRecord>> DeductBalanceAndEnqueueEventAsync(long accountId, decimal amount, CancellationToken ct = default);
    }
}
