using core_banking_lite.Entities;
using core_banking_lite.Interfaces;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace core_banking_lite.Infrastructure
{
    public sealed class OutboxPublisherService(IOutboxRepository outboxRepository, IOptions<InfrastructureOptions> infraOptions,
            ISnsPublisher snsPublisher, ILogger<OutboxPublisherService> logger) : BackgroundService
    {
        private readonly InfrastructureOptions _opts = infraOptions.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TimeSpan currentInterval = TimeSpan.FromSeconds(_opts.OutboxPollIntervalSeconds);
            TimeSpan baseInterval = currentInterval;
            TimeSpan maxInterval = TimeSpan.FromSeconds(_opts.OutboxMaxPollIntervalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    int processed = await PublishPendingMessagesAsync(stoppingToken);

                    if (processed > 0)
                    {
                        // Messages were found — stay responsive, reset to base interval.
                        currentInterval = baseInterval;
                    }
                    else
                    {
                        // Idle cycle — back off exponentially up to the ceiling.
                        currentInterval = Min(TimeSpan.FromSeconds(currentInterval.TotalSeconds * _opts.OutboxBackOffMultiplier), maxInterval);

                        logger.LogDebug("Outbox idle — next poll in {Interval}s.", (int)currentInterval.TotalSeconds);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Outbox publisher cycle failed.");
                }

                await Task.Delay(currentInterval, stoppingToken);
            }
        }

        /// <returns>The number of messages successfully published in this cycle.</returns>
        private async Task<int> PublishPendingMessagesAsync(CancellationToken ct)
        {
            var messages = await outboxRepository.FetchUnprocessedAsync(_opts.OutboxBatchSize, ct);
            int published = 0;

            foreach (var message in messages)
            {
                try
                {
                    await snsPublisher.PublishAsync(_opts.SnsTopicArn, message.Payload, ct);
                    await outboxRepository.MarkProcessedAsync(message.Id, ct);

                    logger.LogInformation("Outbox message {Id} ({EventType}) published to SNS.", message.Id, message.EventType);

                    published++;
                }
                catch (OperationCanceledException)
                {
                    throw; // Propagate shutdown signal immediately.
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to publish outbox message {Id} — will retry on next cycle.", message.Id);
                }
            }

            return published;
        }

        private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
    }
}
