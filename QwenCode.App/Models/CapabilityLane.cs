namespace QwenCode.App.Models;

public sealed class CapabilityLane
{
    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required IReadOnlyList<string> Responsibilities { get; init; }
}
