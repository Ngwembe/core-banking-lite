using System.Text.Json;
using core_banking_lite.Entities;
using core_banking_lite.Interfaces;
using core_banking_lite.Models.Configs;
using Microsoft.Extensions.Options;

namespace core_banking_lite.Infrastructure
{
    public sealed class OutboxReversalService(
        IOutboxRepository outboxRepository,
        IServiceScopeFactory scopeFactory,
        IOptions<InfrastructureOptions> infraOptions,
        ILogger<OutboxReversalService> logger) : BackgroundService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

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
                    int processed = await ReverseWithdrawalMessagesAsync(stoppingToken);

                    if (processed > 0)
                    {
                        currentInterval = baseInterval;
                    }
                    else
                    {
                        currentInterval = Min(TimeSpan.FromSeconds(currentInterval.TotalSeconds * _opts.OutboxBackOffMultiplier), maxInterval);
                        logger.LogDebug("Outbox reversal idle — next poll in {Interval}s.", (int)currentInterval.TotalSeconds);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Outbox reversal cycle failed.");
                }

                await Task.Delay(currentInterval, stoppingToken);
            }
        }

        private async Task<int> ReverseWithdrawalMessagesAsync(CancellationToken ct)
        {
            IReadOnlyList<OutboxMessage> messages = await outboxRepository.FetchUnsentMessagesAsync(_opts.OutboxBatchSize, ct);
            int reversed = 0;

            foreach (var message in messages)
            {
                try
                {
                    var payload = JsonSerializer.Deserialize<WithdrawalPayload>(message.Payload, JsonOptions);
                    if (payload is null || payload.AccountId <= 0 || payload.Amount <= 0)
                    {
                        logger.LogError("Invalid withdrawal payload for message {Id}.", message.Id);
                        continue;
                    }

                    using var scope = scopeFactory.CreateScope();
                    var bankAccountRepository = scope.ServiceProvider.GetRequiredService<IBankAccountRepository>();

                    var creditResult = await bankAccountRepository.EnqueuedEventBalanceCreditAsync(payload.AccountId, payload.Amount, ct);
                    if (!creditResult.IsSuccess)
                    {
                        logger.LogError("Failed to reverse withdrawal for message {Id}: {Error}", message.Id, creditResult.Error);
                        continue;
                    }

                    await outboxRepository.MarkProcessedAsync(message.Id, ct);
                    logger.LogInformation("Withdrawal reversal processed for outbox message {Id}.", message.Id);
                    reversed++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    await outboxRepository.IncrementRetryCountAsync(message.Id, ct);
                    logger.LogError(ex, "Failed to reverse outbox withdrawal message {Id} — will retry on next cycle.", message.Id);
                }
            }

            return reversed;
        }

        private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

        private sealed record WithdrawalPayload(decimal Amount, long AccountId, string Status, string IdempotencyKey);
    }
}
