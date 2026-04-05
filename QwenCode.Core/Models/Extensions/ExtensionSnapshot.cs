namespace QwenCode.App.Models;

public sealed class ExtensionSnapshot
{
    public required int TotalCount { get; init; }

    public required int ActiveCount { get; init; }

    public required int LinkedCount { get; init; }

    public required int MissingCount { get; init; }

    public required IReadOnlyList<ExtensionDefinition> Extensions { get; init; }
}
