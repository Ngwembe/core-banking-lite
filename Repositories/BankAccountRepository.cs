using core_banking_lite.Common;
using core_banking_lite.Entities;
using core_banking_lite.Interfaces;
using core_banking_lite.Models.Events;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace core_banking_lite.Repositories
{
    public sealed class BankAccountRepository(string connectionString) : IBankAccountRepository
    {
        private IDbConnection CreateConnection() => new SqlConnection(connectionString);

        public async Task<decimal?> GetBalanceAsync(long accountId, CancellationToken ct = default)
        {
            const string sql = """
            SELECT balance
            FROM   accounts
            WHERE  id        = @accountId
            AND    is_active  = 1
            """;

            using var conn = CreateConnection();
            return await conn.ExecuteScalarAsync<decimal?>(
                new CommandDefinition(sql, new { accountId }, cancellationToken: ct));
        }

        public async Task<bool> DeductBalanceAsync(long accountId, decimal amount, CancellationToken ct = default)
        {
            // Single atomic statement — no race condition possible.
            // The WHERE clause acts as both the existence check and the balance guard.
            const string sql = """
            UPDATE accounts
            SET    balance    = balance - @amount,
                   updated_at = GETUTCDATE()
            WHERE  id         = @accountId
            AND    balance    >= @amount
            AND    is_active   = 1
            """;

            using var conn = CreateConnection();
            int rows = await conn.ExecuteAsync(
                new CommandDefinition(sql, new { accountId, amount }, cancellationToken: ct));

            return rows > 0;
        }

        private const string DeductBalanceSql = """
        UPDATE accounts
        SET    balance    = balance - @amount,
               updated_at = GETUTCDATE()
        WHERE  id         = @accountId
        AND    balance    >= @amount
        AND    is_active   = 1
        """;

        private const string InsertOutboxSql = """
        INSERT INTO outbox_messages (id, event_type, payload, created_at, processed)
        VALUES (@id, @eventType, @payload, @createdAt, 0)
        """;

        public async Task<Result<WithdrawalRecord>> DeductBalanceAndEnqueueEventAsync(long accountId, decimal amount, CancellationToken ct = default)
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                // Step 1 — Atomic deduction: guard + update in one statement.
                // If balance < amount or account doesn't exist: rows == 0.
                int rows = await conn.ExecuteAsync(
                    new CommandDefinition(
                        DeductBalanceSql,
                        new { accountId, amount },   // always parameterized by Dapper
                        transaction: tx,
                        cancellationToken: ct));

                if (rows == 0)
                {
                    await tx.RollbackAsync(ct);
                    return Result<WithdrawalRecord>.Fail("Insufficient funds or account not found.");
                }

                // Step 2 — Write outbox message in the SAME transaction.
                // If this insert fails, the withdrawal rolls back too — no phantom events.
                var outboxMessage = new
                {
                    id = Guid.NewGuid(),
                    eventType = "WithdrawalEvent",
                    payload = new WithdrawalEvent(amount, accountId, "SUCCESSFUL").ToJson(),
                    createdAt = DateTime.UtcNow
                };

                await conn.ExecuteAsync(
                    new CommandDefinition(
                        InsertOutboxSql,
                        outboxMessage,              // always parameterized by Dapper
                        transaction: tx,
                        cancellationToken: ct));

                await tx.CommitAsync(ct);

                return Result<WithdrawalRecord>.Ok(
                    new WithdrawalRecord(accountId, amount, DateTime.UtcNow));
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
    }
}
