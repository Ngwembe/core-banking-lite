using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using core_banking_lite.Interfaces;

namespace core_banking_lite.Infrastructure
{
    public sealed class SnsPublisher(IAmazonSimpleNotificationService snsClient) : ISnsPublisher
    {
        public async Task PublishAsync(string topicArn, string message, CancellationToken ct = default)
        {
            await snsClient.PublishAsync(new PublishRequest
            {
                TopicArn = topicArn,
                Message = message
            }, ct);
        }
    }
}
