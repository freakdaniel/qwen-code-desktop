namespace QwenCode.App.Models;

public sealed class RuntimePortWorkItem
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string SourceSystem { get; init; }

    public required string TargetModule { get; init; }

    public required string Stage { get; init; }

    public required string Summary { get; init; }

    public required string CompatibilityContract { get; init; }

    public required IReadOnlyList<string> EvidencePaths { get; init; }
}
