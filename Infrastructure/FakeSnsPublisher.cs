using core_banking_lite.Interfaces;

namespace core_banking_lite.Infrastructure
{
    public sealed class FakeSnsPublisher(ILogger<FakeSnsPublisher> logger) : ISnsPublisher
    {
        public Task PublishAsync(string topicArn, string message, CancellationToken ct = default)
        {
            logger.LogInformation(
                "[FakeSnsPublisher] Simulated SNS publish to {TopicArn}: {Message}", topicArn, message);
            return Task.CompletedTask;
        }
    }
}
