using core_banking_lite.Common;
using core_banking_lite.Entities;
using core_banking_lite.Interfaces;
using core_banking_lite.Models.Events;
using Dapper;
using Microsoft.Data.Sqlite;
using System.Data;

namespace core_banking_lite.Repositories
{
    public sealed class SqliteBankAccountRepository(string connectionString) : IBankAccountRepository
    {
        private IDbConnection CreateConnection() => new SqliteConnection(connectionString);

        public async Task<decimal?> GetBalanceAsync(long accountId, CancellationToken ct = default)
        {
            const string sql = """
            SELECT balance_minor
            FROM   accounts
            WHERE  id        = @accountId
            AND    is_active  = 1
            """;

            using var conn = CreateConnection();

            // Dapper maps the INTEGER column to long — convert at the exit boundary.
            long? minor = await conn.ExecuteScalarAsync<long?>(
                new CommandDefinition(sql, new { accountId }, cancellationToken: ct));

            return minor is null ? null : MoneyConverter.FromMinorUnits(minor.Value);
        }

        // balance_minor arithmetic is exact INTEGER — no floating-point involved.
        private const string DeductBalanceSql = """
        UPDATE accounts
        SET    balance_minor = balance_minor - @amountMinor,
               updated_at = datetime('now')
        WHERE  id = @accountId
        AND    balance_minor >= @amountMinor
        AND    is_active = 1
        """;

        // Diagnostic — only executed when the UPDATE returns 0 rows.
        private const string DiagnoseFailureSql = """
        SELECT CASE
                   WHEN NOT EXISTS (SELECT 1 FROM accounts WHERE id = @accountId) THEN 'NOT_FOUND'
                   WHEN EXISTS     (SELECT 1 FROM accounts WHERE id = @accountId AND is_active = 0) THEN 'INACTIVE'
                   ELSE 'INSUFFICIENT_FUNDS'
               END AS reason
        """;

        private const string InsertOutboxSql = """
        INSERT INTO outbox_messages (id, event_type, payload, created_at, processed)
        VALUES (@id, @eventType, @payload, @createdAt, 0)
        """;

        public async Task<Result<WithdrawalRecord>> DeductBalanceAndEnqueueEventAsync(long accountId, decimal amount, CancellationToken ct = default)
        {
            // Convert at the entry boundary — all SQL sees only the exact integer.
            long amountMinor = MoneyConverter.ToMinorUnits(amount);

            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            try
            {
                // If balance < amount or account doesn't exist: rows == 0.
                int rows = await conn.ExecuteAsync(
                    new CommandDefinition(
                        DeductBalanceSql,
                        new { accountId, amountMinor },
                        transaction: tx,
                        cancellationToken: ct));

                if (rows == 0)
                {
                    // Classify the failure without reintroducing TOCTOU:
                    // this read does not gate any write — the transaction is already being abandoned.
                    string reason = await conn.ExecuteScalarAsync<string>(
                        new CommandDefinition(
                            DiagnoseFailureSql,
                            new { accountId },
                            transaction: tx,
                            cancellationToken: ct)) ?? "INSUFFICIENT_FUNDS";

                    await tx.RollbackAsync(ct);

                    return reason switch
                    {
                        "NOT_FOUND" => Result<WithdrawalRecord>.Fail($"Account {accountId} not found."),
                        "INACTIVE" => Result<WithdrawalRecord>.Fail($"Account {accountId} is inactive."),
                        _ => Result<WithdrawalRecord>.Fail("Insufficient funds.")
                    };
                }

                // If this insert fails, the withdrawal rolls back too — no phantom events.
                var outboxMessage = new
                {
                    id = Guid.NewGuid().ToString(),
                    eventType = "WithdrawalEvent",
                    payload = new WithdrawalEvent(amount, accountId, "SUCCESSFUL").ToJson(),
                    createdAt = DateTime.UtcNow.ToString("o")
                };

                await conn.ExecuteAsync(
                    new CommandDefinition(
                        InsertOutboxSql,
                        outboxMessage,
                        transaction: tx,
                        cancellationToken: ct));

                await tx.CommitAsync(ct);

                return Result<WithdrawalRecord>.Ok(new WithdrawalRecord(accountId, amount, DateTime.UtcNow));
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
    }
}