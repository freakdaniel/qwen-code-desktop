namespace QwenCode.App.Models;

public sealed class QwenCompatibilityLayer
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Scope { get; init; }

    public required int Priority { get; init; }

    public required string Path { get; init; }

    public required bool Exists { get; init; }

    public required IReadOnlyList<string> Categories { get; init; }
}
