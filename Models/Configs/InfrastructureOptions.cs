public class InfrastructureOptions
{
    public const string SectionName = "Infrastructure";

    public string Region { get; set; } = string.Empty;
    public string SnsTopicArn { get; set; } = string.Empty;
    public ConnectionStringsOptions ConnectionStrings { get; set; } = new();

    public int OutboxBatchSize { get; set; } = 10;
}