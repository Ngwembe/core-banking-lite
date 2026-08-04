using core_banking_lite.Entities;

namespace core_banking_lite.Interfaces
{
    public interface IOutboxRepository
    {
        Task<IReadOnlyList<OutboxMessage>> FetchUnprocessedAsync(int batchSize, CancellationToken ct = default);
        Task MarkProcessedAsync(string messageId, CancellationToken ct = default);
        Task IncrementRetryCountAsync(string messageId, CancellationToken ct = default);
    }
}
