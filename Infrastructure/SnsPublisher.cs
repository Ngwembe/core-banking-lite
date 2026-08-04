using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using core_banking_lite.Interfaces;

namespace core_banking_lite.Infrastructure
{
    public sealed class SnsPublisher(IAmazonSimpleNotificationService snsClient) : ISnsPublisher
    {
        public async Task PublishAsync(string topicArn, string message, string idempotencyKey, CancellationToken ct = default)
        {
            await snsClient.PublishAsync(new PublishRequest
            {
                TopicArn = topicArn,
                Message = message,
                MessageDeduplicationId = idempotencyKey,
                MessageGroupId = "core-banking-lite-outbox",
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    { "IdempotencyKey", new MessageAttributeValue { DataType = "String", StringValue = idempotencyKey } }
                }
            }, ct);
        }
    }
}
