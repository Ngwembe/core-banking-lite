public class InfrastructureOptions
{
    public const string SectionName = "Infrastructure";

    public string Region { get; set; } = string.Empty;
    public ConnectionStringsOptions ConnectionStrings { get; set; } = new();
}