namespace core_banking_lite.Entities
{
    public sealed record OutboxMessage
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string EventType { get; init; } = string.Empty;
        public string Payload { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public bool Processed { get; init; }
    }
}
