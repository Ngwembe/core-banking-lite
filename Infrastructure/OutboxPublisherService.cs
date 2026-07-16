using core_banking_lite.Entities;
using core_banking_lite.Interfaces;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace core_banking_lite.Infrastructure
{
    public sealed class OutboxPublisherService(
        IOutboxRepository outboxRepository,
        IOptions<InfrastructureOptions> infraOptions,
        ISnsPublisher snsPublisher,
        ILogger<OutboxPublisherService> logger) : BackgroundService
    {
        private readonly InfrastructureOptions _opts = infraOptions.Value;
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PublishPendingMessagesAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
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
            var messages = await outboxRepository.FetchUnprocessedAsync(_opts.OutboxBatchSize, ct);

            foreach (var message in messages)
            {
                try
                {
                    await snsPublisher.PublishAsync(_opts.SnsTopicArn, message.Payload, ct);
                    await outboxRepository.MarkProcessedAsync(message.Id, ct);

                    logger.LogInformation("Outbox message {Id} ({EventType}) published to SNS.", message.Id, message.EventType);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to publish outbox message {Id} — will retry on next cycle.", message.Id);
                }
            }
        }
    }
}
