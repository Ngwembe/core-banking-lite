using System.ComponentModel.DataAnnotations;

public class InfrastructureOptions
{
    public const string SectionName = "Infrastructure";

    /// <summary>AWS region system name — e.g. "af-south-1".</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Infrastructure:Region is required.")]
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Full SNS topic ARN. Optional in Development (FakeSnsPublisher is used),
    /// required in all other environments.
    /// Conditional enforcement is handled in Program.cs via environment guard.
    /// </summary>
    public string SnsTopicArn { get; set; } = string.Empty;
    public ConnectionStringsOptions ConnectionStrings { get; set; } = new();

    /// <summary>Number of outbox messages processed per polling cycle.</summary>
    [Range(1, 1000, ErrorMessage = "Infrastructure:OutboxBatchSize must be between 1 and 1000.")]
    public int OutboxBatchSize { get; set; } = 10;

    /// <summary>
    /// Base polling interval. Resets to this whenever messages are found.
    /// </summary>
    [Range(1, 60, ErrorMessage = "Infrastructure:OutboxPollIntervalSeconds must be between 1 and 60.")]
    public int OutboxPollIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Maximum polling interval reached after repeated idle cycles.
    /// Must be >= OutboxPollIntervalSeconds.
    /// </summary>
    [Range(1, 300, ErrorMessage = "Infrastructure:OutboxMaxPollIntervalSeconds must be between 1 and 300.")]
    public int OutboxMaxPollIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Back-off multiplier applied to the current interval on each idle cycle.
    /// 2.0 = double on each empty poll.
    /// </summary>
    [Range(1.0, 5.0, ErrorMessage = "Infrastructure:OutboxBackOffMultiplier must be between 1.0 and 5.0.")]
    public double OutboxBackOffMultiplier { get; set; } = 2.0;
}