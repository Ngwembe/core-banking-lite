namespace core_banking_lite.Interfaces
{
    public interface ISnsPublisher
    {
        Task PublishAsync(string topicArn, string message, string idempotencyKey, CancellationToken ct = default);
    }
}
