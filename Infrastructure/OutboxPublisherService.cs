using core_banking_lite.Entities;
using core_banking_lite.Interfaces;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace core_banking_lite.Infrastructure
{
    public sealed class OutboxPublisherService(
    string connectionString,
    IOptions<InfrastructureOptions> infraOptions,
    ISnsPublisher snsPublisher,
    ILogger<OutboxPublisherService> logger) : BackgroundService
    {
        private readonly InfrastructureOptions _opts = infraOptions.Value;
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

        // Constant SQL — never interpolated
        private const string FetchUnprocessedSql = """
        SELECT id, event_type AS EventType, payload, created_at AS CreatedAt, processed
        FROM   outbox_messages
        WHERE  processed = 0
        ORDER  BY created_at ASC
        LIMIT  10
        """;

        private const string MarkProcessedSql = """
        UPDATE outbox_messages
        SET    processed    = 1,
               processed_at = datetime('now')
        WHERE  id = @id
        """;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PublishPendingMessagesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Outbox publisher cycle failed.");
                }

                await Task.Delay(_pollInterval, stoppingToken);
            }
        }

        private async Task PublishPendingMessagesAsync(CancellationToken ct)
        {
            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync(ct);

            var messages = (await conn.QueryAsync<OutboxMessage>(
                new CommandDefinition(FetchUnprocessedSql, cancellationToken: ct))).AsList();

            foreach (var message in messages)
            {
                try
                {
                    await snsPublisher.PublishAsync(_opts.SnsTopicArn, message.Payload, ct);

                    // Only mark processed after confirmed SNS delivery
                    await conn.ExecuteAsync(
                        new CommandDefinition(
                            MarkProcessedSql,
                            new { id = message.Id },    // always parameterized by Dapper
                            cancellationToken: ct));

                    logger.LogInformation(
                        "Outbox message {Id} ({EventType}) published to SNS.", message.Id, message.EventType);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to publish outbox message {Id} — will retry on next cycle.", message.Id);
                }
            }
        }
    }
}
