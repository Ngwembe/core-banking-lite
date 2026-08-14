using core_banking_lite.Entities;
using core_banking_lite.Interfaces;
using Dapper;
using Microsoft.Data.Sqlite;

namespace core_banking_lite.Repositories
{
    public sealed class SqliteOutboxRepository(string connectionString) : IOutboxRepository
    {
        /// <summary>
        /// Gets unprocessed messages that have not been sent yet and have a retry count of less than 5.
        /// </summary>
        private const string FetchUnprocessedSql = """
        SELECT id,
               event_type AS EventType,
               payload,
               idempotencyKey AS IdempotencyKey,
               created_at AS CreatedAt,
               processed
        FROM   outbox_messages
        WHERE  processed = 0 AND retry_count < 5
        ORDER  BY created_at ASC
        LIMIT  @batchSize
        """;

        private const string IncrementRetryCountSql = """
        UPDATE outbox_messages
        SET    retry_count = retry_count + 1,
               last_modified_at = datetime('now')  
        WHERE  id = @id
        """;

        /// <summary>
        /// Gets unsent messages that have failed to send at least 5 times but less than 10 times, specifically for the 'WithdrawalEvent' type.
        /// Messages that have failed to send 10 or more times are considered permanently failed and will not be retried.
        /// </summary>
        private const string FetchUnsentMessagesSql = """
        SELECT id,
               event_type AS EventType,
               payload,
               idempotencyKey AS IdempotencyKey,
               created_at AS CreatedAt,
               processed
        FROM   outbox_messages
        WHERE  processed = 0
        AND    retry_count >= 5 AND retry_count < 10
        AND    event_type = 'WithdrawalEvent'
        ORDER  BY created_at ASC
        LIMIT  @batchSize
        """;

        private const string MarkProcessedSql = """
        UPDATE outbox_messages
        SET    processed    = 1,
               processed_at = datetime('now')
        WHERE  id = @id
        """;

        public async Task<IReadOnlyList<OutboxMessage>> FetchUnprocessedAsync(
            int batchSize, CancellationToken ct = default)
        {
            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync(ct);

            var results = await conn.QueryAsync<OutboxMessage>(
                new CommandDefinition(FetchUnprocessedSql, new { batchSize }, cancellationToken: ct));

            return results.AsList();
        }
        
        public async Task<IReadOnlyList<OutboxMessage>> FetchUnsentMessagesAsync(
            int batchSize, CancellationToken ct = default)
        {
            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync(ct);

            var results = await conn.QueryAsync<OutboxMessage>(
                new CommandDefinition(FetchUnsentMessagesSql, new { batchSize }, cancellationToken: ct));

            return results.AsList();
        }

        public async Task MarkProcessedAsync(string messageId, CancellationToken ct = default)
        {
            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync(ct);

            await conn.ExecuteAsync(
                new CommandDefinition(MarkProcessedSql, new { id = messageId }, cancellationToken: ct));
        }

        public async Task IncrementRetryCountAsync(string messageId, CancellationToken ct = default)
        {
            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync(ct);

            await conn.ExecuteAsync(
                new CommandDefinition(IncrementRetryCountSql, new { id = messageId }, cancellationToken: ct));
        }
    }
}
