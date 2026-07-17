namespace core_banking_lite.Interfaces
{
    public interface ISnsPublisher
    {
        Task PublishAsync(string topicArn, string message, CancellationToken ct = default);
    }
}
